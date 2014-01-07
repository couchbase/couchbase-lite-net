package com.couchbase.lite.router;


import com.couchbase.lite.Attachment;
import com.couchbase.lite.ChangesOptions;
import com.couchbase.lite.CouchbaseLiteException;
import com.couchbase.lite.Database;
import com.couchbase.lite.Database.TDContentOptions;
import com.couchbase.lite.DocumentChange;
import com.couchbase.lite.Manager;
import com.couchbase.lite.Misc;
import com.couchbase.lite.QueryOptions;
import com.couchbase.lite.QueryRow;
import com.couchbase.lite.Reducer;
import com.couchbase.lite.ReplicationFilter;
import com.couchbase.lite.Mapper;
import com.couchbase.lite.RevisionList;
import com.couchbase.lite.Status;
import com.couchbase.lite.View;
import com.couchbase.lite.View.TDViewCollation;
import com.couchbase.lite.auth.FacebookAuthorizer;
import com.couchbase.lite.auth.PersonaAuthorizer;
import com.couchbase.lite.internal.Body;
import com.couchbase.lite.internal.RevisionInternal;
import com.couchbase.lite.replicator.Replication;
import com.couchbase.lite.util.Log;

import org.apache.http.client.HttpResponseException;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.lang.reflect.Method;
import java.net.MalformedURLException;
import java.net.URL;
import java.net.URLDecoder;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;


public class Router implements Database.ChangeListener {

    private Manager manager;
    private Database db;
    private URLConnection connection;
    private Map<String,String> queries;
    private boolean changesIncludesDocs = false;
    private RouterCallbackBlock callbackBlock;
    private boolean responseSent = false;
    private boolean waiting = false;
    private ReplicationFilter changesFilter;
    private boolean longpoll = false;

    public static String getVersionString() {
        return Manager.VERSION;
    }

    public Router(Manager manager, URLConnection connection) {
        this.manager = manager;
        this.connection = connection;
    }

    public void setCallbackBlock(RouterCallbackBlock callbackBlock) {
        this.callbackBlock = callbackBlock;
    }

    public Map<String,String> getQueries() {
        if(queries == null) {
            String queryString = connection.getURL().getQuery();
            if(queryString != null && queryString.length() > 0) {
                queries = new HashMap<String,String>();
                for (String component : queryString.split("&")) {
                    int location = component.indexOf('=');
                    if(location > 0) {
                        String key = component.substring(0, location);
                        String value = component.substring(location + 1);
                        queries.put(key, value);
                    }
                }

            }
        }
        return queries;
    }

    public String getQuery(String param) {
        Map<String,String> queries = getQueries();
        if(queries != null) {
            String value = queries.get(param);
            if(value != null) {
                return URLDecoder.decode(value);
            }
        }
        return null;
    }

    public boolean getBooleanQuery(String param) {
        String value = getQuery(param);
        return (value != null) && !"false".equals(value) && !"0".equals(value);
    }

    public int getIntQuery(String param, int defaultValue) {
        int result = defaultValue;
        String value = getQuery(param);
        if(value != null) {
            try {
                result = Integer.parseInt(value);
            } catch (NumberFormatException e) {
                //ignore, will return default value
            }
        }

        return result;
    }

    public Object getJSONQuery(String param) {
        String value = getQuery(param);
        if(value == null) {
            return null;
        }
        Object result = null;
        try {
            result = Manager.getObjectMapper().readValue(value, Object.class);
        } catch (Exception e) {
            Log.w("Unable to parse JSON Query", e);
        }
        return result;
    }

    public boolean cacheWithEtag(String etag) {
        String eTag = String.format("\"%s\"", etag);
        connection.getResHeader().add("Etag", eTag);
        String requestIfNoneMatch = connection.getRequestProperty("If-None-Match");
        return eTag.equals(requestIfNoneMatch);
    }

    public Map<String,Object> getBodyAsDictionary() {
        try {
            InputStream contentStream = connection.getRequestInputStream();
            Map<String,Object> bodyMap = Manager.getObjectMapper().readValue(contentStream, Map.class);
            return bodyMap;
        } catch (IOException e) {
            Log.w(Database.TAG, "WARNING: Exception parsing body into dictionary", e);
            return null;
        }
    }

    public EnumSet<TDContentOptions> getContentOptions() {
        EnumSet<TDContentOptions> result = EnumSet.noneOf(TDContentOptions.class);
        if(getBooleanQuery("attachments")) {
            result.add(TDContentOptions.TDIncludeAttachments);
        }
        if(getBooleanQuery("local_seq")) {
            result.add(TDContentOptions.TDIncludeLocalSeq);
        }
        if(getBooleanQuery("conflicts")) {
            result.add(TDContentOptions.TDIncludeConflicts);
        }
        if(getBooleanQuery("revs")) {
            result.add(TDContentOptions.TDIncludeRevs);
        }
        if(getBooleanQuery("revs_info")) {
            result.add(TDContentOptions.TDIncludeRevsInfo);
        }
        return result;
    }

    public boolean getQueryOptions(QueryOptions options) {
        // http://wiki.apache.org/couchdb/HTTP_view_API#Querying_Options
        options.setSkip(getIntQuery("skip", options.getSkip()));
        options.setLimit(getIntQuery("limit", options.getLimit()));
        options.setGroupLevel(getIntQuery("group_level", options.getGroupLevel()));
        options.setDescending(getBooleanQuery("descending"));
        options.setIncludeDocs(getBooleanQuery("include_docs"));
        options.setUpdateSeq(getBooleanQuery("update_seq"));
        if(getQuery("inclusive_end") != null) {
            options.setInclusiveEnd(getBooleanQuery("inclusive_end"));
        }
        if(getQuery("reduce") != null) {
            options.setReduce(getBooleanQuery("reduce"));
        }
        options.setGroup(getBooleanQuery("group"));
        options.setContentOptions(getContentOptions());


        List<Object> keys;

        Object keysParam = getJSONQuery("keys");
        if (keysParam != null && !(keysParam instanceof List)) {
            return false;
        }
        else {
            keys = ( List<Object>) keysParam;
        }
        if (keys == null) {
            Object key = getJSONQuery("key");
            if(key != null) {
                keys = new ArrayList<Object>();
                keys.add(key);
            }
        }
        if (keys != null) {
            options.setKeys(keys);
        }
        else {
            options.setStartKey(getJSONQuery("startkey"));
            options.setEndKey(getJSONQuery("endkey"));
        }

        return true;
    }

    public String getMultipartRequestType() {
        String accept = connection.getRequestProperty("Accept");
        if(accept.startsWith("multipart/")) {
            return accept;
        }
        return null;
    }

    public Status openDB() {
        if(db == null) {
            return new Status(Status.INTERNAL_SERVER_ERROR);
        }
        if(!db.exists()) {
            return new Status(Status.NOT_FOUND);
        }
        if(!db.open()) {
            return new Status(Status.INTERNAL_SERVER_ERROR);
        }
        return new Status(Status.OK);
    }

