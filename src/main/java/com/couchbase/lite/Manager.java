package com.couchbase.lite;

import com.couchbase.lite.auth.Authorizer;
import com.couchbase.lite.auth.FacebookAuthorizer;
import com.couchbase.lite.auth.PersonaAuthorizer;
import com.couchbase.lite.internal.InterfaceAudience;
import com.couchbase.lite.replicator.Puller;
import com.couchbase.lite.replicator.Pusher;
import com.couchbase.lite.replicator.Replication;
import com.couchbase.lite.support.FileDirUtils;
import com.couchbase.lite.support.HttpClientFactory;
import com.couchbase.lite.util.Log;

import org.codehaus.jackson.map.ObjectMapper;

import java.io.File;
import java.io.FilenameFilter;
import java.io.IOException;
import java.net.MalformedURLException;
import java.net.URL;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Collections;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.ScheduledExecutorService;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Top-level CouchbaseLite object; manages a collection of databases as a CouchDB server does.
 */
public class Manager {

    public static final String VERSION =  "1.0.0-beta2";

    /**
     * @exclude
     */
    public static final String HTTP_ERROR_DOMAIN =  "CBLHTTP";

    /**
     * @exclude
     */
    public static final String DATABASE_SUFFIX_OLD = ".touchdb";

    /**
     * @exclude
     */
    public static final String DATABASE_SUFFIX = ".cblite";

    /**
     * @exclude
     */
    public static final ManagerOptions DEFAULT_OPTIONS = new ManagerOptions();

    /**
     * @exclude
     */
    public static final String LEGAL_CHARACTERS = "[^a-z]{1,}[^a-z0-9_$()/+-]*$";

    private static final ObjectMapper mapper = new ObjectMapper();
    private ManagerOptions options;
    private File directoryFile;
    private Map<String, Database> databases;
    private List<Replication> replications;
    private ScheduledExecutorService workExecutor;
    private HttpClientFactory defaultHttpClientFactory;

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public static ObjectMapper getObjectMapper() {
        return mapper;
    }

    /**
     * Constructor
     * @throws UnsupportedOperationException - not currently supported
     * @exclude
     */
    @InterfaceAudience.Public
    public Manager() {
        final String detailMessage = "Parameterless constructor is not a valid API call on Android. " +
                " Pure java version coming soon.";
        throw new UnsupportedOperationException(detailMessage);
    }

    /**
     * Constructor
     *
     * @throws java.lang.SecurityException - Runtime exception that can be thrown by File.mkdirs()
     */
    @InterfaceAudience.Public
    public Manager(File directoryFile, ManagerOptions options) throws IOException {
        this.directoryFile = directoryFile;
        this.options = (options != null) ? options : DEFAULT_OPTIONS;
        this.databases = new HashMap<String, Database>();
        this.replications = new ArrayList<Replication>();

        directoryFile.mkdirs();
        if (!directoryFile.isDirectory()) {
            throw new IOException(String.format("Unable to create directory for: %s", directoryFile));
        }

        upgradeOldDatabaseFiles(directoryFile);
        workExecutor = Executors.newSingleThreadScheduledExecutor();

    }

    /**
     * Get shared instance
     * @throws UnsupportedOperationException - not currently supported
     * @exclude
     */
    @InterfaceAudience.Public
    public static Manager getSharedInstance() {
        final String detailMessage = "getSharedInstance() is not a valid API call on Android. " +
                " Pure java version coming soon";
        throw new UnsupportedOperationException(detailMessage);
    }

    /**
     * Returns YES if the given name is a valid database name.
     * (Only the characters in "abcdefghijklmnopqrstuvwxyz0123456789_$()+-/" are allowed.)
     */
    @InterfaceAudience.Public
    public static boolean isValidDatabaseName(String databaseName) {
        if (databaseName.length() > 0 && databaseName.length() < 240 &&
                containsOnlyLegalCharacters(databaseName) &&
                Character.isLowerCase(databaseName.charAt(0))) {
            return true;
        }
        return databaseName.equals(Replication.REPLICATOR_DATABASE_NAME);
    }

    /**
     * The root directory of this manager (as specified at initialization time.)
     */
    @InterfaceAudience.Public
    public String getDirectory() {
        return directoryFile.getAbsolutePath();
    }

