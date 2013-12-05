package com.couchbase.lite.replicator;

import com.couchbase.lite.CouchbaseLiteException;
import com.couchbase.lite.Database;
import com.couchbase.lite.Manager;
import com.couchbase.lite.Misc;
import com.couchbase.lite.RevisionList;
import com.couchbase.lite.Status;
import com.couchbase.lite.internal.CBLBody;
import com.couchbase.lite.internal.RevisionInternal;
import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.replicator.changetracker.ChangeTracker;
import com.couchbase.lite.replicator.changetracker.ChangeTracker.TDChangeTrackerMode;
import com.couchbase.lite.replicator.changetracker.ChangeTrackerClient;
import com.couchbase.lite.storage.SQLException;
import com.couchbase.lite.support.BatchProcessor;
import com.couchbase.lite.support.Batcher;
import com.couchbase.lite.support.RemoteRequestCompletionBlock;
import com.couchbase.lite.support.SequenceMap;
import com.couchbase.lite.support.HttpClientFactory;
import com.couchbase.lite.util.Log;

import org.apache.http.client.HttpClient;
import org.apache.http.client.HttpResponseException;

import java.net.URL;
import java.net.URLEncoder;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ScheduledExecutorService;


@InterfaceAudience.Private
public class Puller extends Replication implements ChangeTrackerClient {

    private static final int MAX_OPEN_HTTP_CONNECTIONS = 16;

    protected Batcher<List<Object>> downloadsToInsert;
    protected List<RevisionInternal> revsToPull;
    protected ChangeTracker changeTracker;
    protected SequenceMap pendingSequences;
    protected volatile int httpConnectionCount;

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    public Puller(Database db, URL remote, boolean continuous, ScheduledExecutorService workExecutor) {
        this(db, remote, continuous, null, workExecutor);
    }

    /**
     * Constructor
     */
    @InterfaceAudience.Private
    public Puller(Database db, URL remote, boolean continuous, HttpClientFactory clientFactory, ScheduledExecutorService workExecutor) {
        super(db, remote, continuous, clientFactory, workExecutor);
    }

    @Override
    @InterfaceAudience.Public
    public boolean isPull() {
        return true;
    }

    @Override
    @InterfaceAudience.Public
    public boolean shouldCreateTarget() {
        return false;
    }

    @Override
    @InterfaceAudience.Public
    public void setCreateTarget(boolean createTarget) { }

    @Override
    public void beginReplicating() {
        if(downloadsToInsert == null) {
            int capacity = 200;
            int delay = 1000;
            downloadsToInsert = new Batcher<List<Object>>(workExecutor, capacity, delay, new BatchProcessor<List<Object>>() {
                @Override
                public void process(List<List<Object>> inbox) {
                    insertRevisions(inbox);
                }
            });
        }
        pendingSequences = new SequenceMap();
        Log.w(Database.TAG, this + " starting ChangeTracker with since=" + lastSequence);
        changeTracker = new ChangeTracker(remote, continuous ? TDChangeTrackerMode.LongPoll : TDChangeTrackerMode.OneShot, lastSequence, this);
        if(filterName != null) {
            changeTracker.setFilterName(filterName);
            if(filterParams != null) {
                changeTracker.setFilterParams(filterParams);
            }
        }
        if(!continuous) {
            asyncTaskStarted();
        }
        changeTracker.start();
    }

    @Override
    public void stop() {

        if(!running) {
            return;
        }

        if(changeTracker != null) {
	        changeTracker.setClient(null);  // stop it from calling my changeTrackerStopped()
	        changeTracker.stop();
	        changeTracker = null;
	        if(!continuous) {
	            asyncTaskFinished(1);  // balances asyncTaskStarted() in beginReplicating()
	        }
        }

        synchronized(this) {
            revsToPull = null;
        }

        super.stop();

        if(downloadsToInsert != null) {
            downloadsToInsert.flush();
        }
    }

    @Override
    public void stopped() {
        downloadsToInsert = null;
        super.stopped();
    }

    // Got a _changes feed entry from the ChangeTracker.
    @Override
    public void changeTrackerReceivedChange(Map<String, Object> change) {
        String lastSequence = change.get("seq").toString();
        String docID = (String)change.get("id");
        if(docID == null) {
            return;
        }
        if(!Database.isValidDocumentId(docID)) {
            Log.w(Database.TAG, String.format("%s: Received invalid doc ID from _changes: %s", this, change));
            return;
        }
        boolean deleted = (change.containsKey("deleted") && ((Boolean)change.get("deleted")).equals(Boolean.TRUE));
        List<Map<String,Object>> changes = (List<Map<String,Object>>)change.get("changes");
        for (Map<String, Object> changeDict : changes) {
            String revID = (String)changeDict.get("rev");
            if(revID == null) {
                continue;
            }
            PulledRevision rev = new PulledRevision(docID, revID, deleted, db);
            rev.setRemoteSequenceID(lastSequence);
            addToInbox(rev);
        }
        setChangesCount(getChangesCount() + changes.size());
        while(revsToPull != null && revsToPull.size() > 1000) {
            try {
                Thread.sleep(500);  // <-- TODO: why is this here?
            } catch(InterruptedException e) {

            }
        }
    }

