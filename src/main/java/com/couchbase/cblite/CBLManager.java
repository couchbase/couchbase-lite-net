package com.couchbase.cblite;

import com.couchbase.cblite.replicator.CBLPusher;
import com.couchbase.cblite.replicator.CBLReplicator;

import java.net.MalformedURLException;
import java.net.URL;
import java.util.Map;

public enum CBLManager {

    INSTANCE;

    private CBLServer server;

    public CBLServer getServer() {
        return server;
    }

    public void setServer(CBLServer server) {
        this.server = server;
    }


    public CBLReplicator getReplicator(Map<String,Object> properties) throws CBLiteException {

        String source = (String)properties.get("source");
        String target = (String)properties.get("target");

        Boolean createTargetBoolean = (Boolean)properties.get("create_target");
        boolean createTarget = (createTargetBoolean != null && createTargetBoolean.booleanValue());

        Boolean continuousBoolean = (Boolean)properties.get("continuous");
        boolean continuous = (continuousBoolean != null && continuousBoolean.booleanValue());

        Boolean cancelBoolean = (Boolean)properties.get("cancel");
        boolean cancel = (cancelBoolean != null && cancelBoolean.booleanValue());

        // Map the 'source' and 'target' JSON params to a local database and remote URL:
        if(source == null || target == null) {
            throw new CBLiteException("source and target are both null", new CBLStatus(CBLStatus.BAD_REQUEST));
        }

        boolean push = false;

        CBLDatabase db = getServer().getExistingDatabaseNamed(source);
        String remoteStr = null;
        if(db != null) {
            remoteStr = target;
            push = true;
        } else {
            remoteStr = source;
            if(createTarget && !cancel) {
                db = getServer().getDatabaseNamed(target);
                if(!db.open()) {
                    throw new CBLiteException("cannot open database: " + db, new CBLStatus(CBLStatus.INTERNAL_SERVER_ERROR));
                }
            } else {
                db = getServer().getExistingDatabaseNamed(target);
            }
            if(db == null) {
                throw new CBLiteException("database is null", new CBLStatus(CBLStatus.NOT_FOUND));
            }
        }


        URL remote = null;
        try {
            remote = new URL(remoteStr);
        } catch (MalformedURLException e) {
            throw new CBLiteException("malformed remote url: " + remoteStr, new CBLStatus(CBLStatus.BAD_REQUEST));
        }
        if(remote == null || !remote.getProtocol().startsWith("http")) {
            throw new CBLiteException("remote URL is null or non-http: " + remoteStr, new CBLStatus(CBLStatus.BAD_REQUEST));
        }

        if(!cancel) {
            // Start replication:
            CBLReplicator repl = db.getReplicator(remote, getServer().getDefaultHttpClientFactory(), push, continuous, getServer().getWorkExecutor());
            if(repl == null) {
                throw new CBLiteException("unable to create replicator with remote: " + remote, new CBLStatus(CBLStatus.INTERNAL_SERVER_ERROR));
            }

            String filterName = (String)properties.get("filter");
            if(filterName != null) {
                repl.setFilterName(filterName);
                Map<String,Object> filterParams = (Map<String,Object>)properties.get("query_params");
                if(filterParams != null) {
                    repl.setFilterParams(filterParams);
                }
            }

            if(push) {
                ((CBLPusher)repl).setCreateTarget(createTarget);
            }
            return repl;

        } else {
            // Cancel replication:
            CBLReplicator repl = db.getActiveReplicator(remote, push);
            if(repl == null) {
                throw new CBLiteException("unable to lookup replicator with remote: " + remote, new CBLStatus(CBLStatus.NOT_FOUND));
            }
        }


        return null;
    }

}