    /**
     * An array of the names of all existing databases.
     */
    @InterfaceAudience.Public
    public List<String> getAllDatabaseNames() {
        String[] databaseFiles = directoryFile.list(new FilenameFilter() {

            @Override
            public boolean accept(File dir, String filename) {
                if(filename.endsWith(Manager.DATABASE_SUFFIX)) {
                    return true;
                }
                return false;
            }
        });
        List<String> result = new ArrayList<String>();
        for (String databaseFile : databaseFiles) {
            String trimmed = databaseFile.substring(0, databaseFile.length() - Manager.DATABASE_SUFFIX.length());
            String replaced = trimmed.replace(':', '/');
            result.add(replaced);
        }
        Collections.sort(result);
        return Collections.unmodifiableList(result);
    }

    /**
     * Releases all resources used by the Manager instance and closes all its databases.
     */
    @InterfaceAudience.Public
    public void close() {
        Log.i(Database.TAG, "Closing " + this);
        for (Database database : databases.values()) {
            List<Replication> replicators = database.getAllReplications();
            if (replicators != null) {
                for (Replication replicator : replicators) {
                    replicator.stop();
                }
            }
            database.close();
        }
        databases.clear();
        Log.i(Database.TAG, "Closed " + this);
    }


    /**
     * Returns the database with the given name, or creates it if it doesn't exist.
     * Multiple calls with the same name will return the same Database instance.
     */
    @InterfaceAudience.Public
    public Database getDatabase(String name) throws CouchbaseLiteException {
        boolean mustExist = false;
        Database db = getDatabaseWithoutOpening(name, mustExist);
        if (db != null) {
            db.open();
        }
        return db;
    }

    /**
     * Returns the database with the given name, or null if it doesn't exist.
     * Multiple calls with the same name will return the same Database instance.
     */
    @InterfaceAudience.Public
    public Database getExistingDatabase(String name) throws CouchbaseLiteException {
        boolean mustExist = true;
        Database db = getDatabaseWithoutOpening(name, mustExist);
        if (db != null) {
            db.open();
        }
        return db;
    }