    public static List<String> splitPath(URL url) {
        String pathString = url.getPath();
        if(pathString.startsWith("/")) {
            pathString = pathString.substring(1);
        }
        List<String> result = new ArrayList<String>();
        //we want empty string to return empty list
        if(pathString.length() == 0) {
            return result;
        }
        for (String component : pathString.split("/")) {
            result.add(URLDecoder.decode(component));
        }
        return result;
    }

    public void sendResponse() {
        if(!responseSent) {
            responseSent = true;
            if(callbackBlock != null) {
                callbackBlock.onResponseReady();
            }
        }
    }

    public void start() {
        // Refer to: http://wiki.apache.org/couchdb/Complete_HTTP_API_Reference

        // We're going to map the request into a method call using reflection based on the method and path.
        // Accumulate the method name into the string 'message':
        String method = connection.getRequestMethod();
        if("HEAD".equals(method)) {
            method = "GET";
        }
        String message = String.format("do_%s", method);

        // First interpret the components of the request:
        List<String> path = splitPath(connection.getURL());
        if(path == null) {
            connection.setResponseCode(Status.BAD_REQUEST);
            try {
                connection.getResponseOutputStream().close();
            } catch (IOException e) {
                Log.e(Database.TAG, "Error closing empty output stream");
            }
            sendResponse();
            return;
        }

        int pathLen = path.size();
        if(pathLen > 0) {
            String dbName = path.get(0);
            if(dbName.startsWith("_")) {
                message += dbName;  // special root path, like /_all_dbs
            } else {
                message += "_Database";
                if (!Manager.isValidDatabaseName(dbName)) {
                    Header resHeader = connection.getResHeader();
                    if (resHeader != null) {
                        resHeader.add("Content-Type", "application/json");
                    }
                    Map<String, Object> result = new HashMap<String, Object>();
                    result.put("error", "Invalid database");
                    result.put("status", Status.BAD_REQUEST );
                    connection.setResponseBody(new Body(result));
                    ByteArrayInputStream bais = new ByteArrayInputStream(connection.getResponseBody().getJson());
                    connection.setResponseInputStream(bais);

                    connection.setResponseCode(Status.BAD_REQUEST);
                    try {
                        connection.getResponseOutputStream().close();
                    } catch (IOException e) {
                        Log.e(Database.TAG, "Error closing empty output stream");
                    }
                    sendResponse();
                    return;
                }
                else {
                    boolean mustExist = false;
                    db = manager.getDatabaseWithoutOpening(dbName, mustExist);
                    if(db == null) {
                        connection.setResponseCode(Status.BAD_REQUEST);
                        try {
                            connection.getResponseOutputStream().close();
                        } catch (IOException e) {
                            Log.e(Database.TAG, "Error closing empty output stream");
                        }
                        sendResponse();
                        return;
                    }

                }
            }
        } else {
            message += "Root";
        }

        String docID = null;
        if(db != null && pathLen > 1) {
            message = message.replaceFirst("_Database", "_Document");
            // Make sure database exists, then interpret doc name:
            Status status = openDB();
            if(!status.isSuccessful()) {
                connection.setResponseCode(status.getCode());
                try {
                    connection.getResponseOutputStream().close();
                } catch (IOException e) {
                    Log.e(Database.TAG, "Error closing empty output stream");
                }
                sendResponse();
                return;
            }
            String name = path.get(1);
            if(!name.startsWith("_")) {
                // Regular document
                if(!Database.isValidDocumentId(name)) {
                    connection.setResponseCode(Status.BAD_REQUEST);
                    try {
                        connection.getResponseOutputStream().close();
                    } catch (IOException e) {
                        Log.e(Database.TAG, "Error closing empty output stream");
                    }
                    sendResponse();
                    return;
                }
                docID = name;
            } else if("_design".equals(name) || "_local".equals(name)) {
                // "_design/____" and "_local/____" are document names
                if(pathLen <= 2) {
                    connection.setResponseCode(Status.NOT_FOUND);
                    try {
                        connection.getResponseOutputStream().close();
                    } catch (IOException e) {
                        Log.e(Database.TAG, "Error closing empty output stream");
                    }
                    sendResponse();
                    return;
                }
                docID = name + "/" + path.get(2);
                path.set(1, docID);
                path.remove(2);
                pathLen--;
            } else if(name.startsWith("_design") || name.startsWith("_local")) {
                // This is also a document, just with a URL-encoded "/"
                docID = name;
            } else {
                // Special document name like "_all_docs":
                message += name;
                if(pathLen > 2) {
                    List<String> subList = path.subList(2, pathLen-1);
                    StringBuilder sb = new StringBuilder();
                    Iterator<String> iter = subList.iterator();
                    while(iter.hasNext()) {
                        sb.append(iter.next());
                        if(iter.hasNext()) {
                            sb.append("/");
                        }
                    }
                    docID = sb.toString();
                }
            }
        }

        String attachmentName = null;
        if(docID != null && pathLen > 2) {
        	message = message.replaceFirst("_Document", "_Attachment");
        	// Interpret attachment name:
        	attachmentName = path.get(2);
        	if(attachmentName.startsWith("_") && docID.startsWith("_design")) {
        		// Design-doc attribute like _info or _view
        		message = message.replaceFirst("_Attachment", "_DesignDocument");
        		docID = docID.substring(8); // strip the "_design/" prefix
        		attachmentName = pathLen > 3 ? path.get(3) : null;
        	} else {
        		if (pathLen > 3) {
        			List<String> subList = path.subList(2, pathLen);
        			StringBuilder sb = new StringBuilder();
        			Iterator<String> iter = subList.iterator();
        			while(iter.hasNext()) {
        				sb.append(iter.next());
        				if(iter.hasNext()) {
        					//sb.append("%2F");
        					sb.append("/");
        				}
        			}
        			attachmentName = sb.toString();
        		}
        	}
        }

        //Log.d(TAG, "path: " + path + " message: " + message + " docID: " + docID + " attachmentName: " + attachmentName);

        // Send myself a message based on the components:
        Status status = null;
        try {

            Method m = Router.class.getMethod(message, Database.class, String.class, String.class);
            status = (Status)m.invoke(this, db, docID, attachmentName);

        } catch (NoSuchMethodException msme) {
            try {
                String errorMessage = "Router unable to route request to " + message;
                Log.e(Database.TAG, errorMessage);
                Map<String, Object> result = new HashMap<String, Object>();
                result.put("error", "not_found");
                result.put("reason", errorMessage);
                connection.setResponseBody(new Body(result));
                Method m = Router.class.getMethod("do_UNKNOWN", Database.class, String.class, String.class);
                status = (Status)m.invoke(this, db, docID, attachmentName);
            } catch (Exception e) {
                //default status is internal server error
                Log.e(Database.TAG, "Router attempted do_UNKNWON fallback, but that threw an exception", e);
                Map<String, Object> result = new HashMap<String, Object>();
                result.put("error", "not_found");
                result.put("reason", "Router unable to route request");
                connection.setResponseBody(new Body(result));
                status = new Status(Status.NOT_FOUND);
            }
        } catch (Exception e) {
            String errorMessage = "Router unable to route request to " + message;
            Log.e(Database.TAG, errorMessage, e);
            Map<String, Object> result = new HashMap<String, Object>();
            result.put("error", "not_found");
            result.put("reason", errorMessage + e.toString());
            connection.setResponseBody(new Body(result));
            if (e instanceof CouchbaseLiteException) {
                status = ((CouchbaseLiteException)e).getCBLStatus();
            }
            else {
                status = new Status(Status.NOT_FOUND);
            }
        }

        // Configure response headers:
        if(status.isSuccessful() && connection.getResponseBody() == null && connection.getHeaderField("Content-Type") == null) {
            connection.setResponseBody(new Body("{\"ok\":true}".getBytes()));
        }

        if(connection.getResponseBody() != null && connection.getResponseBody().isValidJSON()) {
            Header resHeader = connection.getResHeader();
            if (resHeader != null) {
                resHeader.add("Content-Type", "application/json");
            }
            else {
                Log.w(Database.TAG, "Cannot add Content-Type header because getResHeader() returned null");
            }
        }

        // Check for a mismatch between the Accept request header and the response type:
        String accept = connection.getRequestProperty("Accept");
        if(accept != null && !"*/*".equals(accept)) {
            String responseType = connection.getBaseContentType();
            if(responseType != null && accept.indexOf(responseType) < 0) {
                Log.e(Database.TAG, String.format("Error 406: Can't satisfy request Accept: %s", accept));
                status = new Status(Status.NOT_ACCEPTABLE);
            }
        }

        connection.getResHeader().add("Server", String.format("Couchbase Lite %s", getVersionString()));

        // If response is ready (nonzero status), tell my client about it:
        if(status.getCode() != 0) {
            connection.setResponseCode(status.getCode());

            if(connection.getResponseBody() != null) {
                ByteArrayInputStream bais = new ByteArrayInputStream(connection.getResponseBody().getJson());
                connection.setResponseInputStream(bais);
            } else {

                try {
                    connection.getResponseOutputStream().close();
                } catch (IOException e) {
                    Log.e(Database.TAG, "Error closing empty output stream");
                }
            }
            sendResponse();
        }
    }

