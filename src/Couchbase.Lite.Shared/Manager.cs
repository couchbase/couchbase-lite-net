//
// Manager.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Auth;
using Couchbase.Lite.Db;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using ICSharpCode.SharpZipLib.Zip;
using Sharpen;
using Couchbase.Lite.Internal;
using System.Diagnostics;
using Couchbase.Lite.Store;

#if !NET_3_5
using StringEx = System.String;
#else
using Rackspace.Threading;
#endif

namespace Couchbase.Lite
{
    /// <summary>
    /// The top-level object that manages Couchbase Lite <see cref="Couchbase.Lite.Database"/>s.
    /// </summary>
    public sealed class Manager
    {

    #region Constants

        /// <summary>
        /// The version of Couchbase Lite that is running
        /// </summary>
        public static readonly string VersionString;

        internal static readonly string PLATFORM = Platform.Name;

        private const string TAG = "Manager";
        private const string HttpErrorDomain = "CBLHTTP";

        internal const string DatabaseSuffixv0 = ".touchdb";
        internal const string DatabaseSuffixv1 = ".cblite";
        internal const string DatabaseSuffix = ".cblite2";

        // FIXME: Not all of these are valid Windows file chars.
        private const string IllegalCharacters = @"(^[^a-z]+)|[^a-z0-9_\$\(\)/\+\-]+";

    #endregion

    #region Static Members

        /// <summary>
        /// Gets the default options for creating a manager
        /// </summary>
        public static ManagerOptions DefaultOptions { get; private set; }

        /// <summary>
        /// Gets a shared, per-process, instance of <see cref="Couchbase.Lite.Manager"/>.
        /// </summary>
        /// <value>The shared instance.</value>
        // FIXME: SharedInstance lifecycle is undefined, so returning default manager for now.
        public static Manager SharedInstance { 
            get { 
                return sharedManager ?? (sharedManager = new Manager(defaultDirectory, ManagerOptions.Default)); 
            }
            set { 
                sharedManager = value;
            }
        }

        //Methods

        /// <summary>
        /// Determines if the given name is a valid <see cref="Couchbase.Lite.Database"/> name.  
        /// Only the following characters are valid: abcdefghijklmnopqrstuvwxyz0123456789_$()+-/
        /// </summary>
        /// <returns><c>true</c> if the given name is a valid <see cref="Couchbase.Lite.Database"/> name, otherwise <c>false</c>.</returns>
        /// <param name="name">The Database name to validate.</param>
        public static Boolean IsValidDatabaseName(String name) 
        {
            if (name.Length > 0 && name.Length < 240 && ContainsOnlyLegalCharacters(name) && Char.IsLower(name[0])) {
                return true;
            }

            return name.Equals(Replication.REPLICATOR_DATABASE_NAME);
        }

    #endregion
    
    #region Constructors

        static Manager()
        {
            illegalCharactersPattern = new Regex(IllegalCharacters);
            mapper = new ObjectWriter();
            DefaultOptions = ManagerOptions.Default;
            //
            // Note: Environment.SpecialFolder.LocalApplicationData returns null on Azure (and possibly other Windows Server environments)
            // and this is only needed by the default constructor or when accessing the SharedInstanced
            // So, let's only set it only when GetFolderPath returns something and allow the directory to be
            // manually specified via the ctor that accepts a DirectoryInfo
            #if __UNITY__
            string defaultDirectoryPath = Unity.UnityMainThreadScheduler.PersistentDataPath;

            #else
            var defaultDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            #endif
            if (!StringEx.IsNullOrWhiteSpace(defaultDirectoryPath))
            {
                defaultDirectory = new DirectoryInfo(defaultDirectoryPath);
            }


            string gitVersion= String.Empty;
            using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("version")) {
                if(stream != null) {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        gitVersion= reader.ReadToEnd();
                    }
                } else {
                    gitVersion = "No git information";
                }
            }


