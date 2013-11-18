package com.couchbase.cblite;

import com.couchbase.cblite.auth.CBLAuthorizer;
import com.couchbase.cblite.auth.CBLFacebookAuthorizer;
import com.couchbase.cblite.auth.CBLPersonaAuthorizer;
import com.couchbase.cblite.internal.InterfaceAudience;
import com.couchbase.cblite.replicator.CBLPuller;
import com.couchbase.cblite.replicator.CBLPusher;
import com.couchbase.cblite.replicator.CBLReplicator;
import com.couchbase.cblite.support.FileDirUtils;
import com.couchbase.cblite.support.HttpClientFactory;
import com.couchbase.cblite.util.Log;

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
import java.util.List;
import java.util.Map;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Top-level CouchbaseLite object; manages a collection of databases as a CouchDB server does.
 */
public class CBLManager {

    private static final ObjectMapper mapper = new ObjectMapper();
    public static final String DATABASE_SUFFIX_OLD = ".touchdb";
    public static final String DATABASE_SUFFIX = ".cblite";
    public static final CBLManagerOptions DEFAULT_OPTIONS = new CBLManagerOptions(false, false);
    private static CBLManager sharedInstance;
    public static final String LEGAL_CHARACTERS = "[^a-z]{1,}[^a-z0-9_$()/+-]*$";


    private CBLManagerOptions options;
    private File directoryFile;
    private Map<String, CBLDatabase> databases;
    private List<CBLReplicator> replications;
    private ScheduledExecutorService workExecutor;
    private HttpClientFactory defaultHttpClientFactory;

    public static ObjectMapper getObjectMapper() {
        return mapper;
    }

    @InterfaceAudience.Public
    public CBLManager(File directoryFile) {
        this(directoryFile, DEFAULT_OPTIONS);
    }

    @InterfaceAudience.Public
    public CBLManager(File directoryFile, CBLManagerOptions options) {
        this.directoryFile = directoryFile;
        this.options = (options != null) ? options : DEFAULT_OPTIONS;
        this.databases = new HashMap<String, CBLDatabase>();
        this.replications = new ArrayList<CBLReplicator>();

        //create the directory, but don't fail if it already exists
        if(!directoryFile.exists()) {
            boolean result = directoryFile.mkdir();
            if(!result) {
                throw new RuntimeException("Unable to create directory " + directoryFile);
            }
        }

        upgradeOldDatabaseFiles(directoryFile);
        workExecutor = Executors.newSingleThreadScheduledExecutor();

    }

    @InterfaceAudience.Public
    public static CBLManager getSharedInstance() {
        return sharedInstance;
    }