    public void stop() {
        callbackBlock = null;
        if(db != null) {
            db.removeChangeListener(this);
        }
    }

    public Status do_UNKNOWN(Database db, String docID, String attachmentName) {
        return new Status(Status.BAD_REQUEST);
    }

    /*************************************************************************************************/
    /*** Router+Handlers                                                                         ***/
    /*************************************************************************************************/

    public void setResponseLocation(URL url) {
        String location = url.toExternalForm();
        String query = url.getQuery();
        if(query != null) {
            int startOfQuery = location.indexOf(query);
            if(startOfQuery > 0) {
                location = location.substring(0, startOfQuery);
            }
        }
        connection.getResHeader().add("Location", location);
    }

    /** SERVER REQUESTS: **/

    public Status do_GETRoot(Database _db, String _docID, String _attachmentName) {
        Map<String,Object> info = new HashMap<String,Object>();
        info.put("CBLite", "Welcome");
        info.put("couchdb", "Welcome"); // for compatibility
        info.put("version", getVersionString());
        connection.setResponseBody(new Body(info));
        return new Status(Status.OK);
    }

    public Status do_GET_all_dbs(Database _db, String _docID, String _attachmentName) {
        List<String> dbs = manager.getAllDatabaseNames();
        connection.setResponseBody(new Body(dbs));
        return new Status(Status.OK);
    }

    public Status do_GET_session(Database _db, String _docID, String _attachmentName) {
        // Send back an "Admin Party"-like response
        Map<String,Object> session= new HashMap<String,Object>();
        Map<String,Object> userCtx = new HashMap<String,Object>();
        String[] roles = {"_admin"};
        session.put("ok", true);
        userCtx.put("name", null);
        userCtx.put("roles", roles);
        session.put("userCtx", userCtx);
        connection.setResponseBody(new Body(session));
        return new Status(Status.OK);
    }

    public Status do_POST_replicate(Database _db, String _docID, String _attachmentName) {

        Replication replicator;

        // Extract the parameters from the JSON request body:
        // http://wiki.apache.org/couchdb/Replication
        Map<String,Object> body = getBodyAsDictionary();
        if(body == null) {
            return new Status(Status.BAD_REQUEST);
        }

        try {
            replicator = manager.getReplicator(body);
        } catch (CouchbaseLiteException e) {
            Map<String, Object> result = new HashMap<String, Object>();
            result.put("error", e.toString());
            connection.setResponseBody(new Body(result));
            return e.getCBLStatus();
        }

        Boolean cancelBoolean = (Boolean)body.get("cancel");
        boolean cancel = (cancelBoolean != null && cancelBoolean.booleanValue());

        if(!cancel) {
            replicator.start();
            Map<String,Object> result = new HashMap<String,Object>();
            result.put("session_id", replicator.getSessionID());
            connection.setResponseBody(new Body(result));
        } else {
            // Cancel replication:
            replicator.stop();
        }
        return new Status(Status.OK);

    }

    public Status do_GET_uuids(Database _db, String _docID, String _attachmentName) {
        int count = Math.min(1000, getIntQuery("count", 1));
        List<String> uuids = new ArrayList<String>(count);
        for(int i=0; i<count; i++) {
            uuids.add(Database.generateDocumentId());
        }
        Map<String,Object> result = new HashMap<String,Object>();
        result.put("uuids", uuids);
        connection.setResponseBody(new Body(result));
        return new Status(Status.OK);
    }

    public Status do_GET_active_tasks(Database _db, String _docID, String _attachmentName) {
        // http://wiki.apache.org/couchdb/HttpGetActiveTasks
        List<Map<String,Object>> activities = new ArrayList<Map<String,Object>>();
        for (Database db : manager.allOpenDatabases()) {
            List<Replication> activeReplicators = db.getAllReplications();
            if(activeReplicators != null) {
                for (Replication replicator : activeReplicators) {
                    String source = replicator.getRemoteUrl().toExternalForm();
                    String target = db.getName();
                    if(!replicator.isPull()) {
                        String tmp = source;
                        source = target;
                        target = tmp;
                    }
                    int processed = replicator.getCompletedChangesCount();
                    int total = replicator.getChangesCount();
                    String status = String.format("Processed %d / %d changes", processed, total);
                    int progress = (total > 0) ? Math.round(100 * processed / (float)total) : 0;
                    Map<String,Object> activity = new HashMap<String,Object>();
                    activity.put("type", "Replication");
                    activity.put("task", replicator.getSessionID());
                    activity.put("source", source);
                    activity.put("target", target);
                    activity.put("status", status);
                    activity.put("progress", progress);

                    if (replicator.getLastError() != null) {
                        String msg = String.format("Replicator error: %s.  Repl: %s.  Source: %s, Target: %s",
                                replicator.getLastError(), replicator, source, target);
                        Log.e(Database.TAG, msg);
                        Throwable error = replicator.getLastError();
                        int statusCode = 400;
                        if (error instanceof HttpResponseException) {
                            statusCode = ((HttpResponseException)error).getStatusCode();
                        }
                        Object[] errorObjects = new Object[]{ statusCode, replicator.getLastError().toString() };
                        activity.put("error", errorObjects);
                    }

                    activities.add(activity);
                }
            }
        }
        connection.setResponseBody(new Body(activities));
        return new Status(Status.OK);
    }

