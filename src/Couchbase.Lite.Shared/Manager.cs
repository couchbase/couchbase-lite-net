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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using System.Threading.Tasks;
using Couchbase.Lite.Util;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using System.Net.NetworkInformation;

namespace Couchbase.Lite
{
    /// <summary>
    /// The top-level object that manages Couchbase Lite <see cref="Couchbase.Lite.Database"/>s.
    /// </summary>
    public sealed class Manager
    {

    #region Constants

        const string VersionString = "1.0.4";
        const string Tag = "Manager";

        /// <summary>
        /// The error domain used for HTTP status codes.
        /// </summary>
        const string HttpErrorDomain = "CBLHTTP";

        internal const string DatabaseSuffixOld = ".touchdb";

        internal const string DatabaseSuffix = ".cblite";

        // FIXME: Not all of these are valid Windows file chars.
        const string IllegalCharacters = @"(^[^a-z]+)|[^a-z0-9_\$\(\)/\+\-]+";

    #endregion

    #region Static Members
        //Properties
        public static ManagerOptions DefaultOptions { get; private set; }

        /// <summary>
        /// Gets a shared, per-process, instance of <see cref="Couchbase.Lite.Manager"/>.
        /// </summary>
        /// <value>The shared instance.</value>
        // FIXME: SharedInstance lifecycle is undefined, so returning default manager for now.
        public static Manager SharedInstance { get { return sharedManager ?? (sharedManager = new Manager(defaultDirectory, ManagerOptions.Default)); } }

        //Methods

        /// <summary>
        /// Determines if the given name is a valid <see cref="Couchbase.Lite.Database"/> name.  
        /// Only the following characters are valid: abcdefghijklmnopqrstuvwxyz0123456789_$()+-/
        /// </summary>
        /// <returns><c>true</c> if the given name is a valid <see cref="Couchbase.Lite.Database"/> name, otherwise <c>false</c>.</returns>
        /// <param name="name">The Database name to validate.</param>
        public static Boolean IsValidDatabaseName(String name) 
        {
            if (name.Length > 0 && name.Length < 240 && ContainsOnlyLegalCharacters(name) && Char.IsLower(name[0]))
            {
                return true;
            }
            return name.Equals(Replication.ReplicatorDatabaseName);
        }

        public static bool IsValidDocumentId(string docId)
        {
            // http://wiki.apache.org/couchdb/HTTP_Document_API#Documents
            if (String.IsNullOrEmpty(docId)) {
                return false;
            }

            if (docId[0] == '_') {
                return docId.StartsWith("_design/");
            }

            return true;
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
            var defaultDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!String.IsNullOrWhiteSpace(defaultDirectoryPath))
            {
                defaultDirectory = new DirectoryInfo(defaultDirectoryPath);
            }
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
            Log.I(Tag, "Starting Manager version: " + VersionString);

            this.directoryFile = directoryFile;
            this.options = options ?? DefaultOptions;
            this.databases = new Dictionary<string, Database>();
            this.replications = new List<Replication>();

            //create the directory, but don't fail if it already exists
            if (!directoryFile.Exists)
            {
                directoryFile.Create();
                directoryFile.Refresh();
                if (!directoryFile.Exists)
                {
                    throw new  DirectoryNotFoundException("Unable to create directory " + directoryFile);
                }
            }

            UpgradeOldDatabaseFiles(directoryFile);

            var scheduler = options.CallbackScheduler;
            CapturedContext = new TaskFactory(scheduler);
            workExecutor = new TaskFactory(new SingleTaskThreadpoolScheduler());
            Log.D(Tag, "New Manager uses a scheduler with a max concurrency level of {0}".Fmt(workExecutor.Scheduler.MaximumConcurrencyLevel));

            this.NetworkReachabilityManager = new NetworkReachabilityManager();

            SharedCookieStore = new CookieStore(this.directoryFile.FullName);
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
        /// Gets the names of all existing <see cref="Couchbase.Lite.Database"/>s.
        /// </summary>
        /// <value>All database names.</value>
        public IEnumerable<String> AllDatabaseNames 
        { 
            get 
            { 
                var databaseFiles = directoryFile.EnumerateFiles("*" + Manager.DatabaseSuffix, SearchOption.AllDirectories);
                var result = new List<String>();
                foreach (var databaseFile in databaseFiles)
                {
                    var path = Path.GetFileNameWithoutExtension(databaseFile.FullName);
                    var replaced = path.Replace(':', '/');
                    result.AddItem(replaced);
                }
                result.Sort();
                return new ReadOnlyCollection<String>(result);
            }
        }