    @InterfaceAudience.Public
    public synchronized static CBLManager createSharedInstance(File directoryFile, CBLManagerOptions options) {
        if (sharedInstance == null) {
            sharedInstance = new CBLManager(directoryFile, options);
        }
        return sharedInstance;
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
        return databaseName.equals(CBLReplicator.REPLICATOR_DATABASE_NAME);
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
                if(filename.endsWith(CBLManager.DATABASE_SUFFIX)) {
                    return true;
                }
                return false;
            }
        });
        List<String> result = new ArrayList<String>();
        for (String databaseFile : databaseFiles) {
            String trimmed = databaseFile.substring(0, databaseFile.length() - CBLManager.DATABASE_SUFFIX.length());
            String replaced = trimmed.replace(':', '/');
            result.add(replaced);
        }
        Collections.sort(result);
        return Collections.unmodifiableList(result);
    }

    /**
     * Releases all resources used by the CBLManager instance and closes all its databases.
     */
    @InterfaceAudience.Public
    public void close() {
        Log.i(CBLDatabase.TAG, "Closing " + this);
        for (CBLDatabase database : databases.values()) {
            List<CBLReplicator> replicators = database.getAllReplications();
            if (replicators != null) {
                for (CBLReplicator replicator : replicators) {
                    replicator.stop();
                }
            }
            database.close();
        }
        databases.clear();
        Log.i(CBLDatabase.TAG, "Closed " + this);
    }


    /**
     * Returns the database with the given name, or creates it if it doesn't exist.
     * Multiple calls with the same name will return the same CBLDatabase instance.
     */
    @InterfaceAudience.Public
    public CBLDatabase getDatabase(String name) {
        CBLDatabase db = databases.get(name);
        if(db == null) {
            if (!isValidDatabaseName(name)) {
                throw new IllegalArgumentException("Invalid database name: " + name);
            }
            String path = pathForName(name);
            if(path == null) {
                return null;
            }
            db = new CBLDatabase(path, this);
            db.setName(name);
            databases.put(name, db);
        }
        return db;
    }

    /**
     * Returns the database with the given name, or null if it doesn't exist.
     * Multiple calls with the same name will return the same CBLDatabase instance.
     */
    @InterfaceAudience.Public
    public CBLDatabase getExistingDatabase(String name) {
        return databases.get(name);
    }

    /**
     * Replaces or installs a database from a file.
     *
     * This is primarily used to install a canned database on first launch of an app, in which case
     * you should first check .exists to avoid replacing the database if it exists already. The
     * canned database would have been copied into your app bundle at build time.
     *
     * @param databaseName  The name of the database to replace.
     * @param databasePath  Path of the database file that should replace it.
     * @param attachmentsPath  Path of the associated attachments directory, or nil if there are no attachments.
     **/
    @InterfaceAudience.Public
    public void replaceDatabase(String databaseName, String databasePath, String attachmentsPath) throws IOException {
        CBLDatabase database = getDatabase(databaseName);
        String dstAttachmentsPath = database.getAttachmentStorePath();
        File sourceFile = new File(databasePath);
        File destFile = new File(database.getPath());
        FileDirUtils.copyFile(sourceFile, destFile);
        File attachmentsFile = new File(dstAttachmentsPath);
        FileDirUtils.deleteRecursive(attachmentsFile);
        attachmentsFile.mkdirs();
        if(attachmentsPath != null) {
            FileDirUtils.copyFolder(new File(attachmentsPath), attachmentsFile);
        }
        database.replaceUUIDs();
    }

    private static boolean containsOnlyLegalCharacters(String databaseName) {
        Pattern p = Pattern.compile("^[abcdefghijklmnopqrstuvwxyz0123456789_$()+-/]+$");
        Matcher matcher = p.matcher(databaseName);
        return matcher.matches();
    }

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
                Log.w(CBLDatabase.TAG, msg);
                continue;
            }
            boolean ok = file.renameTo(newFile);
            if (!ok) {
                String msg = String.format("Unable to rename %s to %s", oldFilename, newFilename);
                throw new IllegalStateException(msg);
            }
        }
    }

    private String filenameWithNewExtension(String oldFilename, String oldExtension, String newExtension) {
        String oldExtensionRegex = String.format("%s$",oldExtension);
        return oldFilename.replaceAll(oldExtensionRegex, newExtension);
    }







    public Collection<CBLDatabase> allOpenDatabases() {
        return databases.values();
    }



    /**
     * Asynchronously dispatches a callback to run on a background thread. The callback will be passed
     * CBLDatabase instance.  There is not currently a known reason to use it, it may not make
     * sense on the Android API, but it was added for the purpose of having a consistent API with iOS.
     */
    public Future runAsync(String databaseName, final CBLDatabaseAsyncFunction function) {

        final CBLDatabase database = getDatabase(databaseName);
        return runAsync(new Runnable() {
            @Override
            public void run() {
                function.performFunction(database);
            }
        });

    }

    Future runAsync(Runnable runnable) {
        return workExecutor.submit(runnable);
    }

    private String pathForName(String name) {
        if((name == null) || (name.length() == 0) || Pattern.matches(LEGAL_CHARACTERS, name)) {
            return null;
        }
        name = name.replace('/', ':');
        String result = directoryFile.getPath() + File.separator + name + CBLManager.DATABASE_SUFFIX;
        return result;
    }

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


    @InterfaceAudience.Private
    CBLReplicator replicationWithDatabase(CBLDatabase db, URL remote, boolean push, boolean create, boolean start) {
        for (CBLReplicator replicator : replications) {
            if (replicator.getDb() == db && replicator.getRemote().equals(remote) && replicator.isPush() == push) {
                return replicator;
            }

        }
        if (!create) {
            return null;
        }

        CBLReplicator replicator = null;
        if (push) {
            replicator = new CBLPusher(db, remote, true, getWorkExecutor());
        }
        else {
            replicator = new CBLPuller(db, remote, true, getWorkExecutor());
        }

        replications.add(replicator);
        if (start) {
            replicator.start();
        }

        return replicator;
    }



    @InterfaceAudience.Private
    public CBLReplicator getReplicator(Map<String,Object> properties) throws CBLiteException {

        CBLAuthorizer authorizer = null;
        CBLReplicator repl = null;
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
            throw new CBLiteException("source and target are both null", new CBLStatus(CBLStatus.BAD_REQUEST));
        }

        boolean push = false;

        CBLDatabase db = getExistingDatabase(source);
        String remoteStr = null;
        if(db != null) {
            remoteStr = target;
            push = true;
            remoteMap = targetMap;
        } else {
            remoteStr = source;
            if(createTarget && !cancel) {
                db = getDatabase(target);
                if(!db.open()) {
                    throw new CBLiteException("cannot open database: " + db, new CBLStatus(CBLStatus.INTERNAL_SERVER_ERROR));
                }
            } else {
                db = getExistingDatabase(target);
            }
            if(db == null) {
                throw new CBLiteException("database is null", new CBLStatus(CBLStatus.NOT_FOUND));
            }
            remoteMap = sourceMap;
        }


        Map<String, Object> authMap = (Map<String, Object>) remoteMap.get("auth");
        if (authMap != null) {

            Map<String, Object> persona = (Map<String, Object>) authMap.get("persona");
            if (persona != null) {
                String email = (String) persona.get("email");
                authorizer = new CBLPersonaAuthorizer(email);
            }
            Map<String, Object> facebook = (Map<String, Object>) authMap.get("facebook");
            if (facebook != null) {
                String email = (String) facebook.get("email");
                authorizer = new CBLFacebookAuthorizer(email);
            }

        }

        try {
            remote = new URL(remoteStr);
        } catch (MalformedURLException e) {
            throw new CBLiteException("malformed remote url: " + remoteStr, new CBLStatus(CBLStatus.BAD_REQUEST));
        }
        if(remote == null || !remote.getProtocol().startsWith("http")) {
            throw new CBLiteException("remote URL is null or non-http: " + remoteStr, new CBLStatus(CBLStatus.BAD_REQUEST));
        }


        if(!cancel) {
            repl = db.getReplicator(remote, getDefaultHttpClientFactory(), push, continuous, getWorkExecutor());
            if(repl == null) {
                throw new CBLiteException("unable to create replicator with remote: " + remote, new CBLStatus(CBLStatus.INTERNAL_SERVER_ERROR));
            }

            if (authorizer != null) {
                repl.setAuthorizer(authorizer);
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


        } else {
            // Cancel replication:
            repl = db.getActiveReplicator(remote, push);
            if(repl == null) {
                throw new CBLiteException("unable to lookup replicator with remote: " + remote, new CBLStatus(CBLStatus.NOT_FOUND));
            }
        }

        return repl;
    }

    public ScheduledExecutorService getWorkExecutor() {
        return workExecutor;
    }

    public HttpClientFactory getDefaultHttpClientFactory() {
        return defaultHttpClientFactory;
    }

    public void setDefaultHttpClientFactory(
            HttpClientFactory defaultHttpClientFactory) {
        this.defaultHttpClientFactory = defaultHttpClientFactory;
    }
}

