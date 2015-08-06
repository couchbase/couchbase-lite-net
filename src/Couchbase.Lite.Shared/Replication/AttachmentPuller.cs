using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Replicator
{
    internal sealed class AttachmentPuller : Replication
    {

        #region Constants

        private const string TAG = "AttachmentPuller";

        #endregion

        #region Variables

        private IList<AttachmentRequest> _attachmentsToPull;
        private Dictionary<String, AttachmentRequest> _requestLookup;
        private volatile int _httpConnectionCount;
        private readonly object _locker = new object ();

        #endregion

        #region Constructors

        internal AttachmentPuller(Database db, Uri remote, bool continuous, TaskFactory workExecutor)
            : this(db, remote, continuous, null, workExecutor) { }
        
        internal AttachmentPuller(Database db, Uri remote, bool continuous, IHttpClientFactory clientFactory, TaskFactory workExecutor) 
            : base(db, remote, continuous, clientFactory, workExecutor) {  }

        #endregion

        #region Private Methods
 
        private void FinishStopping()
        {
            StopRemoteRequests();
            lock (_locker) {
                _attachmentsToPull = null;
            }

            FireTrigger(ReplicationTrigger.StopImmediate);
        }

        /// <summary>Add an attachment to the appropriate queue to individually GET</summary>
        public AttachmentRequest QueueRemoteAttachment(Attachment att)
        {
            string digest = att.Metadata.Get("digest").ToString();

            lock(_locker)
            {
                if(_attachmentsToPull == null)
                {
                    _attachmentsToPull = new List<AttachmentRequest>(100);
                }

                if(_requestLookup == null)
                {
                    _requestLookup = new Dictionary<string, AttachmentRequest>();
                }

                if(_requestLookup.ContainsKey(digest))
                {
                    return _requestLookup.Get(digest);
                }

                AttachmentRequest req = new AttachmentRequest(att);

                _requestLookup.Add(digest, req);
                _attachmentsToPull.AddItem (req);

                return req;
            }
        }

        /// <summary>
        /// Start up some HTTP GETs, within our limit on the maximum simultaneous number
        /// The entire method is not synchronized, only the portion pulling work off the list
        /// Important to not hold the synchronized block while we do network access
        /// </summary>
        public void PullRemoteAttachments ()
        {
            //find the work to be done in a synchronized block
            var attachmentsToStartNow = new List<AttachmentRequest> ();
            lock (_locker) {
                while (LocalDatabase != null && _httpConnectionCount + attachmentsToStartNow.Count < ManagerOptions.Default.MaxOpenHttpConnections) {
                    if (_attachmentsToPull != null && _attachmentsToPull.Count > 0) {
                        // prefer to pull an attachment over a deleted revision
                        attachmentsToStartNow.AddItem (_attachmentsToPull [0]);
                        _attachmentsToPull.Remove (0);
                    } else {
                     break;
                    }
                }
            }

            //actually run it outside the synchronized block
            foreach (var att in attachmentsToStartNow)
            {
                PullRemoteAttachment(att);
            }
        }

        /// <summary>Fetches an attechment from the remote db
        ///     </summary>
        /// <remarks>
        /// Fetches an attechment from the remote db.
        /// </remarks>
        internal void PullRemoteAttachment (AttachmentRequest req)
        {
            Log.D (TAG, "PullRemoteAttachment with rev: {0}, att: {1}", req.attachment.Revision, req.attachment.Name);

            _httpConnectionCount++;

            // Construct a query. We want the revision history, and the bodies of attachments that have
            // been added since the latest revisions we have locally.
            // See: http://wiki.apache.org/couchdb/HTTP_Document_API#Getting_Attachments_With_a_Document

            var path = new StringBuilder("/" + Uri.EscapeUriString(req.attachment.Revision.Document.Id) + "/" + req.attachment.Name + "?rev=" + Uri.EscapeUriString(req.attachment.Revision.Id));

            //create a final version of this variable for the log statement inside
            //FIXME find a way to avoid this
            var pathInside = path.ToString();

            var blobWriter = LocalDatabase.AttachmentWriter;

            SendAsyncAttachmentRequest(HttpMethod.Get, pathInside, (buffer, bytesRead, complete, e) =>
            {
                try
                {
                    // OK, now we've got the response revision:
                    Log.D (TAG, "PullRemoteAttachment progress for rev: " + req.attachment.Revision);

                    if (buffer != null && e == null && bytesRead > 0)
                    {
                        Log.V(TAG, string.Format("read {0} bytes", bytesRead));
                        if (bytesRead != buffer.Length)
                        {
                            blobWriter.AppendData(buffer.SubList(0, bytesRead));
                        }
                        else
                        {
                            blobWriter.AppendData(buffer);
                        }

                        req.AppendData(buffer, bytesRead);
                    }
                    else if (e != null)
                    {
                        Log.E (TAG, "Error pulling remote attachment", e);
                        LastError = e;
                        complete = true;
                    }

                    if (req.progress != null)
                    {
                        req.progress(buffer, bytesRead, complete, e);
                    }

                    if (complete == true)
                    {
                        if (e == null)
                        {
                            blobWriter.Finish();
                            blobWriter.Install();
                        }

                        string digest = req.attachment.Metadata.Get("digest").ToString();
                        lock(_locker)
                        {
                            _requestLookup.Remove(digest);
                        }

                        Log.D (TAG, "PullRemoteAttachment complete");

                        req.SetComplete();

                        // Note that we've finished this task; then start another one if there
                        // are still revisions waiting to be pulled:
                        --_httpConnectionCount;
                        PullRemoteAttachments ();
                    }
                }
                catch(Exception ex)
                {
                    Log.E(TAG,"SendAsyncAttachmentRequest: ",ex);
                }
            });
        }

        #endregion
            
        #region Overrides

        public override bool CreateTarget { get { return false; } set { return; /* No-op intended. Only used in Pusher. */ } }

        public override bool IsPull { get { return false; } }

        public override bool IsAttachmentPull { get { return true; } }

        public override IEnumerable<string> DocIds { get; set; }

        public override IDictionary<string, string> Headers 
        {
            get { return clientFactory.Headers; } 
            set { clientFactory.Headers = value; } 
        }

        protected override void StopGraceful()
        {
            base.StopGraceful();

            FinishStopping();
        }

        protected override void PerformGoOffline()
        {
            base.PerformGoOffline();

            StopRemoteRequests();
        }

        protected override void PerformGoOnline()
        {
            base.PerformGoOnline();

            BeginReplicating();
        }

        internal override void ProcessInbox(RevisionList inbox)
        {
            PullRemoteAttachments();
        }

        internal override void BeginReplicating()
        {
            Log.D(TAG, string.Format("Using MaxOpenHttpConnections({0})", 
                ManagerOptions.Default.MaxOpenHttpConnections));
        }

        internal override void Stopping()
        {
            base.Stopping();
        }

        #endregion

        #region Nested Classes

        #endregion
    }

}