            var colonPos = gitVersion.IndexOf(':');
            var branchName = "no branch";
            if(colonPos != -1) {
                branchName = gitVersion.Substring(0, colonPos);
                gitVersion = gitVersion.Substring(colonPos + 2);
            }

            #if !OFFICIAL
            VersionString = String.Format(".NET {0}/{1} Unofficial ({2})/{3}", PLATFORM, Platform.Architecture, branchName.Replace('/', '\\'), 
                gitVersion.TrimEnd());
            #else
            VersionString = String.Format(".NET {0}/{1} 1.2/{2}", PLATFORM, Platform.Architecture, gitVersion.TrimEnd());
            #endif

            Log.I(TAG, "Starting Manager version: " + VersionString);
        }

        /// <summary>
        ///  Initializes a Manager that stores Databases in the default directory.
        /// </summary>
        public Manager() : this(defaultDirectory, ManagerOptions.Default) { }

        /// <summary>
        /// Initializes a Manager that stores Databases in the given directory.
        /// </summary>
        /// <param name="directoryFile"><see cref="System.IO.DirectoryInfo"/> object for initializing the Manager object.</param>
        /// <param name="options">Option flags for initialization.</param>
        /// <exception cref="T:System.IO.DirectoryNotFoundException">Thrown when there is an error while accessing or creating the given directory.</exception>
        public Manager(DirectoryInfo directoryFile, ManagerOptions options)
        {
            this.directoryFile = directoryFile;
            _options = options ?? DefaultOptions;
            this.databases = new Dictionary<string, Database>();
            this.replications = new List<Replication>();
            Shared = new SharedState();

            //create the directory, but don't fail if it already exists
            if (!directoryFile.Exists) {
                directoryFile.Create();
                directoryFile.Refresh();
                if (!directoryFile.Exists) {
                    throw new  DirectoryNotFoundException("Unable to create directory " + directoryFile);
                }
            }

            UpgradeOldDatabaseFiles(directoryFile);

            #if __IOS__

            Foundation.NSString protection;
            switch(options.FileProtection & Foundation.NSDataWritingOptions.FileProtectionMask) {
            case Foundation.NSDataWritingOptions.FileProtectionNone:
                protection = Foundation.NSFileManager.FileProtectionNone;
                break;
            case Foundation.NSDataWritingOptions.FileProtectionComplete:
                protection = Foundation.NSFileManager.FileProtectionComplete;
                break;
            case Foundation.NSDataWritingOptions.FileProtectionCompleteUntilFirstUserAuthentication:
                protection = Foundation.NSFileManager.FileProtectionCompleteUntilFirstUserAuthentication;
                break;
            default:
                protection = Foundation.NSFileManager.FileProtectionCompleteUnlessOpen;
                break;
            }

            var attributes = new Foundation.NSDictionary(Foundation.NSFileManager.FileProtectionKey, protection);
            Foundation.NSError error;
            Foundation.NSFileManager.DefaultManager.SetAttributes(attributes, directoryFile.FullName, out error);

            #endif

            var scheduler = options.CallbackScheduler;
            CapturedContext = new TaskFactory(scheduler);
            workExecutor = new TaskFactory(new SingleTaskThreadpoolScheduler());
            Log.D(TAG, "New Manager uses a scheduler with a max concurrency level of {0}".Fmt(workExecutor.Scheduler.MaximumConcurrencyLevel));

            _networkReachabilityManager = new NetworkReachabilityManager();
            _networkReachabilityManager.StartListening();
            StorageType = "SQLite";
        }

    #endregion

    #region Instance Members
        //Properties
        /// <summary>
        /// Gets the directory where the <see cref="Couchbase.Lite.Manager"/> stores <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>The directory.</value>
        public String Directory { get { return directoryFile.FullName; } }

        /// <summary>
        /// Default storage type for newly created databases.
        /// There are two options, "SQLite" (the default) or "ForestDB".
        /// </summary>
        public string StorageType { get; set; }

        /// <summary>
        /// Gets the names of all existing <see cref="Couchbase.Lite.Database"/>s.
        /// </summary>
        /// <value>All database names.</value>
        public IEnumerable<String> AllDatabaseNames 
        { 
            get 
            { 
                var databaseFiles = directoryFile.GetDirectories("*" + Manager.DatabaseSuffix);
                var result = new List<String>();
                foreach (var databaseFile in databaseFiles) {
                    var path = Path.GetFileNameWithoutExtension(databaseFile.FullName);
                    var replaced = path.Replace('.', '/');
                    replaced = replaced.Replace(':', '/'); //For backwards compatibility
                    result.Add(replaced);
                }

                result.Sort();
                return new ReadOnlyCollection<String>(result);
            }
        }

        /// <summary>
        /// Returns all the databases that are open by this manager.
        /// </summary>
        /// <returns>All the databases that are open by this manager.</returns>
        public ICollection<Database> AllOpenDatabases()
        {
            return databases.Values;
        }

        //Methods
        /// <summary>
        /// Releases all resources used by the <see cref="Couchbase.Lite.Manager"/> and closes all its <see cref="Couchbase.Lite.Database"/>s.
        /// </summary>
        public void Close() 
        {
            _networkReachabilityManager.StopListening();
            foreach (var database in databases.Values.ToArray()) {
                var replicators = database.AllReplications;

                if (replicators != null) {
                    foreach (var replicator in replicators) {
                        replicator.Stop();
                    }
                }

                database.Close();
            }

            databases.Clear();
            Log.D(TAG, "Manager is Closed");
        }

        /// <summary>
        /// Returns the database with the given name. If the database is not yet open, the options given
        /// will be applied; if it's already open, the options are ignored.
        /// Multiple calls with the same name will return the same CBLDatabase instance.
        /// </summary>
        /// <param name="name">The name of the database. May NOT contain capital letters!</param>
        /// <param name="options">ptions to use when opening, such as the encryption key; if nil, a default
        /// set of options will be used.</param>
        /// <returns>The opened database, or null if it doesn't exist and options.Create is false</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an error occurs opening
        /// the database</exception>
        public Database OpenDatabase(string name, DatabaseOptions options)
        {
            if (name == null) {
                throw new ArgumentNullException("name");
            }

            if (options == null) {
                options = new DatabaseOptions();
            }

            var db = GetDatabase(name, !options.Create);
            if (db != null && !db.IsOpen) {
                db.OpenWithOptions(options);
                Shared.SetValue("encryptionKey", "", name, options.EncryptionKey);
            }

            return db;
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Database"/> with the given name.  If the <see cref="Couchbase.Lite.Database"/> does not already exist, it is created.
        /// </summary>
        /// <returns>The database.</returns>
        /// <param name="name">Name.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an issue occurs while gettings or createing the <see cref="Couchbase.Lite.Database"/>.</exception>
        public Database GetDatabase(String name) 
        {
            var options = DefaultOptionsFor(name);
            options.Create = true;
            return OpenDatabase(name, options);
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Database"/> with the given name if it exists, otherwise null.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Database"/> with the given name if it exists, otherwise null.</returns>
        /// <param name="name">The name of the Database to get.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an issue occurs while getting the <see cref="Couchbase.Lite.Database"/>.</exception>
        public Database GetExistingDatabase(string name)
        {
            var options = DefaultOptionsFor(name);
            return OpenDatabase(name, options);
        }

        /// <summary>Replaces or installs a database from a file.</summary>
        /// <remarks>
        /// Replaces or installs a database from a file.
        /// This is primarily used to install a canned database on first launch of an app, in which case
        /// you should first check .exists to avoid replacing the database if it exists already. The
        /// canned database would have been copied into your app at build time.
        /// </remarks>
        /// <param name="name">The name of the target Database to replace or create.</param>
        /// <param name="databaseStream">Stream on the source Database file.</param>
        /// <param name="attachmentStreams">
        /// Map of the associated source Attachments, or null if there are no attachments.
        /// The Map key is the name of the attachment, the map value is an InputStream for
        /// the attachment contents. If you wish to control the order that the attachments
        /// will be processed, use a LinkedHashMap, SortedMap or similar and the iteration order
        /// will be honoured.
        /// </param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        [Obsolete("This will only work for v1 (.cblite) databases")]
        public void ReplaceDatabase(string name, Stream databaseStream, IDictionary<string, Stream> attachmentStreams)
        {
            try {
                var tempPath = Path.Combine(Path.GetTempPath(), name + "-upgrade");
                if(System.IO.Directory.Exists(tempPath)) {
                    System.IO.Directory.Delete(tempPath, true);
                }

                System.IO.Directory.CreateDirectory(tempPath);
                var fileStream = File.OpenWrite(Path.Combine(tempPath, name + DatabaseSuffixv1));
                databaseStream.CopyTo(fileStream);
                fileStream.Dispose();
                var success = UpgradeDatabase(new FileInfo(Path.Combine(tempPath, name + DatabaseSuffixv1)));
                if(!success) {
                    Log.E(TAG, "Unable to replace database (upgrade failed)");
                    System.IO.Directory.Delete(tempPath, true);
                    return;
                }
                   
                System.IO.Directory.Delete(tempPath, true);

                var db = GetDatabase(name, true);
                var attachmentsPath = db.AttachmentStorePath;
                if(attachmentStreams != null) {
                    StreamUtils.CopyStreamsToFolder(attachmentStreams, new FilePath(attachmentsPath));
                }
            } catch (Exception e) {
                Log.E(Database.TAG, string.Empty, e);
                throw new CouchbaseLiteException("Error upgrading database", e) { Code = StatusCode.Exception };
            }
        }

        /// <summary>
        /// Replaces or installs a database from a zipped DB folder structure
        /// </summary>
        /// <param name="name">The name of the target Database to replace or create.</param>
        /// <param name="compressedStream">The zip stream containing all of the files required by the DB.</param>
        /// <param name="autoRename">Whether or not to automatically rename the db inside of the zip file.
        /// If false, the database name must match the name parameter or an exception is thrown</param>
        /// <remarks>
        /// The zip stream must be from a regular PKZip structure compressed with Deflate (*nix command
        /// line zip will produce this)
        /// </remarks>
        public void ReplaceDatabase(string name, Stream compressedStream, bool autoRename)
        {
            var zipInfo = GetDbNameAndExtFromZip(compressedStream);
            var zipDbName = zipInfo.Item1;
            var extension = zipInfo.Item2;
            bool namesMatch = name.Equals(zipDbName);
            if (!namesMatch && !autoRename) {
                throw new ArgumentException("Names of db file in zip and name passed to function differ", "compressedStream");
            }

            var tempPath = Path.Combine(Path.GetTempPath(), name + "-upgrade");
            if (System.IO.Directory.Exists(tempPath)) {
                System.IO.Directory.Delete(tempPath, true);
            }

            System.IO.Directory.CreateDirectory(tempPath);

            ZipEntry entry = null;
            using (var zipStream = new ZipInputStream(compressedStream) { IsStreamOwner = false }) {
                while ((entry = zipStream.GetNextEntry()) != null) {
                    string entryName = null;
                    if (namesMatch) {
                        entryName = entry.Name;
                    } else {
                        entryName = entry.Name.Replace(zipDbName, name);
                    }

                    if (entry.IsDirectory) {
                        System.IO.Directory.CreateDirectory(Path.Combine(tempPath, entryName));
                        continue;
                    }


                    var path = Path.Combine(tempPath, entryName);
                    if (File.Exists(path)) {
                        File.Delete(path);
                    }

                    using (var destStream = File.OpenWrite(path)) {
                        if (entry.CompressedSize != 0) {
                            zipStream.CopyTo(destStream);
                        }
                    }
                }
            }

            var db = GetDatabase(name, false);
            if (db.Exists()) {
                System.IO.Directory.Move(db.DbDirectory, db.DbDirectory + "-old");
            }

            var success = true;
            if (extension == Manager.DatabaseSuffixv0 || extension == Manager.DatabaseSuffixv1) {
                success = UpgradeDatabase(new FileInfo(Path.Combine(tempPath, name + extension)));
            } else {
                var folderPath = Path.Combine(tempPath, Path.GetFileName(db.DbDirectory));
                System.IO.Directory.Move(folderPath, db.DbDirectory);
            }

            if (!success) {
                db.Delete();
                System.IO.Directory.Move(db.DbDirectory + "-old", db.DbDirectory);
                Log.E(TAG, "Unable to replace database (upgrade failed)");
                System.IO.Directory.Delete(tempPath, true);
                return;
            }

            if (System.IO.Directory.Exists(db.DbDirectory + "-old")) {
                System.IO.Directory.Delete(db.DbDirectory + "-old", true);
            }

            System.IO.Directory.Delete(tempPath, true);
        }

    #endregion
    
    #region Non-public Members

        // Static Fields
        private static readonly ObjectWriter mapper;
        private static          Manager sharedManager;
        private static readonly DirectoryInfo defaultDirectory;
        private static readonly Regex illegalCharactersPattern;

        // Static Methods
        internal static ObjectWriter GetObjectMapper ()
        {
            return mapper;
        }

        private static bool ContainsOnlyLegalCharacters(string databaseName)
        {
            var result = !illegalCharactersPattern.IsMatch(databaseName);
            return result;
        }

        // Instance Fields
        private readonly ManagerOptions _options;
        private readonly DirectoryInfo directoryFile;
        private readonly IDictionary<String, Database> databases;
        private readonly List<Replication> replications;
        private readonly NetworkReachabilityManager _networkReachabilityManager;
        internal readonly TaskFactory workExecutor;

        // Instance Properties
        internal TaskFactory CapturedContext { get; private set; }
        internal IHttpClientFactory DefaultHttpClientFactory { get; set; }
        internal INetworkReachabilityManager NetworkReachabilityManager { get { return _networkReachabilityManager; } }
        internal SharedState Shared { get; private set; }

        // Instance Methods
        internal Database GetDatabase(string name, bool mustExist)
        {
            var db = databases.Get(name);
            if (db == null) {
                if (!IsValidDatabaseName(name)) {
                    throw new ArgumentException("Invalid database name: " + name);
                }

                if (_options.ReadOnly) {
                    mustExist = true;
                }

                var path = PathForName(name);
                db = new Database(path, name, this, _options.ReadOnly);
                if (mustExist && !db.Exists()) {
                    Log.W(TAG, "mustExist is true and db ({0}) does not exist", name);
                    return null;
                }

                db.Name = name;
                databases.Put(name, db);
                Shared.OpenedDatabase(db);
            }

            return db;
        }

        /// <summary>
        /// Removes the given database from the manager, along with any associated replications
        /// </summary>
        /// <param name="database">The database to remove</param>
        public void ForgetDatabase (Database database)
        {
            // remove from cached list of dbs
            databases.Remove(database.Name);
            if (Shared != null) {
                Shared.ClosedDatabase(database);
            }

            // remove from list of replications
            // TODO: should there be something that actually stops the replication(s) first?
            if (replications.Count == 0) {
                return;
            }

            var i = replications.Count;
            for (; i >= 0; i--) {
                var replication = replications[i];
                if (replication.LocalDatabase == database) {
                    replications.RemoveAt(i);
                }
            }
        }

        private bool ContainsExtension(string name)
        {
            return DatabaseUpgraderFactory.ALL_KNOWN_PREFIXES.Any(x => name.Contains(x));
        }

        private Tuple<string, string> GetDbNameAndExtFromZip(Stream compressedStream) {
            string dbName = null;
            string extension = null;
            using (var zipStream = new ZipInputStream(compressedStream) { IsStreamOwner = false }) {
                ZipEntry next;
                while ((next = zipStream.GetNextEntry()) != null) {
                    var fileInfo = next.IsDirectory ? 
                        (FileSystemInfo)new DirectoryInfo(next.Name) : (FileSystemInfo)new FileInfo(next.Name);
                    if (ContainsExtension(fileInfo.Name)) {
                        dbName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        extension = Path.GetExtension(fileInfo.Name);
                        break;
                    }
                }
            }

            if (dbName == null) {
                throw new ArgumentException("No database found in zip file", "compressedStream");
            }

            compressedStream.Seek(0, SeekOrigin.Begin);
            return Tuple.Create(dbName, extension);
        }

        private void UpgradeOldDatabaseFiles(DirectoryInfo dirInfo)
        {
            var files = dirInfo.GetFiles("*" + DatabaseSuffixv1);
            foreach (var file in files) {
                UpgradeDatabase(file);
            }
        }

        private bool UpgradeDatabase(FileInfo path)
        {
            var previousStorageType = StorageType;
            try {
                StorageType = "SQLite";
                var oldFilename = path.FullName;
                var newFilename = Path.ChangeExtension(oldFilename, DatabaseSuffix);
                var newFile = new DirectoryInfo(newFilename);

                if (!oldFilename.Equals(newFilename) && newFile.Exists) {
                    var msg = String.Format("Cannot move {0} to {1}, {2} already exists", oldFilename, newFilename, newFilename);
                    Log.W(Database.TAG, msg);
                    return false;
                }

                var name = Path.GetFileNameWithoutExtension(Path.Combine(path.DirectoryName, newFilename));
                var db = GetDatabase(name, false);
                if (db == null) {
                    Log.W(TAG, "Upgrade failed for {0} (Creating new DB failed)", path.Name);
                    return false;
                }
                db.Dispose();

                var upgrader = DatabaseUpgraderFactory.CreateUpgrader(db, oldFilename);
                try {
                    upgrader.Import();
                } catch(CouchbaseLiteException e) {
                    Log.W(TAG, "Upgrade failed for {0} (Status {1})", path.Name, e.CBLStatus);
                    upgrader.Backout();
                    return false;
                }

                Log.D(TAG, "...Success!");
                return true;
            } finally {
                StorageType = previousStorageType;
            }
        }



        internal Replication ReplicationWithProperties(IDictionary<string, object> properties)
        {
            // Extract the parameters from the JSON request body:
            // http://wiki.apache.org/couchdb/Replication

            bool push, createTarget;
            var results = new Dictionary<string, object>() {
                { "database", null },
                { "remote", null },
                { "headers", null },
                { "authorizer", null }
            };

            Status result = ParseReplicationProperties(properties, out push, out createTarget, results);
            if (result.IsError) {
                throw new CouchbaseLiteException(result.Code);
            }

            object continuousObj = properties.Get("continuous");
            bool continuous = false;
            if (continuousObj is bool) {
                continuous = (bool)continuousObj;
            }

            var scheduler = new SingleTaskThreadpoolScheduler();
            Replication rep = null;
            if (push) {
                rep = new Pusher((Database)results["database"], (Uri)results["remote"], continuous, new TaskFactory(scheduler));
            } else {
                rep = new Puller((Database)results["database"], (Uri)results["remote"], continuous, new TaskFactory(scheduler));
            }

            rep.Filter = properties.Get("filter") as string;
            rep.FilterParams = properties.Get("query_params") as IDictionary<string, object>;
            rep.DocIds = properties.Get("doc_ids") as IEnumerable<string>;
            rep.RequestHeaders = results.Get("headers") as IDictionary<string, object>;
            rep.Authenticator = results.Get("authorizer") as IAuthenticator;
            if (push) {
                ((Pusher)rep).CreateTarget = createTarget;
            }

            var db = (Database)results["database"];

            // If this is a duplicate, reuse an existing replicator:
            var existing = db.ActiveReplicators.FirstOrDefault(x => x.LocalDatabase == rep.LocalDatabase
                && x.RemoteUrl == rep.RemoteUrl && x.IsPull == rep.IsPull &&
                x.RemoteCheckpointDocID().Equals(rep.RemoteCheckpointDocID()));


            return existing ?? rep;
        }

        private Status ParseReplicationProperties(IDictionary<string, object> properties, out bool isPush, out bool createTarget,
            IDictionary<string, object> results)
        {
            // http://wiki.apache.org/couchdb/Replication
            isPush = false;
            createTarget = false;

            var sourceDict = ParseSourceOrTarget(properties, "source");
            var targetDict = ParseSourceOrTarget(properties, "target");
            var source = sourceDict.Get("url") as string;
            var target = targetDict.Get("url") as string;
            if (source == null || target == null) {
                return new Status(StatusCode.BadRequest);
            }

            createTarget = properties.Get("create_target") is bool && (bool)properties.Get("create_target");

            IDictionary<string, object> remoteDict = null;
            bool targetIsLocal = Manager.IsValidDatabaseName(target);
            if (Manager.IsValidDatabaseName(source)) {
                //Push replication
                if (targetIsLocal) {
                    // This is a local-to-local replication. Turn the remote into a full URL to keep the
                    // replicator happy:
                    Database targetDb;
                    if (createTarget) {
                        targetDb = Manager.SharedInstance.GetDatabase(target);
                    } else {
                        targetDb = Manager.SharedInstance.GetExistingDatabase(target);
                    }

                    if (targetDb == null) {
                        return new Status(StatusCode.BadRequest);
                    }

                    targetDict["url"] = "http://localhost:20000" + targetDb.DbDirectory;
                }

                remoteDict = targetDict;
                if (results.ContainsKey("database")) {
                    results["database"] = GetExistingDatabase(source);
                }

                isPush = true;
            } else if (targetIsLocal) {
                //Pull replication
                remoteDict = sourceDict;
                if (results.ContainsKey("database")) {
                    Database db;
                    if (createTarget) {
                        db = GetDatabase(target);
                        if (db == null) {
                            return new Status(StatusCode.DbError);
                        } 
                    } else {
                        db = GetExistingDatabase(target);
                    }
                    results["database"] = db;
                }
            } else {
                return new Status(StatusCode.BadId);
            }

            Uri remote = new Uri(remoteDict["url"] as string);
            if (!remote.Scheme.Equals("http") && !remote.Scheme.Equals("https") && !remote.Scheme.Equals("cbl")) {
                return new Status(StatusCode.BadRequest);
            }

            var database = results.Get("database");
            if (database == null) {
                return new Status(StatusCode.NotFound);
            }

            if (results.ContainsKey("remote")) {
                results["remote"] = remote;
            }

            if (results.ContainsKey("headers")) {
                results["headers"] = remoteDict.Get("headers");
            }

            if (results.ContainsKey("authorizer")) {
                var auth = remoteDict.Get("auth") as IDictionary<string, object>;
                if (auth != null) {
                    //var oauth = auth["oauth"] as IDictionary<string, object>;
                    var persona = auth.Get("persona") as IDictionary<string, object>;
                    var facebook = auth.Get("facebook") as IDictionary<string, object>;
                    //TODO: OAuth
                    /*if (oauth != null) {
                        string consumerKey = oauth.Get("consumer_key") as string;
                        string consumerSec = oauth.Get("consumer_secret") as string;
                        string token = oauth.Get("token") as string;
                        string tokenSec = oauth.Get("token_secret") as string;
                        string sigMethod = oauth.Get("signature_method") as string;
                        results["authorizer"] = 
                    }*/
                    if (persona != null) {
                        string email = persona.Get("email") as string;
                        results["authorizer"] = new PersonaAuthorizer(email);
                    } else if (facebook != null) {
                        string email = facebook.Get("email") as string;
                        results["authorizer"] = new FacebookAuthorizer(email);
                    } else {
                        Log.W(TAG, "Invalid authorizer settings {0}", auth);
                    }
                }
            }

            // Can't specify both a filter and doc IDs
            if (properties.ContainsKey("filter") && properties.ContainsKey("doc_ids")) {
                return new Status(StatusCode.BadRequest);
            }

            return new Status(StatusCode.Ok);
        }

        private static IDictionary<string, object> ParseSourceOrTarget(IDictionary<string, object> properties, string key)
        {
            object val = properties.Get(key);
            if (val is IDictionary<string, object>) {
                return (IDictionary<string, object>)val;
            }

            if (val is string) {
                return new Dictionary<string, object> {
                    { "url", val }
                };
            }

            return new Dictionary<string, object>();
        }


        internal Replication ReplicationWithDatabase (Database database, Uri url, bool push, bool create, bool start)
        {
            foreach (var replication in replications)
            {
                if (replication.LocalDatabase == database 
                    && replication.RemoteUrl.Equals(url) 
                    && replication.IsPull == !push)
                {
                    return replication;
                }
            }
            if (!create)
            {
                return null;
            }

            var replicator = push 
                ? (Replication)new Pusher (database, url, true, new TaskFactory(new SingleTaskThreadpoolScheduler()))
                : (Replication)new Puller (database, url, true, new TaskFactory(new SingleTaskThreadpoolScheduler()));

            replications.Add(replicator);
            if (start)
            {
                replicator.Start();
            }
            return replicator;
        }

        private string PathForName(string name)
        {
            if (String.IsNullOrEmpty(name) || illegalCharactersPattern.IsMatch(name)) {
                throw new ArgumentException(String.Format("\"{0}\" is empty or contains illegal characters", name), "name");
            }

            //Backwards compatibility
            var oldStyleName = name.Replace('/', ':');
            var fileName = oldStyleName + Manager.DatabaseSuffix;
            var result = Path.Combine(directoryFile.FullName, fileName);
            if (System.IO.Directory.Exists(result)) {
                return result;
            }
            
            name = name.Replace('/', '.');
            fileName = name + Manager.DatabaseSuffix;
            result = Path.Combine(directoryFile.FullName, fileName);
            return result;
        }

        // Concurrency Management
        internal Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> action) 
        {
            return RunAsync(action, CancellationToken.None);
        }

        internal Task<Boolean> RunAsync(String databaseName, Func<Database, Boolean> action) 
        {
            var db = OpenDatabase(databaseName, new DatabaseOptions { Create = true });
            return RunAsync<Boolean>(() => action (db));
        }

        internal Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> action, CancellationToken token) 
        {
            var task = token == CancellationToken.None 
                   ? workExecutor.StartNew<QueryEnumerator>(action) 
                    : workExecutor.StartNew<QueryEnumerator>(action, token);

            return task;
        }

        internal Task RunAsync(RunAsyncDelegate action, Database database)
        {
            return RunAsync(()=>{ action(database); });
        }

        internal Task RunAsync(Action action)
        {
            var task = workExecutor.StartNew(action);
            return task;
        }

        internal Task RunAsync(Action action, CancellationToken token)
        {
            var task = token == CancellationToken.None ?
                workExecutor.StartNew(action) :
                    workExecutor.StartNew(action, token);
            return task;
        }

        internal Task<T> RunAsync<T>(Func<T> action)
        {
            return workExecutor.StartNew(action);
        }

        internal Task<T> RunAsync<T>(Func<T> action, CancellationToken token)
        {
            var task = token == CancellationToken.None 
                ? workExecutor.StartNew(action) 
                  : workExecutor.StartNew(action, token);
            return task;
        }

        internal DatabaseOptions DefaultOptionsFor(string dbName)
        {
            var options = new DatabaseOptions();
            var encryptionKey = default(SymmetricKey);
            if (Shared.TryGetValue("encryptionKey", "", dbName, out encryptionKey)) {
                options.EncryptionKey = encryptionKey;
            }

            return options;
        }

    #endregion

        #region Overrides
        #pragma warning disable 1591

        public override string ToString()
        {
            return String.Format("[Manager] {0}", Directory);
        }

        #pragma warning restore 1591
        #endregion
    }

}