    /** DATABASE REQUESTS: **/

    public Status do_GET_Database(Database _db, String _docID, String _attachmentName) {
        // http://wiki.apache.org/couchdb/HTTP_database_API#Database_Information
        Status status = openDB();
        if(!status.isSuccessful()) {
            return status;
        }
        int num_docs = db.getDocumentCount();
        long update_seq = db.getLastSequenceNumber();
        Map<String, Object> result = new HashMap<String,Object>();
        result.put("db_name", db.getName());
        result.put("db_uuid", db.publicUUID());
        result.put("doc_count", num_docs);
        result.put("update_seq", update_seq);
        result.put("disk_size", db.totalDataSize());
        connection.setResponseBody(new Body(result));
        return new Status(Status.OK);
    }

    public Status do_PUT_Database(Database _db, String _docID, String _attachmentName) {
        if(db.exists()) {
            return new Status(Status.PRECONDITION_FAILED);
        }
        if(!db.open()) {
            return new Status(Status.INTERNAL_SERVER_ERROR);
        }
        setResponseLocation(connection.getURL());
        return new Status(Status.CREATED);
    }

    public Status do_DELETE_Database(Database _db, String _docID, String _attachmentName) throws CouchbaseLiteException {
        if(getQuery("rev") != null) {
            return new Status(Status.BAD_REQUEST);  // CouchDB checks for this; probably meant to be a document deletion
        }
        db.delete();
        return new Status(Status.OK);
    }

    /**
     * This is a hack to deal with the fact that there is currently no custom
     * serializer for QueryRow.  Instead, just convert everything to generic Maps.
     */
    private void convertCBLQueryRowsToMaps(Map<String,Object> allDocsResult) {
        List<Map<String, Object>> rowsAsMaps = new ArrayList<Map<String, Object>>();
        List<QueryRow> rows = (List<QueryRow>) allDocsResult.get("rows");
        for (QueryRow row : rows) {
            rowsAsMaps.add(row.asJSONDictionary());
        }
        allDocsResult.put("rows", rowsAsMaps);
    }

    public Status do_POST_Database(Database _db, String _docID, String _attachmentName) {
        Status status = openDB();
        if(!status.isSuccessful()) {
            return status;
        }
        return update(db, null, getBodyAsDictionary(), false);
    }

    public Status do_GET_Document_all_docs(Database _db, String _docID, String _attachmentName) throws CouchbaseLiteException {
        QueryOptions options = new QueryOptions();
        if(!getQueryOptions(options)) {
            return new Status(Status.BAD_REQUEST);
        }
        Map<String,Object> result = db.getAllDocs(options);
        convertCBLQueryRowsToMaps(result);
        if(result == null) {
            return new Status(Status.INTERNAL_SERVER_ERROR);
        }
        connection.setResponseBody(new Body(result));
        return new Status(Status.OK);
    }

    public Status do_POST_Document_all_docs(Database _db, String _docID, String _attachmentName) throws CouchbaseLiteException {
        QueryOptions options = new QueryOptions();
        if (!getQueryOptions(options)) {
            return new Status(Status.BAD_REQUEST);
        }

        Map<String, Object> body = getBodyAsDictionary();
        if (body == null) {
            return new Status(Status.BAD_REQUEST);
        }

        Map<String, Object> result = null;
        result = db.getAllDocs(options);
        convertCBLQueryRowsToMaps(result);

        if (result == null) {
            return new Status(Status.INTERNAL_SERVER_ERROR);
        }
        connection.setResponseBody(new Body(result));
        return new Status(Status.OK);
    }

    public Status do_POST_facebook_token(Database _db, String _docID, String _attachmentName) {

        Map<String, Object> body = getBodyAsDictionary();
        if (body == null) {
            return new Status(Status.BAD_REQUEST);
        }

        String email = (String) body.get("email");
        String remoteUrl = (String) body.get("remote_url");
        String accessToken = (String) body.get("access_token");
        if (email != null && remoteUrl != null && accessToken != null) {
            try {
                URL siteUrl = new URL(remoteUrl);
            } catch (MalformedURLException e) {
                Map<String, Object> result = new HashMap<String, Object>();
                result.put("error", "invalid remote_url: " + e.getLocalizedMessage());
                connection.setResponseBody(new Body(result));
                return new Status(Status.BAD_REQUEST);
            }

            try {
                FacebookAuthorizer.registerAccessToken(accessToken, email, remoteUrl);
            } catch (Exception e) {
                Map<String, Object> result = new HashMap<String, Object>();
                result.put("error", "error registering access token: " + e.getLocalizedMessage());
                connection.setResponseBody(new Body(result));
                return new Status(Status.BAD_REQUEST);
            }

            Map<String, Object> result = new HashMap<String, Object>();
            result.put("ok", "registered");
            connection.setResponseBody(new Body(result));
            return new Status(Status.OK);


        }
        else {
            Map<String, Object> result = new HashMap<String, Object>();
            result.put("error", "required fields: access_token, email, remote_url");
            connection.setResponseBody(new Body(result));
            return new Status(Status.BAD_REQUEST);
        }


    }

    public Status do_POST_persona_assertion(Database _db, String _docID, String _attachmentName) {

        Map<String, Object> body = getBodyAsDictionary();
        if (body == null) {
            return new Status(Status.BAD_REQUEST);
        }

        String assertion = (String) body.get("assertion");

        if (assertion == null) {
            Map<String, Object> result = new HashMap<String, Object>();
            result.put("error", "required fields: assertion");
            connection.setResponseBody(new Body(result));
            return new Status(Status.BAD_REQUEST);
        }

        try {
            String email = PersonaAuthorizer.registerAssertion(assertion);

            Map<String, Object> result = new HashMap<String, Object>();
            result.put("ok", "registered");
            result.put("email", email);

            connection.setResponseBody(new Body(result));
            return new Status(Status.OK);

        } catch (Exception e) {
            Map<String, Object> result = new HashMap<String, Object>();
            result.put("error", "error registering persona assertion: " + e.getLocalizedMessage());
            connection.setResponseBody(new Body(result));
            return new Status(Status.BAD_REQUEST);
        }



    }