        //Methods
        /// <summary>
        /// Releases all resources used by the <see cref="Couchbase.Lite.Manager"/> and closes all its <see cref="Couchbase.Lite.Database"/>s.
        /// </summary>
        public void Close() 
        {
            Log.I(Tag, "Closing " + this);

            foreach (var database in databases.Values)
            {
                var replicators = database.AllReplications;

                if (replicators != null)
                {
                    foreach (var replicator in replicators)
                    {
                        replicator.Stop();
                    }
                }

                database.Close();
            }

            databases.Clear();

            Log.I(Tag, "Manager is Closed");
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Database"/> with the given name.  If the <see cref="Couchbase.Lite.Database"/> does not already exist, it is created.
        /// </summary>
        /// <returns>The database.</returns>
        /// <param name="name">Name.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an issue occurs while gettings or createing the <see cref="Couchbase.Lite.Database"/>.</exception>
        public Database GetDatabase(String name) 
        {
            var db = GetDatabaseWithoutOpening(name, false);
            if (db != null)
            {
                var opened = db.Open();
                if (!opened)
                {
                    return null;
                }
            }
            return db;
        }

        /// <summary>
        /// Returns the <see cref="Couchbase.Lite.Database"/> with the given name if it exists, otherwise null.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Database"/> with the given name if it exists, otherwise null.</returns>
        /// <param name="name">The name of the Database to get.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an issue occurs while getting the <see cref="Couchbase.Lite.Database"/>.</exception>
        public Database GetExistingDatabase(String name)
        {
            var db = GetDatabaseWithoutOpening(name, mustExist: true);
            if (db != null)
            {
                db.Open();
            }
            return db;
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
        public void ReplaceDatabase(String name, Stream databaseStream, IDictionary<String, Stream> attachmentStreams)
        {
            try {
                var database = GetDatabaseWithoutOpening (name, false);
                var dstAttachmentsPath = database.AttachmentStorePath;

                var destStream = File.OpenWrite(database.Path);
                databaseStream.CopyTo(destStream);
                destStream.Dispose();

                if (File.Exists(dstAttachmentsPath)) 
                {
                    System.IO.Directory.Delete (dstAttachmentsPath, true);
                }
                System.IO.Directory.CreateDirectory(dstAttachmentsPath);

                var attachmentsFile = new FilePath(dstAttachmentsPath);

                if (attachmentStreams != null) {
                    StreamUtils.CopyStreamsToFolder(attachmentStreams, attachmentsFile);
                }
                database.Open();
                database.ReplaceUUIDs ();
            } catch (Exception e) {
                Log.E(Database.Tag, string.Empty, e);
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
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
        private readonly ManagerOptions options;
        private readonly DirectoryInfo directoryFile;
        private readonly IDictionary<String, Database> databases;
        private readonly List<Replication> replications;
        internal readonly TaskFactory workExecutor;

        // Instance Properties
        internal TaskFactory CapturedContext { get; private set; }
        internal IHttpClientFactory DefaultHttpClientFactory { get; set; }
        internal INetworkReachabilityManager NetworkReachabilityManager { get ; private set; }
        internal CookieStore SharedCookieStore { get; set; } 


        // Instance Methods
        internal Database GetDatabaseWithoutOpening(String name, Boolean mustExist)
        {
            var db = databases.Get(name);
            if (db == null)
            {
                if (!IsValidDatabaseName(name))
                {
                    throw new ArgumentException("Invalid database name: " + name);
                }

                if (options.ReadOnly)
                {
                    mustExist = true;
                }
                var path = PathForName(name);
                if (path == null)
                {
                    return null;
                }
                db = new Database(path, this);
                if (mustExist && !db.Exists())
                {
                    var msg = string.Format("mustExist is true and db ({0}) does not exist", name);
                    Log.W(Database.Tag, msg);
                    return null;
                }
                db.Name = name;
                databases.Put(name, db);
            }
            return db;
        }

        public void ForgetDatabase (Database database)
        {
            // remove from cached list of dbs
            databases.Remove(database.Name);

            // remove from list of replications
            // TODO: should there be something that actually stops the replication(s) first?
            if (replications.Count == 0)
            {
                return;
            }

            var i = replications.Count;
            for (; i >= 0; i--) 
            {
                var replication = replications[i];
                if (replication.LocalDatabase == database) 
                {
                    replications.RemoveAt(i);
                }
            }
        }

        private void UpgradeOldDatabaseFiles(DirectoryInfo dirInfo)
        {
            var files = dirInfo.EnumerateFiles("*" + DatabaseSuffixOld, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var oldFilename = file.Name;
                var newFilename = String.Concat(Path.GetFileNameWithoutExtension(oldFilename), DatabaseSuffix);
                var newFile = new FileInfo(Path.Combine (dirInfo.FullName, newFilename));

                if (newFile.Exists)
                {
                    var msg = String.Format("Cannot rename {0} to {1}, {2} already exists", oldFilename, newFilename, newFilename);
                    Log.W(Database.Tag, msg);
                    continue;
                }

                try
                {
                    // Workaround : using CopyTo() and Delete() as file.MoveTo throws IOException with Sharing Violation Error.
                    //file.MoveTo (newFile.FullName);
                    file.CopyTo(newFile.FullName);
                    file.Delete();
                } catch (Exception ex) {
                    var msg = string.Format("Unable to rename {0} to {1}", oldFilename, newFilename);
                    var error = new InvalidOperationException(msg, ex);
                    Log.E(Database.Tag, msg, error);
                    throw error;
                }
            }
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

            replications.AddItem(replicator);
            if (start)
            {
                replicator.Start();
            }
            return replicator;
        }

        private string PathForName(string name)
        {
            if (String.IsNullOrEmpty (name) || illegalCharactersPattern.IsMatch (name))
            {
                return null;
            }

            name = name.Replace('/', ':');

            var fileName = name + Manager.DatabaseSuffix;
            var result = Path.Combine(directoryFile.FullName, fileName);
            return result;
        }

        // Concurrency Management
        internal Task<QueryEnumerator> RunAsync(Func<QueryEnumerator> action) 
        {
            return RunAsync(action, CancellationToken.None);
        }

        internal Task<Boolean> RunAsync(String databaseName, Func<Database, Boolean> action) 
        {
            var db = GetDatabase(databaseName);
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

    #endregion
    }

}