    @Override
    public void changeTrackerStopped(ChangeTracker tracker) {
        Log.w(Database.TAG, this + ": ChangeTracker stopped");
        //FIXME tracker doesnt have error right now
//        if(error == null && tracker.getLastError() != null) {
//            error = tracker.getLastError();
//        }
        changeTracker = null;
        if(batcher != null) {
            batcher.flush();
        }

        asyncTaskFinished(1);
    }

    @Override
    public HttpClient getHttpClient() {
    	HttpClient httpClient = this.clientFactory.getHttpClient();

        return httpClient;
    }

    /**
     * Process a bunch of remote revisions from the _changes feed at once
     */
    @Override
    public void processInbox(RevisionList inbox) {
        // Ask the local database which of the revs are not known to it:
        //Log.w(Database.TAG, String.format("%s: Looking up %s", this, inbox));
        String lastInboxSequence = ((PulledRevision)inbox.get(inbox.size()-1)).getRemoteSequenceID();
        int total = getChangesCount() - inbox.size();
        if(!db.findMissingRevisions(inbox)) {
            Log.w(Database.TAG, String.format("%s failed to look up local revs", this));
            inbox = null;
        }
        //introducing this to java version since inbox may now be null everywhere
        int inboxCount = 0;
        if(inbox != null) {
            inboxCount = inbox.size();
        }
        if(getChangesCount() != total + inboxCount) {
            setChangesCount(total + inboxCount);
        }

        if(inboxCount == 0) {
            // Nothing to do. Just bump the lastSequence.
            Log.w(Database.TAG, String.format("%s no new remote revisions to fetch", this));
            long seq = pendingSequences.addValue(lastInboxSequence);
            pendingSequences.removeSequence(seq);
            setLastSequence(pendingSequences.getCheckpointedValue());
            return;
        }

        Log.v(Database.TAG, this + " fetching " + inboxCount + " remote revisions...");
        //Log.v(Database.TAG, String.format("%s fetching remote revisions %s", this, inbox));

        // Dump the revs into the queue of revs to pull from the remote db:
        synchronized (this) {
	        if(revsToPull == null) {
	            revsToPull = new ArrayList<RevisionInternal>(200);
	        }

	        for(int i=0; i < inbox.size(); i++) {
	            PulledRevision rev = (PulledRevision)inbox.get(i);
				// FIXME add logic here to pull initial revs in bulk
	            rev.setSequence(pendingSequences.addValue(rev.getRemoteSequenceID()));
	            revsToPull.add(rev);
	        }
		}

        pullRemoteRevisions();
    }

    /**
     * Start up some HTTP GETs, within our limit on the maximum simultaneous number
     *
     * The entire method is not synchronized, only the portion pulling work off the list
     * Important to not hold the synchronized block while we do network access
     */
    public void pullRemoteRevisions() {
        //find the work to be done in a synchronized block
        List<RevisionInternal> workToStartNow = new ArrayList<RevisionInternal>();
        synchronized (this) {
			while(httpConnectionCount + workToStartNow.size() < MAX_OPEN_HTTP_CONNECTIONS && revsToPull != null && revsToPull.size() > 0) {
				RevisionInternal work = revsToPull.remove(0);
				workToStartNow.add(work);
			}
		}

        //actually run it outside the synchronized block
        for(RevisionInternal work : workToStartNow) {
            pullRemoteRevision(work);
        }
    }

    /**
     * Fetches the contents of a revision from the remote db, including its parent revision ID.
     * The contents are stored into rev.properties.
     */
    public void pullRemoteRevision(final RevisionInternal rev) {
        asyncTaskStarted();
        ++httpConnectionCount;

        // Construct a query. We want the revision history, and the bodies of attachments that have
        // been added since the latest revisions we have locally.
        // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document
        StringBuilder path = new StringBuilder("/" + URLEncoder.encode(rev.getDocId()) + "?rev=" + URLEncoder.encode(rev.getRevId()) + "&revs=true&attachments=true");
        List<String> knownRevs = knownCurrentRevIDs(rev);
        if(knownRevs == null) {
            //this means something is wrong, possibly the replicator has shut down
            asyncTaskFinished(1);
            --httpConnectionCount;
            return;
        }
        if(knownRevs.size() > 0) {
            path.append("&atts_since=");
            path.append(joinQuotedEscaped(knownRevs));
        }

        //create a final version of this variable for the log statement inside
        //FIXME find a way to avoid this
        final String pathInside = path.toString();
        sendAsyncMultipartDownloaderRequest("GET", pathInside, null, db, new RemoteRequestCompletionBlock() {

            @Override
            public void onCompletion(Object result, Throwable e) {
                // OK, now we've got the response revision:
                if(result != null) {
                    Map<String,Object> properties = (Map<String,Object>)result;
                    List<String> history = db.parseCouchDBRevisionHistory(properties);
                    if(history != null) {
                        rev.setProperties(properties);
                        // Add to batcher ... eventually it will be fed to -insertRevisions:.
                        List<Object> toInsert = new ArrayList<Object>();
                        toInsert.add(rev);
                        toInsert.add(history);
                        downloadsToInsert.queueObject(toInsert);
                        asyncTaskStarted();
                    } else {
                        Log.w(Database.TAG, this + ": Missing revision history in response from " + pathInside);
                        setCompletedChangesCount(getCompletedChangesCount() + 1);
                    }
                } else {
                    if(e != null) {
                        Log.e(Database.TAG, "Error pulling remote revision", e);
                        error = e;
                    }
                    setCompletedChangesCount(getCompletedChangesCount() + 1);
                }

                // Note that we've finished this task; then start another one if there
                // are still revisions waiting to be pulled:
                asyncTaskFinished(1);
                --httpConnectionCount;
                pullRemoteRevisions();
            }
        });

    }