    public Status do_POST_Document_bulk_docs(Database _db, String _docID, String _attachmentName) {
    	Map<String,Object> bodyDict = getBodyAsDictionary();
        if(bodyDict == null) {
            return new Status(Status.BAD_REQUEST);
        }
        List<Map<String,Object>> docs = (List<Map<String, Object>>) bodyDict.get("docs");

        boolean allObj = false;
        if(getQuery("all_or_nothing") == null || (getQuery("all_or_nothing") != null && (new Boolean(getQuery("all_or_nothing"))))) {
        	allObj = true;
        }
        //   allowConflict If false, an error status 409 will be returned if the insertion would create a conflict, i.e. if the previous revision already has a child.
        boolean allOrNothing = (allObj && allObj != false);
        boolean noNewEdits = true;
        if(getQuery("new_edits") == null || (getQuery("new_edits") != null && (new Boolean(getQuery("new_edits"))))) {
        	noNewEdits = false;
        }
        boolean ok = false;
        db.beginTransaction();
        List<Map<String,Object>> results = new ArrayList<Map<String,Object>>();
        try {
            for (Map<String, Object> doc : docs) {
                String docID = (String) doc.get("_id");
                RevisionInternal rev = null;
                Status status = new Status(Status.BAD_REQUEST);
                Body docBody = new Body(doc);
                if (noNewEdits) {
                    rev = new RevisionInternal(docBody, db);
                    if(rev.getRevId() == null || rev.getDocId() == null || !rev.getDocId().equals(docID)) {
                        status =  new Status(Status.BAD_REQUEST);
                    } else {
                        List<String> history = Database.parseCouchDBRevisionHistory(doc);
                        db.forceInsert(rev, history, null);
                    }
                } else {
                    Status outStatus = new Status();
                    rev = update(db, docID, docBody, false, allOrNothing, outStatus);
                    status.setCode(outStatus.getCode());
                }
                Map<String, Object> result = null;
                if(status.isSuccessful()) {
                    result = new HashMap<String, Object>();
                    result.put("ok", true);
                    result.put("id", docID);
                    if (rev != null) {
                        result.put("rev", rev.getRevId());
                    }
                } else if(allOrNothing) {
                    return status;  // all_or_nothing backs out if there's any error
                } else if(status.getCode() == Status.FORBIDDEN) {
                    result = new HashMap<String, Object>();
                    result.put("error", "validation failed");
                    result.put("id", docID);
                } else if(status.getCode() == Status.CONFLICT) {
                    result = new HashMap<String, Object>();
                    result.put("error", "conflict");
                    result.put("id", docID);
                } else {
                    return status;  // abort the whole thing if something goes badly wrong
                }
                if(result != null) {
                    results.add(result);
                }
            }
            Log.w(Database.TAG, String.format("%s finished inserting %d revisions in bulk", this, docs.size()));
            ok = true;
        } catch (Exception e) {
            Log.w(Database.TAG, String.format("%s: Exception inserting revisions in bulk", this), e);
        } finally {
            db.endTransaction(ok);
        }
        Log.d(Database.TAG, "results: " + results.toString());
        connection.setResponseBody(new Body(results));
        return new Status(Status.CREATED);
    }

    public Status do_POST_Document_revs_diff(Database _db, String _docID, String _attachmentName) {
        // http://wiki.apache.org/couchdb/HttpPostRevsDiff
        // Collect all of the input doc/revision IDs as TDRevisions:
        RevisionList revs = new RevisionList();
        Map<String, Object> body = getBodyAsDictionary();
        if(body == null) {
            return new Status(Status.BAD_JSON);
        }
        for (String docID : body.keySet()) {
            List<String> revIDs = (List<String>)body.get(docID);
            for (String revID : revIDs) {
                RevisionInternal rev = new RevisionInternal(docID, revID, false, db);
                revs.add(rev);
            }
        }

        // Look them up, removing the existing ones from revs:
        if(!db.findMissingRevisions(revs)) {
            return new Status(Status.DB_ERROR);
        }

        // Return the missing revs in a somewhat different format:
        Map<String, Object> diffs = new HashMap<String, Object>();
        for (RevisionInternal rev : revs) {
            String docID = rev.getDocId();

            List<String> missingRevs = null;
            Map<String, Object> idObj = (Map<String, Object>)diffs.get(docID);
            if(idObj != null) {
                missingRevs = (List<String>)idObj.get("missing");
            } else {
                idObj = new HashMap<String, Object>();
            }

            if(missingRevs == null) {
                missingRevs = new ArrayList<String>();
                idObj.put("missing", missingRevs);
                diffs.put(docID, idObj);
            }
            missingRevs.add(rev.getRevId());
        }

        // FIXME add support for possible_ancestors

        connection.setResponseBody(new Body(diffs));
        return new Status(Status.OK);
    }

    public Status do_POST_Document_compact(Database _db, String _docID, String _attachmentName) {
    	Status status = _db.compact();
    	if (status.getCode() < 300) {
    		Status outStatus = new Status();
    		outStatus.setCode(202);	// CouchDB returns 202 'cause it's an async operation
            return outStatus;
    	} else {
    		return status;
    	}
    }

    public Status do_POST_Document_ensure_full_commit(Database _db, String _docID, String _attachmentName) {
        return new Status(Status.OK);
    }

    /** CHANGES: **/

    public Map<String,Object> changesDictForRevision(RevisionInternal rev) {
        Map<String,Object> changesDict = new HashMap<String, Object>();
        changesDict.put("rev", rev.getRevId());

        List<Map<String,Object>> changes = new ArrayList<Map<String,Object>>();
        changes.add(changesDict);

        Map<String,Object> result = new HashMap<String,Object>();
        result.put("seq", rev.getSequence());
        result.put("id", rev.getDocId());
        result.put("changes", changes);
        if(rev.isDeleted()) {
            result.put("deleted", true);
        }
        if(changesIncludesDocs) {
            result.put("doc", rev.getProperties());
        }
        return result;
    }

    public Map<String,Object> responseBodyForChanges(List<RevisionInternal> changes, long since) {
        List<Map<String,Object>> results = new ArrayList<Map<String,Object>>();
        for (RevisionInternal rev : changes) {
            Map<String,Object> changeDict = changesDictForRevision(rev);
            results.add(changeDict);
        }
        if(changes.size() > 0) {
            since = changes.get(changes.size() - 1).getSequence();
        }
        Map<String,Object> result = new HashMap<String,Object>();
        result.put("results", results);
        result.put("last_seq", since);
        return result;
    }