    /**
     * Replaces or installs a database from a file.
     *
     * This is primarily used to install a canned database on first launch of an app, in which case
     * you should first check .exists to avoid replacing the database if it exists already. The
     * canned database would have been copied into your app bundle at build time.
     *
     * @param databaseName  The name of the target Database to replace or create.
     * @param databaseFile  Path of the source Database file.
     * @param attachmentsDirectory  Path of the associated Attachments directory, or null if there are no attachments.
     **/
    @InterfaceAudience.Public
    public void replaceDatabase(String databaseName, File databaseFile, File attachmentsDirectory) throws CouchbaseLiteException {
        try {
            Database database = getDatabase(databaseName);
            String dstAttachmentsPath = database.getAttachmentStorePath();
            File destFile = new File(database.getPath());
            FileDirUtils.copyFile(databaseFile, destFile);
            File attachmentsFile = new File(dstAttachmentsPath);
            FileDirUtils.deleteRecursive(attachmentsFile);
            attachmentsFile.mkdirs();
            if(attachmentsDirectory != null) {
                FileDirUtils.copyFolder(attachmentsDirectory, attachmentsFile);
            }
            database.replaceUUIDs();
        } catch (IOException e) {
            Log.e(Database.TAG, "", e);
            throw new CouchbaseLiteException(Status.INTERNAL_SERVER_ERROR);
        }
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public HttpClientFactory getDefaultHttpClientFactory() {
        return defaultHttpClientFactory;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public void setDefaultHttpClientFactory(
            HttpClientFactory defaultHttpClientFactory) {
        this.defaultHttpClientFactory = defaultHttpClientFactory;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    private static boolean containsOnlyLegalCharacters(String databaseName) {
        Pattern p = Pattern.compile("^[abcdefghijklmnopqrstuvwxyz0123456789_$()+-/]+$");
        Matcher matcher = p.matcher(databaseName);
        return matcher.matches();
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    private void upgradeOldDatabaseFiles(File directory) {
        File[] files = directory.listFiles(new FilenameFilter() {
            @Override
            public boolean accept(File file, String name) {
                return name.endsWith(DATABASE_SUFFIX_OLD);
            }
        });

        for (File file : files) {
            String oldFilename = file.getName();
            String newFilename = filenameWithNewExtension(oldFilename, DATABASE_SUFFIX_OLD, DATABASE_SUFFIX);
            File newFile = new File(directory, newFilename);
            if (newFile.exists()) {
                String msg = String.format("Cannot rename %s to %s, %s already exists", oldFilename, newFilename, newFilename);
                Log.w(Database.TAG, msg);
                continue;
            }
            boolean ok = file.renameTo(newFile);
            if (!ok) {
                String msg = String.format("Unable to rename %s to %s", oldFilename, newFilename);
                throw new IllegalStateException(msg);
            }
        }
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    private String filenameWithNewExtension(String oldFilename, String oldExtension, String newExtension) {
        String oldExtensionRegex = String.format("%s$",oldExtension);
        return oldFilename.replaceAll(oldExtensionRegex, newExtension);
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public Collection<Database> allOpenDatabases() {
        return databases.values();
    }


    /**
     * Asynchronously dispatches a callback to run on a background thread. The callback will be passed
     * Database instance.  There is not currently a known reason to use it, it may not make
     * sense on the Android API, but it was added for the purpose of having a consistent API with iOS.
     * @exclude
     */
    @InterfaceAudience.Private
    public Future runAsync(String databaseName, final AsyncTask function) throws CouchbaseLiteException {

        final Database database = getDatabase(databaseName);
        return runAsync(new Runnable() {
            @Override
            public void run() {
                function.run(database);
            }
        });

    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    Future runAsync(Runnable runnable) {
        return workExecutor.submit(runnable);
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    private String pathForName(String name) {
        if((name == null) || (name.length() == 0) || Pattern.matches(LEGAL_CHARACTERS, name)) {
            return null;
        }
        name = name.replace('/', ':');
        String result = directoryFile.getPath() + File.separator + name + Manager.DATABASE_SUFFIX;
        return result;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    private Map<String, Object> parseSourceOrTarget(Map<String,Object> properties, String key) {
        Map<String, Object> result = new HashMap<String, Object>();

        Object value = properties.get(key);

        if (value instanceof String) {
            result.put("url", (String)value);
        }
        else if (value instanceof Map) {
            result = (Map<String, Object>) value;
        }
        return result;

    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    Replication replicationWithDatabase(Database db, URL remote, boolean push, boolean create, boolean start) {
        for (Replication replicator : replications) {
            if (replicator.getLocalDatabase() == db && replicator.getRemoteUrl().equals(remote) && replicator.isPull() == !push) {
                return replicator;
            }

        }
        if (!create) {
            return null;
        }

        Replication replicator = null;
        final boolean continuous = false;

        if (push) {
            replicator = new Pusher(db, remote, continuous, getWorkExecutor());
        }
        else {
            replicator = new Puller(db, remote, continuous, getWorkExecutor());
        }

        replications.add(replicator);
        if (start) {
            replicator.start();
        }

        return replicator;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public Database getDatabaseWithoutOpening(String name, boolean mustExist) {
        Database db = databases.get(name);
        if(db == null) {
            if (!isValidDatabaseName(name)) {
                throw new IllegalArgumentException("Invalid database name: " + name);
            }
            if (options.isReadOnly()) {
                mustExist = true;
            }
            String path = pathForName(name);
            if (path == null) {
                return null;
            }
            db = new Database(path, this);
            if (mustExist && !db.exists()) {
                String msg = String.format("mustExist is true and db (%s) does not exist", name);
                Log.w(Database.TAG, msg);
                return null;
            }
            db.setName(name);
            databases.put(name, db);
        }
        return db;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    /* package */ void forgetDatabase(Database db) {

        // remove from cached list of dbs
        databases.remove(db.getName());

        // remove from list of replications
        // TODO: should there be something that actually stops the replication(s) first?
        Iterator<Replication> replicationIterator = this.replications.iterator();
        while (replicationIterator.hasNext()) {
            Replication replication = replicationIterator.next();
            if (replication.getLocalDatabase().getName().equals(db.getName())) {
                replicationIterator.remove();
            }
        }
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public Replication getReplicator(Map<String,Object> properties) throws CouchbaseLiteException {

        // TODO: in the iOS equivalent of this code, there is: {@"doc_ids", _documentIDs}) - write unit test that detects this bug
        // TODO: ditto for "headers"

        Authorizer authorizer = null;
        Replication repl = null;
        URL remote = null;

        Map<String, Object> remoteMap;

        Map<String, Object> sourceMap = parseSourceOrTarget(properties, "source");
        Map<String, Object> targetMap = parseSourceOrTarget(properties, "target");

        String source = (String)sourceMap.get("url");
        String target = (String)targetMap.get("url");

        Boolean createTargetBoolean = (Boolean)properties.get("create_target");
        boolean createTarget = (createTargetBoolean != null && createTargetBoolean.booleanValue());

        Boolean continuousBoolean = (Boolean)properties.get("continuous");
        boolean continuous = (continuousBoolean != null && continuousBoolean.booleanValue());

        Boolean cancelBoolean = (Boolean)properties.get("cancel");
        boolean cancel = (cancelBoolean != null && cancelBoolean.booleanValue());

        // Map the 'source' and 'target' JSON params to a local database and remote URL:
        if(source == null || target == null) {
            throw new CouchbaseLiteException("source and target are both null", new Status(Status.BAD_REQUEST));
        }

        boolean push = false;
        Database db = null;
        String remoteStr = null;

        if (Manager.isValidDatabaseName(source)) {
            db = getExistingDatabase(source);
            remoteStr = target;
            push = true;
            remoteMap = targetMap;
        } else {
            remoteStr = source;
            if(createTarget && !cancel) {
                boolean mustExist = false;
                db = getDatabaseWithoutOpening(target, mustExist);
                if(!db.open()) {
                    throw new CouchbaseLiteException("cannot open database: " + db, new Status(Status.INTERNAL_SERVER_ERROR));
                }
            } else {
                db = getExistingDatabase(target);
            }
            if(db == null) {
                throw new CouchbaseLiteException("database is null", new Status(Status.NOT_FOUND));
            }
            remoteMap = sourceMap;

        }


        Map<String, Object> authMap = (Map<String, Object>) remoteMap.get("auth");
        if (authMap != null) {

            Map<String, Object> persona = (Map<String, Object>) authMap.get("persona");
            if (persona != null) {
                String email = (String) persona.get("email");
                authorizer = new PersonaAuthorizer(email);
            }
            Map<String, Object> facebook = (Map<String, Object>) authMap.get("facebook");
            if (facebook != null) {
                String email = (String) facebook.get("email");
                authorizer = new FacebookAuthorizer(email);
            }

        }

        try {
            remote = new URL(remoteStr);
        } catch (MalformedURLException e) {
            throw new CouchbaseLiteException("malformed remote url: " + remoteStr, new Status(Status.BAD_REQUEST));
        }
        if(remote == null || !remote.getProtocol().startsWith("http")) {
            throw new CouchbaseLiteException("remote URL is null or non-http: " + remoteStr, new Status(Status.BAD_REQUEST));
        }


        if(!cancel) {
            repl = db.getReplicator(remote, getDefaultHttpClientFactory(), push, continuous, getWorkExecutor());
            if(repl == null) {
                throw new CouchbaseLiteException("unable to create replicator with remote: " + remote, new Status(Status.INTERNAL_SERVER_ERROR));
            }

            if (authorizer != null) {
                repl.setAuthorizer(authorizer);
            }

            Map<String, Object> headers = (Map) properties.get("headers");
            if (headers != null && !headers.isEmpty()) {
                repl.setHeaders(headers);
            }

            String filterName = (String)properties.get("filter");
            if(filterName != null) {
                repl.setFilter(filterName);
                Map<String,Object> filterParams = (Map<String,Object>)properties.get("query_params");
                if(filterParams != null) {
                    repl.setFilterParams(filterParams);
                }
            }

            if(push) {
                ((Pusher)repl).setCreateTarget(createTarget);
            }


        } else {
            // Cancel replication:
            repl = db.getActiveReplicator(remote, push);
            if(repl == null) {
                throw new CouchbaseLiteException("unable to lookup replicator with remote: " + remote, new Status(Status.NOT_FOUND));
            }
        }

        return repl;
    }

    /**
     * @exclude
     */
    @InterfaceAudience.Private
    public ScheduledExecutorService getWorkExecutor() {
        return workExecutor;
    }


}