    /**
     * This will be called when _revsToInsert fills up:
     */
    public void insertRevisions(List<List<Object>> revs) {
        Log.i(Database.TAG, this + " inserting " + revs.size() + " revisions...");
        //Log.v(Database.TAG, String.format("%s inserting %s", this, revs));

        /* Updating self.lastSequence is tricky. It needs to be the received sequence ID of
        the revision for which we've successfully received and inserted (or rejected) it and
        all previous received revisions. That way, next time we can start tracking remote
        changes from that sequence ID and know we haven't missed anything. */
        /* FIX: The current code below doesn't quite achieve that: it tracks the latest
        sequence ID we've successfully processed, but doesn't handle failures correctly
        across multiple calls to -insertRevisions. I think correct behavior will require
        keeping an NSMutableIndexSet to track the fake-sequences of all processed revisions;
        then we can find the first missing index in that set and not advance lastSequence
        past the revision with that fake-sequence. */
        Collections.sort(revs, new Comparator<List<Object>>() {

            public int compare(List<Object> list1, List<Object> list2) {
                RevisionInternal reva = (RevisionInternal)list1.get(0);
                RevisionInternal revb = (RevisionInternal)list2.get(0);
                return Misc.TDSequenceCompare(reva.getSequence(), revb.getSequence());
            }

        });

        if(db == null) {
            asyncTaskFinished(revs.size());
            return;
        }
        db.beginTransaction();
        boolean success = false;
        try {
            for (List<Object> revAndHistory : revs) {
                PulledRevision rev = (PulledRevision)revAndHistory.get(0);
                long fakeSequence = rev.getSequence();
                List<String> history = (List<String>)revAndHistory.get(1);
                // Insert the revision:

                try {
                    db.forceInsert(rev, history, remote);
                } catch (CouchbaseLiteException e) {
                    if(e.getCBLStatus().getCode() == Status.FORBIDDEN) {
                        Log.i(Database.TAG, this + ": Remote rev failed validation: " + rev);
                    } else {
                        Log.w(Database.TAG, this + " failed to write " + rev + ": status=" + e.getCBLStatus().getCode());
                        error = new HttpResponseException(e.getCBLStatus().getCode(), null);
                        continue;
                    }
                }

                pendingSequences.removeSequence(fakeSequence);
            }

            Log.w(Database.TAG, this + " finished inserting " + revs.size() + " revisions");

            setLastSequence(pendingSequences.getCheckpointedValue());

            success = true;
        } catch(SQLException e) {
            Log.e(Database.TAG, this + ": Exception inserting revisions", e);
        } finally {
            db.endTransaction(success);
            asyncTaskFinished(revs.size());
        }

        setCompletedChangesCount(getCompletedChangesCount() + revs.size());
    }

    List<String> knownCurrentRevIDs(RevisionInternal rev) {
        if(db != null) {
            return db.getAllRevisionsOfDocumentID(rev.getDocId(), true).getAllRevIds();
        }
        return null;
    }

    public String joinQuotedEscaped(List<String> strings) {
        if(strings.size() == 0) {
            return "[]";
        }
        byte[] json = null;
        try {
            json = Manager.getObjectMapper().writeValueAsBytes(strings);
        } catch (Exception e) {
            Log.w(Database.TAG, "Unable to serialize json", e);
        }
        return URLEncoder.encode(new String(json));
    }

}

/**
 * A revision received from a remote server during a pull. Tracks the opaque remote sequence ID.
 */
class PulledRevision extends RevisionInternal {

    public PulledRevision(CBLBody body, Database database) {
        super(body, database);
    }

    public PulledRevision(String docId, String revId, boolean deleted, Database database) {
        super(docId, revId, deleted, database);
    }

    public PulledRevision(Map<String, Object> properties, Database database) {
        super(properties, database);
    }

    protected String remoteSequenceID;

    public String getRemoteSequenceID() {
        return remoteSequenceID;
    }

    public void setRemoteSequenceID(String remoteSequenceID) {
        this.remoteSequenceID = remoteSequenceID;
    }

}