    public Map<String, Object> responseBodyForChangesWithConflicts(List<RevisionInternal> changes, long since) {
        // Assumes the changes are grouped by docID so that conflicts will be adjacent.
        List<Map<String,Object>> entries = new ArrayList<Map<String, Object>>();
        String lastDocID = null;
        Map<String, Object> lastEntry = null;
        for (RevisionInternal rev : changes) {
            String docID = rev.getDocId();
            if(docID.equals(lastDocID)) {
                Map<String,Object> changesDict = new HashMap<String, Object>();
                changesDict.put("rev", rev.getRevId());
                List<Map<String,Object>> inchanges = (List<Map<String,Object>>)lastEntry.get("changes");
                inchanges.add(changesDict);
            } else {
                lastEntry = changesDictForRevision(rev);
                entries.add(lastEntry);
                lastDocID = docID;
            }
        }
        // After collecting revisions, sort by sequence:
        Collections.sort(entries, new Comparator<Map<String,Object>>() {
           public int compare(Map<String,Object> e1, Map<String,Object> e2) {
               return Misc.TDSequenceCompare((Long) e1.get("seq"), (Long) e2.get("seq"));
           }
        });

        Long lastSeq = (Long)entries.get(entries.size() - 1).get("seq");
        if(lastSeq == null) {
            lastSeq = since;
        }

        Map<String,Object> result = new HashMap<String,Object>();
        result.put("results", entries);
        result.put("last_seq", lastSeq);
        return result;
    }

    public void sendContinuousChange(RevisionInternal rev) {
        Map<String,Object> changeDict = changesDictForRevision(rev);
        try {
            String jsonString = Manager.getObjectMapper().writeValueAsString(changeDict);
            if(callbackBlock != null) {
                byte[] json = (jsonString + "\n").getBytes();
                OutputStream os = connection.getResponseOutputStream();
                try {
                    os.write(json);
                    os.flush();
                } catch (Exception e) {
                    Log.e(Database.TAG, "IOException writing to internal streams", e);
                }
            }
        } catch (Exception e) {
            Log.w("Unable to serialize change to JSON", e);
        }
    }

    @Override
    public void changed(Database.ChangeEvent event) {

        List<DocumentChange> changes = event.getChanges();
        for (DocumentChange change : changes) {

            RevisionInternal rev = change.getAddedRevision();

            Map<String, Object> paramsFixMe = null;  // TODO: these should not be null
            final boolean allowRevision = event.getSource().runFilter(changesFilter, paramsFixMe, rev);
            if (!allowRevision) {
                return;
            }

            if(longpoll) {
                Log.w(Database.TAG, "Router: Sending longpoll response");
                sendResponse();
                List<RevisionInternal> revs = new ArrayList<RevisionInternal>();
                revs.add(rev);
                Map<String,Object> body = responseBodyForChanges(revs, 0);
                if(callbackBlock != null) {
                    byte[] data = null;
                    try {
                        data = Manager.getObjectMapper().writeValueAsBytes(body);
                    } catch (Exception e) {
                        Log.w(Database.TAG, "Error serializing JSON", e);
                    }
                    OutputStream os = connection.getResponseOutputStream();
                    try {
                        os.write(data);
                        os.close();
                    } catch (IOException e) {
                        Log.e(Database.TAG, "IOException writing to internal streams", e);
                    }
                }
            } else {
                Log.w(Database.TAG, "Router: Sending continous change chunk");
                sendContinuousChange(rev);
            }

        }

    }

    public Status do_GET_Document_changes(Database _db, String docID, String _attachmentName) {
        // http://wiki.apache.org/couchdb/HTTP_database_API#Changes
        ChangesOptions options = new ChangesOptions();
        changesIncludesDocs = getBooleanQuery("include_docs");
        options.setIncludeDocs(changesIncludesDocs);
        String style = getQuery("style");
        if(style != null && style.equals("all_docs")) {
            options.setIncludeConflicts(true);
        }
        options.setContentOptions(getContentOptions());
        options.setSortBySequence(!options.isIncludeConflicts());
        options.setLimit(getIntQuery("limit", options.getLimit()));

        int since = getIntQuery("since", 0);

        String filterName = getQuery("filter");
        if(filterName != null) {
            changesFilter = db.getFilter(filterName);
            if(changesFilter == null) {
                return new Status(Status.NOT_FOUND);
            }
        }

        RevisionList changes = db.changesSince(since, options, changesFilter);

        if(changes == null) {
            return new Status(Status.INTERNAL_SERVER_ERROR);
        }

        String feed = getQuery("feed");
        longpoll = "longpoll".equals(feed);
        boolean continuous = !longpoll && "continuous".equals(feed);

        if(continuous || (longpoll && changes.size() == 0)) {
            connection.setChunked(true);
            connection.setResponseCode(Status.OK);
            sendResponse();
            if(continuous) {
                for (RevisionInternal rev : changes) {
                    sendContinuousChange(rev);
                }
            }
            db.addChangeListener(this);
         // Don't close connection; more data to come
            return new Status(0);
        } else {
            if(options.isIncludeConflicts()) {
                connection.setResponseBody(new Body(responseBodyForChangesWithConflicts(changes, since)));
            } else {
                connection.setResponseBody(new Body(responseBodyForChanges(changes, since)));
            }
            return new Status(Status.OK);
        }
    }

    /** DOCUMENT REQUESTS: **/

    public String getRevIDFromIfMatchHeader() {
        String ifMatch = connection.getRequestProperty("If-Match");
        if(ifMatch == null) {
            return null;
        }
        // Value of If-Match is an ETag, so have to trim the quotes around it:
        if(ifMatch.length() > 2 && ifMatch.startsWith("\"") && ifMatch.endsWith("\"")) {
            return ifMatch.substring(1,ifMatch.length() - 2);
        } else {
            return null;
        }
    }

    public String setResponseEtag(RevisionInternal rev) {
        String eTag = String.format("\"%s\"", rev.getRevId());
        connection.getResHeader().add("Etag", eTag);
        return eTag;
    }

    public Status do_GET_Document(Database _db, String docID, String _attachmentName) {
        try {
            // http://wiki.apache.org/couchdb/HTTP_Document_API#GET
            boolean isLocalDoc = docID.startsWith("_local");
            EnumSet<TDContentOptions> options = getContentOptions();
            String openRevsParam = getQuery("open_revs");
            if(openRevsParam == null || isLocalDoc) {
                // Regular GET:
                String revID = getQuery("rev");  // often null
                RevisionInternal rev = null;
                if(isLocalDoc) {
                    rev = db.getLocalDocument(docID, revID);
                } else {
                    rev = db.getDocumentWithIDAndRev(docID, revID, options);
                    // Handle ?atts_since query by stubbing out older attachments:
                    //?atts_since parameter - value is a (URL-encoded) JSON array of one or more revision IDs.
                    // The response will include the content of only those attachments that changed since the given revision(s).
                    //(You can ask for this either in the default JSON or as multipart/related, as previously described.)
                    List<String> attsSince = (List<String>)getJSONQuery("atts_since");
                    if (attsSince != null) {
                        String ancestorId = db.findCommonAncestorOf(rev, attsSince);
                        if (ancestorId != null) {
                            int generation = RevisionInternal.generationFromRevID(ancestorId);
                            db.stubOutAttachmentsIn(rev, generation + 1);
                        }
                    }
                }
                if(rev == null) {
                    return new Status(Status.NOT_FOUND);
                }
                if(cacheWithEtag(rev.getRevId())) {
                    return new Status(Status.NOT_MODIFIED);  // set ETag and check conditional GET
                }

                connection.setResponseBody(rev.getBody());
            } else {
                List<Map<String,Object>> result = null;
                if(openRevsParam.equals("all")) {
                    // Get all conflicting revisions:
                    RevisionList allRevs = db.getAllRevisionsOfDocumentID(docID, true);
                    result = new ArrayList<Map<String,Object>>(allRevs.size());
                    for (RevisionInternal rev : allRevs) {

                        try {
                            db.loadRevisionBody(rev, options);
                        } catch (CouchbaseLiteException e) {
                            if (e.getCBLStatus().getCode() != Status.INTERNAL_SERVER_ERROR) {
                                Map<String, Object> dict = new HashMap<String,Object>();
                                dict.put("missing", rev.getRevId());
                                result.add(dict);
                            }
                            else {
                                throw e;
                            }
                        }

                        Map<String, Object> dict = new HashMap<String,Object>();
                        dict.put("ok", rev.getProperties());
                        result.add(dict);

                    }
                } else {
                    // ?open_revs=[...] returns an array of revisions of the document:
                    List<String> openRevs = (List<String>)getJSONQuery("open_revs");
                    if(openRevs == null) {
                        return new Status(Status.BAD_REQUEST);
                    }
                    result = new ArrayList<Map<String,Object>>(openRevs.size());
                    for (String revID : openRevs) {
                        RevisionInternal rev = db.getDocumentWithIDAndRev(docID, revID, options);
                        if(rev != null) {
                            Map<String, Object> dict = new HashMap<String,Object>();
                            dict.put("ok", rev.getProperties());
                            result.add(dict);
                        } else {
                            Map<String, Object> dict = new HashMap<String,Object>();
                            dict.put("missing", revID);
                            result.add(dict);
                        }
                    }
                }
                String acceptMultipart  = getMultipartRequestType();
                if(acceptMultipart != null) {
                    //FIXME figure out support for multipart
                    throw new UnsupportedOperationException();
                } else {
                    connection.setResponseBody(new Body(result));
                }
            }
            return new Status(Status.OK);
        } catch (CouchbaseLiteException e) {
            return e.getCBLStatus();
        }
    }

    public Status do_GET_Attachment(Database _db, String docID, String _attachmentName) {
        try {
            // http://wiki.apache.org/couchdb/HTTP_Document_API#GET
            EnumSet<TDContentOptions> options = getContentOptions();
            options.add(TDContentOptions.TDNoBody);
            String revID = getQuery("rev");  // often null
            RevisionInternal rev = db.getDocumentWithIDAndRev(docID, revID, options);
            if(rev == null) {
                return new Status(Status.NOT_FOUND);
            }
            if(cacheWithEtag(rev.getRevId())) {
                return new Status(Status.NOT_MODIFIED);  // set ETag and check conditional GET
            }

            String type = null;
            String acceptEncoding = connection.getRequestProperty("accept-encoding");
            Attachment contents = db.getAttachmentForSequence(rev.getSequence(), _attachmentName);

            if (contents == null) {
                return new Status(Status.NOT_FOUND);
            }
            type = contents.getContentType();
            if (type != null) {
                connection.getResHeader().add("Content-Type", type);
            }
            if (acceptEncoding != null && acceptEncoding.contains("gzip") && contents.getGZipped()) {
                connection.getResHeader().add("Content-Encoding", "gzip");
            }

            connection.setResponseInputStream(contents.getContent());
            return new Status(Status.OK);

        } catch (CouchbaseLiteException e) {
            return e.getCBLStatus();
        }
    }

    /**
     * NOTE this departs from the iOS version, returning revision, passing status back by reference
     */
    public RevisionInternal update(Database _db, String docID, Body body, boolean deleting, boolean allowConflict, Status outStatus) {
        boolean isLocalDoc = docID != null && docID.startsWith(("_local"));
        String prevRevID = null;

        if(!deleting) {
            Boolean deletingBoolean = (Boolean)body.getPropertyForKey("_deleted");
            deleting = (deletingBoolean != null && deletingBoolean.booleanValue());
            if(docID == null) {
                if(isLocalDoc) {
                    outStatus.setCode(Status.METHOD_NOT_ALLOWED);
                    return null;
                }
                // POST's doc ID may come from the _id field of the JSON body, else generate a random one.
                docID = (String)body.getPropertyForKey("_id");
                if(docID == null) {
                    if(deleting) {
                        outStatus.setCode(Status.BAD_REQUEST);
                        return null;
                    }
                    docID = Database.generateDocumentId();
                }
            }
            // PUT's revision ID comes from the JSON body.
            prevRevID = (String)body.getPropertyForKey("_rev");
        } else {
            // DELETE's revision ID comes from the ?rev= query param
            prevRevID = getQuery("rev");
        }

        // A backup source of revision ID is an If-Match header:
        if(prevRevID == null) {
            prevRevID = getRevIDFromIfMatchHeader();
        }

        RevisionInternal rev = new RevisionInternal(docID, null, deleting, db);
        rev.setBody(body);

        RevisionInternal result = null;
        try {
            if(isLocalDoc) {
                result = _db.putLocalRevision(rev, prevRevID);
            } else {
                result = _db.putRevision(rev, prevRevID, allowConflict);
            }
            if(deleting){
                outStatus.setCode(Status.OK);
            } else{
                outStatus.setCode(Status.CREATED);
            }

        } catch (CouchbaseLiteException e) {
            e.printStackTrace();
            Log.e(Database.TAG, e.toString());
            outStatus.setCode(e.getCBLStatus().getCode());
        }

        return result;
    }

    public Status update(Database _db, String docID, Map<String,Object> bodyDict, boolean deleting) {
        Body body = new Body(bodyDict);
        Status status = new Status();

        if (docID != null && docID.isEmpty() == false) {
            // On PUT/DELETE, get revision ID from either ?rev= query or doc body:
            String revParam = getQuery("rev");
            if (revParam != null && bodyDict != null && bodyDict.size() > 0) {
                String revProp = (String) bodyDict.get("_rev");
                if (revProp == null) {
                    // No _rev property in body, so use ?rev= query param instead:
                    bodyDict.put("_rev", revParam);
                    body = new Body(bodyDict);
                } else if (!revParam.equals(revProp)) {
                    throw new IllegalArgumentException("Mismatch between _rev and rev");
                }
            }
        }

        RevisionInternal rev = update(_db, docID, body, deleting, false, status);
        if(status.isSuccessful()) {
            cacheWithEtag(rev.getRevId());  // set ETag
            if(!deleting) {
                URL url = connection.getURL();
                String urlString = url.toExternalForm();
                if(docID != null) {
                    urlString += "/" + rev.getDocId();
                    try {
                        url = new URL(urlString);
                    } catch (MalformedURLException e) {
                        Log.w("Malformed URL", e);
                    }
                }
                setResponseLocation(url);
            }
            Map<String, Object> result = new HashMap<String, Object>();
            result.put("ok", true);
            result.put("id", rev.getDocId());
            result.put("rev", rev.getRevId());
            connection.setResponseBody(new Body(result));
        }
        return status;
    }

    public Status do_PUT_Document(Database _db, String docID, String _attachmentName) throws CouchbaseLiteException {

        Status status = new Status(Status.CREATED);
        Map<String,Object> bodyDict = getBodyAsDictionary();
        if(bodyDict == null) {
            throw new CouchbaseLiteException(Status.BAD_REQUEST);
        }

        if(getQuery("new_edits") == null || (getQuery("new_edits") != null && (new Boolean(getQuery("new_edits"))))) {
            // Regular PUT
            status = update(_db, docID, bodyDict, false);
        } else {
            // PUT with new_edits=false -- forcible insertion of existing revision:
            Body body = new Body(bodyDict);
            RevisionInternal rev = new RevisionInternal(body, _db);
            if(rev.getRevId() == null || rev.getDocId() == null || !rev.getDocId().equals(docID)) {
                throw new CouchbaseLiteException(Status.BAD_REQUEST);
            }
            List<String> history = Database.parseCouchDBRevisionHistory(body.getProperties());
            db.forceInsert(rev, history, null);
        }
        return status;
    }

    public Status do_DELETE_Document(Database _db, String docID, String _attachmentName) {
        return update(_db, docID, null, true);
    }

    public Status updateAttachment(String attachment, String docID, InputStream contentStream) throws CouchbaseLiteException {
        Status status = new Status(Status.OK);
        String revID = getQuery("rev");
        if(revID == null) {
            revID = getRevIDFromIfMatchHeader();
        }
        RevisionInternal rev = db.updateAttachment(attachment, contentStream, connection.getRequestProperty("content-type"),
                docID, revID);
        Map<String, Object> resultDict = new HashMap<String, Object>();
        resultDict.put("ok", true);
        resultDict.put("id", rev.getDocId());
        resultDict.put("rev", rev.getRevId());
        connection.setResponseBody(new Body(resultDict));
        cacheWithEtag(rev.getRevId());
        if(contentStream != null) {
            setResponseLocation(connection.getURL());
        }
        return status;
    }

    public Status do_PUT_Attachment(Database _db, String docID, String _attachmentName) throws CouchbaseLiteException {
        return updateAttachment(_attachmentName, docID, connection.getRequestInputStream());
    }

    public Status do_DELETE_Attachment(Database _db, String docID, String _attachmentName) throws CouchbaseLiteException {
        return updateAttachment(_attachmentName, docID, null);
    }

    /** VIEW QUERIES: **/

    public View compileView(String viewName, Map<String,Object> viewProps) {
        String language = (String)viewProps.get("language");
        if(language == null) {
            language = "javascript";
        }
        String mapSource = (String)viewProps.get("map");
        if(mapSource == null) {
            return null;
        }
        Mapper mapBlock = View.getCompiler().compileMap(mapSource, language);
        if(mapBlock == null) {
            Log.w(Database.TAG, String.format("View %s has unknown map function: %s", viewName, mapSource));
            return null;
        }
        String reduceSource = (String)viewProps.get("reduce");
        Reducer reduceBlock = null;
        if(reduceSource != null) {
            reduceBlock = View.getCompiler().compileReduce(reduceSource, language);
            if(reduceBlock == null) {
                Log.w(Database.TAG, String.format("View %s has unknown reduce function: %s", viewName, reduceBlock));
                return null;
            }
        }

        View view = db.getView(viewName);
        view.setMapAndReduce(mapBlock, reduceBlock, "1");
        String collation = (String)viewProps.get("collation");
        if("raw".equals(collation)) {
            view.setCollation(TDViewCollation.TDViewCollationRaw);
        }
        return view;
    }

    public Status queryDesignDoc(String designDoc, String viewName, List<Object> keys) throws CouchbaseLiteException {
        String tdViewName = String.format("%s/%s", designDoc, viewName);
        View view = db.getExistingView(tdViewName);
        if(view == null || view.getMap() == null) {
            // No TouchDB view is defined, or it hasn't had a map block assigned;
            // see if there's a CouchDB view definition we can compile:
            RevisionInternal rev = db.getDocumentWithIDAndRev(String.format("_design/%s", designDoc), null, EnumSet.noneOf(TDContentOptions.class));
            if(rev == null) {
                return new Status(Status.NOT_FOUND);
            }
            Map<String,Object> views = (Map<String,Object>)rev.getProperties().get("views");
            Map<String,Object> viewProps = (Map<String,Object>)views.get(viewName);
            if(viewProps == null) {
                return new Status(Status.NOT_FOUND);
            }
            // If there is a CouchDB view, see if it can be compiled from source:
            view = compileView(tdViewName, viewProps);
            if(view == null) {
                return new Status(Status.INTERNAL_SERVER_ERROR);
            }
        }

        QueryOptions options = new QueryOptions();

        //if the view contains a reduce block, it should default to reduce=true
        if(view.getReduce() != null) {
            options.setReduce(true);
        }

        if(!getQueryOptions(options)) {
            return new Status(Status.BAD_REQUEST);
        }
        if(keys != null) {
            options.setKeys(keys);
        }

        view.updateIndex();

        long lastSequenceIndexed = view.getLastSequenceIndexed();

        // Check for conditional GET and set response Etag header:
        if(keys == null) {
            long eTag = options.isIncludeDocs() ? db.getLastSequenceNumber() : lastSequenceIndexed;
            if(cacheWithEtag(String.format("%d", eTag))) {
                return new Status(Status.NOT_MODIFIED);
            }
        }

        // convert from QueryRow -> Map
        List<QueryRow> queryRows = view.queryWithOptions(options);
        List<Map<String,Object>> rows = new ArrayList<Map<String,Object>>();
        for (QueryRow queryRow : queryRows) {
            rows.add(queryRow.asJSONDictionary());
        }

        Map<String,Object> responseBody = new HashMap<String,Object>();
        responseBody.put("rows", rows);
        responseBody.put("total_rows", rows.size());
        responseBody.put("offset", options.getSkip());
        if(options.isUpdateSeq()) {
            responseBody.put("update_seq", lastSequenceIndexed);
        }
        connection.setResponseBody(new Body(responseBody));
        return new Status(Status.OK);
    }

    public Status do_GET_DesignDocument(Database _db, String designDocID, String viewName) throws CouchbaseLiteException {
        return queryDesignDoc(designDocID, viewName, null);
    }

    public Status do_POST_DesignDocument(Database _db, String designDocID, String viewName) throws CouchbaseLiteException {
    	Map<String,Object> bodyDict = getBodyAsDictionary();
    	if(bodyDict == null) {
    		return new Status(Status.BAD_REQUEST);
    	}
    	List<Object> keys = (List<Object>) bodyDict.get("keys");
    	return queryDesignDoc(designDocID, viewName, keys);
    }

    @Override
    public String toString() {
        String url = "Unknown";
        if(connection != null && connection.getURL() != null) {
            url = connection.getURL().toExternalForm();
        }
        return String.format("Router [%s]", url);
    }
}
