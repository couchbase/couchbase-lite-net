//
// Database.cs
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
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite 
{

    /// <summary>
    /// A Couchbase Lite Database.
    /// </summary>
    public partial class Database 
    {

        Document debugDoc;
    
    #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Couchbase.Lite.Database"/> class.
        /// </summary>
        /// <param name="path">Path.</param>
        /// <param name="manager">Manager.</param>
        internal Database(String path, Manager manager)
        {
            Debug.Assert((path.StartsWith("/", StringComparison.InvariantCultureIgnoreCase)));

            //path must be absolute
            Path = path;
            Name = FileDirUtils.GetDatabaseNameFromPath(path);
            Manager = manager;
            DocumentCache = new LruCache<string, Document>(MaxDocCacheSize);
            UnsavedRevisionDocumentCache = new Dictionary<string, WeakReference>();
 
            // TODO: Make Synchronized ICollection
            ActiveReplicators = new HashSet<Replication>();
            AllReplicators = new HashSet<Replication>();

            ChangesToNotify = new AList<DocumentChange>();

            StartTime = DateTime.UtcNow.ToMillisecondsSinceEpoch ();

            MaxRevTreeDepth = DefaultMaxRevs;
        }


    #endregion

    #region Static Members
        //Properties

        /// <summary>
        /// Gets or sets an object that can compile source code into <see cref="FilterDelegate"/>.
        /// </summary>
        /// <value>The filter compiler object.</value>
        public static CompileFilterDelegate FilterCompiler { get; set; }

        // "_local/*" is not a valid document ID. Local docs have their own API and shouldn't get here.
        internal static String GenerateDocumentId()
        {
            return Misc.TDCreateUUID();
        }

        static readonly ICollection<String> KnownSpecialKeys;

        static Database()
        {
            // Length that constitutes a 'big' attachment
            KnownSpecialKeys = new List<String>();
            KnownSpecialKeys.Add("_id");
            KnownSpecialKeys.Add("_rev");
            KnownSpecialKeys.Add("_attachments");
            KnownSpecialKeys.Add("_deleted");
            KnownSpecialKeys.Add("_revisions");
            KnownSpecialKeys.Add("_revs_info");
            KnownSpecialKeys.Add("_conflicts");
            KnownSpecialKeys.Add("_deleted_conflicts");
            KnownSpecialKeys.Add("_local_seq");
        }

    #endregion
    
    #region Instance Members
        //Properties

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> name.
        /// </summary>
        /// <value>The database name.</value>
        public String Name { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Manager"> that owns this <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>The manager object.</value>
        public Manager Manager { get; private set; }

        /// <summary>
        /// Gets the number of <see cref="Couchbase.Lite.Document"> in the <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>The document count.</value>
        /// TODO: Convert this to a standard method call.
        public Int32 DocumentCount 
        {
            get 
            {
                var sql = "SELECT COUNT(DISTINCT doc_id) FROM revs WHERE current=1 AND deleted=0";
                Cursor cursor = null;
                int result = 0;
                try
                {
                    cursor = StorageEngine.RawQuery(sql);
                    if (cursor.MoveToNext())
                    {
                        result = cursor.GetInt(0);
                    }
                }
                catch (SQLException e)
                {   // FIXME: Should we really swallow this exception?
                    Log.E(Tag, "Error getting document count", e);
                }
                finally
                {
                    if (cursor != null)
                    {
                        cursor.Close();
                    }
                }
                return result;
            }
        }
            
        /// <summary>
        /// Gets the latest sequence number used by the <see cref="Couchbase.Lite.Database" />.  Every new <see cref="Couchbase.Lite.Revision" /> is assigned a new sequence 
        /// number, so this property increases monotonically as changes are made to the <see cref="Couchbase.Lite.Database" />. This can be used to 
        /// check whether the <see cref="Couchbase.Lite.Database" /> has changed between two points in time.
        /// </summary>
        /// <value>The last sequence number.</value>
        public Int64 LastSequenceNumber 
        {
            get
            {
                var sql = "SELECT MAX(sequence) FROM revs";
                Cursor cursor = null;
                long result = 0;
                try
                {
                    cursor = StorageEngine.RawQuery(sql);
                    if (cursor.MoveToNext())
                    {
                        result = cursor.GetLong(0);

                        // When there is no rows in revs table, the result is -1 which is different
                        // from the Android platform.
                        if (result < 0) result = 0;
                    }
                }
                catch (SQLException e)
                {   // FIXME: Should we really swallow this exception?
                    Log.E(Tag, "Error getting last sequence", e);
                }
                finally
                {
                    if (cursor != null)
                    {
                        cursor.Close();
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Gets all the running <see cref="Couchbase.Lite.Replication" />s 
        /// for this <see cref="Couchbase.Lite.Database" />.  
        /// This includes all continuous <see cref="Couchbase.Lite.Replication" />s and 
        /// any non-continuous <see cref="Couchbase.Lite.Replication" />s that has been started 
        /// and are still running.
        /// </summary>
        /// <value>All replications.</value>
        public IEnumerable<Replication> AllReplications { get { return AllReplicators; } }

        //Methods

        /// <summary>
        /// Compacts the <see cref="Couchbase.Lite.Database" /> file by purging non-current 
        /// <see cref="Couchbase.Lite.Revision" />s and deleting unused <see cref="Couchbase.Lite.Attachment" />s.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">thrown if an issue occurs while 
        /// compacting the <see cref="Couchbase.Lite.Database" /></exception>
        public void Compact()
        {
            // Can't delete any rows because that would lose revision tree history.
            // But we can remove the JSON of non-current revisions, which is most of the space.
            try
            {
                Log.V(Tag, "Deleting JSON of old revisions...");
                PruneRevsToMaxDepth(0);
                Log.V(Tag, "Deleting JSON of old revisions...");

                var args = new ContentValues();
                args["json"] = null;
                StorageEngine.Update("revs", args, "current=0 AND json IS NOT NULL", null);
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error compacting", e);
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }

            Log.V(Tag, "Deleting old attachments...");
            var result = GarbageCollectAttachments();
            if (!result.IsSuccessful())
            {
                throw new CouchbaseLiteException(result.GetCode());
            }

            try
            {
                Log.V(Tag, "Vacuuming SQLite sqliteDb..." + result);
                StorageEngine.ExecSQL("VACUUM");
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error vacuuming sqliteDb", e);
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.Database" />.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while deleting the <see cref="Couchbase.Lite.Database" /></exception>
        public void Delete()
        {
            if (open)
            {
                if (!Close())
                {
                    throw new CouchbaseLiteException("The database was open, and could not be closed", 
                        StatusCode.InternalServerError);
                }
            }

            Manager.ForgetDatabase(this);
            if (!Exists())
            {
                return;
            }

            var file = new FilePath(Path);
            var attachmentsFile = new FilePath(AttachmentStorePath);

            var deleteStatus = file.Delete();
            if (!deleteStatus)
            {
                Log.V(Tag, String.Format("Error deleting the SQLite database file at {0}", file.GetAbsolutePath()));
            }

            //recursively delete attachments path
            var deletedAttachmentsPath = true;
            try {
                Directory.Delete (attachmentsFile.GetPath (), true);
            } catch (Exception ex) {
                Log.V(Tag, "Error deleting the attachments directory.", ex);
                deletedAttachmentsPath = false;
            }

            if (!deleteStatus)
            {
                throw new CouchbaseLiteException("Was not able to delete the database file", StatusCode.InternalServerError);
            }

            if (!deletedAttachmentsPath)
            {
                throw new CouchbaseLiteException("Was not able to delete the attachments files", StatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Gets or creates the <see cref="Couchbase.Lite.Document" /> with the given id.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Document" />.</returns>
        /// <param name="id">The id of the Document to get or create.</param>
        public Document GetDocument(String id) 
        { 
            if (String.IsNullOrWhiteSpace (id)) {
                return null;
            }

            var unsavedDoc = UnsavedRevisionDocumentCache.Get(id);
            Document doc = unsavedDoc != null ? (Document)unsavedDoc.Target : DocumentCache.Get(id);
            if (doc == null)
            {
                doc = new Document(this, id);
                DocumentCache[id] = doc;
                UnsavedRevisionDocumentCache[id] = new WeakReference(doc);
            }

            return doc;
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document" /> with the given id, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Document" /> with the given id, or null if it does not exist.</returns>
        /// <param name="id">The id of the Document to get.</param>
        public Document GetExistingDocument(String id) 
        { 
            if (String.IsNullOrWhiteSpace (id)) {
                return null;
            }
            var revisionInternal = GetDocumentWithIDAndRev(id, null, EnumSet.NoneOf<TDContentOptions>());
            return revisionInternal == null ? null : GetDocument (id);
        }

        /// <summary>
        /// Creates a <see cref="Couchbase.Lite.Document" /> with a unique id.
        /// </summary>
        /// <returns>A document with a unique id.</returns>
        public Document CreateDocument()
        { 
            return GetDocument(Misc.TDCreateUUID());
        }

        /// <summary>
        /// Gets the local document with the given id, or null if it does not exist.
        /// </summary>
        /// <returns>The existing local document.</returns>
        /// <param name="id">Identifier.</param>
        public IDictionary<String, Object> GetExistingLocalDocument(String id) 
        {
            var revInt = GetLocalDocument(MakeLocalDocumentId(id), null);
            if (revInt == null)
            {
                return null;
            }
            return revInt.GetProperties();
        }

        /// <summary>
        /// Sets the contents of the local <see cref="Couchbase.Lite.Document" /> with the given id.  If <param name="properties"/> is null, the 
        /// <see cref="Couchbase.Lite.Document" /> is deleted.
        /// </summary>
        /// <param name="id">The id of the local document whos contents to set.</param>
        /// <param name="properties">The contents to set for the local document.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if an issue occurs 
        /// while setting the contents of the local document.</exception>
        public void PutLocalDocument(String id, IDictionary<String, Object> properties) 
        { 
            // TODO: the iOS implementation wraps this in a transaction, this should do the same.
            id = MakeLocalDocumentId(id);
            var prevRev = GetLocalDocument(id, null);

            if (prevRev == null && properties == null)
            {
                return;
            }

            var deleted = false || properties == null;
            var rev = new RevisionInternal(id, null, deleted, this);

            if (properties != null)
            {
                rev.SetProperties(properties);
            }

            var success = false;

            if (prevRev == null)
            {
                success = PutLocalRevision(rev, null) != null;
            }
            else
            {
                success = PutLocalRevision(rev, prevRev.GetRevId()) != null;
            }

            if (!success) 
            {
                throw new CouchbaseLiteException("Unable to put local revision with id " + id);
            }
        }

        /// <summary>
        /// Deletes the local <see cref="Couchbase.Lite.Document" /> with the given id.
        /// </summary>
        /// <returns><c>true</c>, if local <see cref="Couchbase.Lite.Document" /> was deleted, <c>false</c> otherwise.</returns>
        /// <param name="id">Identifier.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if there is an issue occurs while deleting the local document.</exception>
        public Boolean DeleteLocalDocument(String id) 
        {
            id = MakeLocalDocumentId(id);

            var prevRev = GetLocalDocument(id, null);
            if (prevRev == null)
            {
                return false;
            }

            try 
            {
                DeleteLocalDocument(id, prevRev.GetRevId());
            }
            catch (Exception ex)
            {
                Log.D(Tag, "Cannot delete a local document id " + id, ex);
                return false;
            }

            return true;
        } 

        /// <summary>
        /// Creates a <see cref="Couchbase.Lite.Query" /> that matches all <see cref="Couchbase.Lite.Document" />s in the <see cref="Couchbase.Lite.Database" />.
        /// </summary>
        /// <returns>Returns a <see cref="Couchbase.Lite.Query" /> that matches all <see cref="Couchbase.Lite.Document" />s in the <see cref="Couchbase.Lite.Database" />s.</returns>
        public Query CreateAllDocumentsQuery() 
        {
            return new Query(this, (View)null);
        }

        /// <summary>
        /// Gets or creates the <see cref="Couchbase.Lite.View" /> with the given name.  
        /// New <see cref="Couchbase.Lite.View" />s won't be added to the <see cref="Couchbase.Lite.Database" /> 
        /// until a map function is assigned.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.View" /> with the given name.</returns>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.View" /> to get or create.</param>
        public View GetView(String name) 
        {
            View view = null;

            if (views != null)
            {
                view = views.Get(name);
            }

            if (view != null)
            {
                return view;
            }

            return RegisterView(new View(this, name));
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View" /> with the given name, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.View" /> with the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the View to get.</param>
        public View GetExistingView(String name) 
        {
            View view = null;
            if (views != null)
            {
                views.TryGetValue(name, out view);
            }
            if (view != null)
            {
                return view;
            }
            view = new View(this, name);

            return view.Id == 0 ? null : RegisterView(view);
        }

        /// <summary>
        /// Gets the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.
        /// </summary>
        /// <returns>the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the validation delegate to get.</param>
        public ValidateDelegate GetValidation(String name) 
        {
            ValidateDelegate result = null;
            if (Validations != null)
            {
                result = Validations.Get(name);
            }
            return result;
        }

        /// <summary>
        /// Sets the validation delegate for the given name. If delegate is null, 
        /// the validation with the given name is deleted. Before any change 
        /// to the <see cref="Couchbase.Lite.Database"/> is committed, including incoming changes from a pull 
        /// <see cref="Couchbase.Lite.Replication"/>, all of its validation delegates are called and given 
        /// a chance to reject it.
        /// </summary>
        /// <param name="name">The name of the validation delegate to set.</param>
        /// <param name="validationDelegate">The validation delegate to set.</param>
        public void SetValidation(String name, ValidateDelegate validationDelegate)
        {
            if (Validations == null)
                Validations = new Dictionary<string, ValidateDelegate>();

            if (validationDelegate != null)
                Validations[name] = validationDelegate;
            else
                Validations.Remove(name);
        }

        /// <summary>
        /// Returns the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the validation delegate to get.</param>
        public FilterDelegate GetFilter(String name) 
        { 
            FilterDelegate result = null;
            if (Filters != null)
            {
                result = Filters.Get(name);
            }
            if (result == null)
            {
                var filterCompiler = FilterCompiler;
                if (filterCompiler == null)
                {
                    return null;
                }

                var outLanguageList = new AList<string>();
                var sourceCode = GetDesignDocFunction(name, "filters", outLanguageList);

                if (sourceCode == null)
                {
                    return null;
                }

                var language = outLanguageList[0];

                var filter = filterCompiler(sourceCode, language);
                if (filter == null)
                {
                    Log.W(Tag, string.Format("Filter {0} failed to compile", name));
                    return null;
                }

                SetFilter(name, filter);
                return filter;
            }
            return result;
        }

        /// <summary>
        /// Sets the <see cref="ValidateDelegate" /> for the given name. If delegate is null, the filter 
        /// with the given name is deleted. Before a <see cref="Couchbase.Lite.Revision" /> is replicated via a 
        /// push <see cref="Couchbase.Lite.Replication" />, its filter delegate is called and 
        /// given a chance to exclude it from the <see cref="Couchbase.Lite.Replication" />.
        /// </summary>
        /// <param name="name">The name of the filter delegate to set.</param>
        /// <param name="filterDelegate">The filter delegate to set.</param>
        public void SetFilter(String name, FilterDelegate filterDelegate) 
        { 
            if (Filters == null)
            {
                Filters = new Dictionary<String, FilterDelegate>();
            }
            if (filterDelegate != null)
            {
                Filters[name] = filterDelegate;
            }
            else
            {
                Collections.Remove(Filters, name);
            }
        }

        /// <summary>
        /// Runs the <see cref="Couchbase.Lite.RunAsyncDelegate"/> asynchronously.
        /// </summary>
        /// <returns>The async task.</returns>
        /// <param name="runAsyncDelegate">The delegate to run asynchronously.</param>
        public Task RunAsync(RunAsyncDelegate runAsyncDelegate) 
        {
            return Manager
                .RunAsync(runAsyncDelegate, this)
//                .ContinueWith(task=>{
//                    if (task.Status != TaskStatus.RanToCompletion)
//                    throw new CouchbaseLiteException(Tag, task.Exception);
//                }, TaskScheduler.Current); // Dispatch to original scheduler.
                    ;
        }

        /// <summary>
        /// Runs the delegate within a transaction. If the delegate returns false, 
        /// the transaction is rolled back.
        /// </summary>
        /// <returns>True if the transaction was committed, otherwise false.</returns>
        /// <param name="transactionDelegate">The delegate to run within a transaction.</param>
        public Boolean RunInTransaction(RunInTransactionDelegate transactionDelegate)
        {
            var shouldCommit = true;

            BeginTransaction();

            try
            {
                shouldCommit = transactionDelegate();
            }
            catch (Exception e)
            {
                shouldCommit = false;
                Log.E(Tag, e.ToString(), e);
                throw new RuntimeException(e);
            }
            finally
            {
                EndTransaction(shouldCommit);
            }

            return shouldCommit;
        }

            
        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Replication"/> that will push to the target <see cref="Couchbase.Lite.Database"/> at the given url.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Replication"/> that will push to the target <see cref="Couchbase.Lite.Database"/> at the given url.</returns>
        /// <param name="url">The url of the target Database.</param>
        public Replication CreatePushReplication(Uri url)
        {
            var scheduler = new SingleThreadTaskScheduler(); //TaskScheduler.FromCurrentSynchronizationContext();
            return new Pusher(this, url, false, new TaskFactory(scheduler));
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Replication"/> that will pull from the source <see cref="Couchbase.Lite.Database"/> at the given url.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Replication"/> that will pull from the source Database at the given url.</returns>
        /// <param name="url">The url of the source Database.</param>
        public Replication CreatePullReplication(Uri url)
        {
            var scheduler = new SingleThreadTaskScheduler(); //TaskScheduler.FromCurrentSynchronizationContext();
            return new Puller(this, url, false, new TaskFactory(scheduler));
        }

        public override string ToString()
        {
            return GetType().FullName + "[" + Path + "]";
        }

        /// <summary>
        /// Event handler delegate that will be called whenever a <see cref="Couchbase.Lite.Document"/> within the <see cref="Couchbase.Lite.Database"/> changes.
        /// </summary>
        public event EventHandler<DatabaseChangeEventArgs> Changed;

    #endregion
       
    #region Constants
        internal const String Tag = "Database";

        internal const String TagSql = "CBLSQL";
       
        internal const Int32 BigAttachmentLength = 16384;

        const Int32 MaxDocCacheSize = 50;

        const Int32 DefaultMaxRevs = Int32.MaxValue;

        internal readonly String Schema = @"
CREATE TABLE docs ( 
  doc_id INTEGER PRIMARY KEY, 
  docid TEXT UNIQUE NOT NULL); 
CREATE INDEX docs_docid ON docs(docid); 
CREATE TABLE revs ( 
  sequence INTEGER PRIMARY KEY AUTOINCREMENT, 
  doc_id INTEGER NOT NULL REFERENCES docs(doc_id) ON DELETE CASCADE, 
  revid TEXT NOT NULL COLLATE REVID, 
  parent INTEGER REFERENCES revs(sequence) ON DELETE SET NULL, 
  current BOOLEAN, 
  deleted BOOLEAN DEFAULT 0, 
  json BLOB); 
CREATE INDEX revs_by_id ON revs(revid, doc_id); 
CREATE INDEX revs_current ON revs(doc_id, current); 
CREATE INDEX revs_parent ON revs(parent); 
CREATE TABLE localdocs ( 
  docid TEXT UNIQUE NOT NULL, 
  revid TEXT NOT NULL COLLATE REVID, 
  json BLOB); 
CREATE INDEX localdocs_by_docid ON localdocs(docid); 
CREATE TABLE views ( 
  view_id INTEGER PRIMARY KEY, 
  name TEXT UNIQUE NOT NULL,
  version TEXT, 
  lastsequence INTEGER DEFAULT 0); 
CREATE INDEX views_by_name ON views(name); 
CREATE TABLE maps ( 
  view_id INTEGER NOT NULL REFERENCES views(view_id) ON DELETE CASCADE, 
  sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE, 
  key TEXT NOT NULL COLLATE JSON, 
  value TEXT); 
CREATE INDEX maps_keys on maps(view_id, key COLLATE JSON); 
CREATE TABLE attachments ( 
  sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE, 
  filename TEXT NOT NULL, 
  key BLOB NOT NULL, 
  type TEXT, 
  length INTEGER NOT NULL, 
  revpos INTEGER DEFAULT 0); 
CREATE INDEX attachments_by_sequence on attachments(sequence, filename); 
CREATE TABLE replicators ( 
  remote TEXT NOT NULL, 
  push BOOLEAN, 
  last_sequence TEXT, 
  UNIQUE (remote, push)); 
PRAGMA user_version = 3;";

    #endregion

    #region Non-Public Instance Members

        private Boolean                                 open;
        private IDictionary<String, ValidateDelegate>   Validations;
        private IDictionary<String, BlobStoreWriter>    _pendingAttachmentsByDigest;
        private IDictionary<String, View>               views;
        private Int32                                   transactionLevel;
        private IList<DocumentChange>                   ChangesToNotify;
        private Boolean                                 PostingChangeNotifications;

        internal String                                 Path { get; private set; }
        internal ICollection<Replication>               ActiveReplicators { get; set; }
        internal ICollection<Replication>               AllReplicators { get; set; }
        internal ISQLiteStorageEngine                   StorageEngine { get; set; }
        internal LruCache<String, Document>             DocumentCache { get; set; }
        internal IDictionary<String, WeakReference>     UnsavedRevisionDocumentCache { get; set; }

        //TODO: Should thid be a public member?

        /// <summary>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// </summary>
        /// <remarks>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// Smaller values save space, at the expense of making document conflicts somewhat more likely.
        /// </remarks>
        internal Int32                                  MaxRevTreeDepth { get; set; }

        private Int64                                   StartTime { get; set; }
        private IDictionary<String, FilterDelegate>     Filters { get; set; }

        internal RevisionList GetAllRevisionsOfDocumentID (string id, bool onlyCurrent)
        {
            var docNumericId = GetDocNumericID(id);
            if (docNumericId < 0)
            {
                return null;
            }
            else
            {
                if (docNumericId == 0)
                {
                    return new RevisionList();
                }
                else
                {
                    return GetAllRevisionsOfDocumentID(id, docNumericId, onlyCurrent);
                }
            }
        }

        private RevisionList GetAllRevisionsOfDocumentID(string docId, long docNumericID, bool onlyCurrent)
        {
            var sql = onlyCurrent 
                ? "SELECT sequence, revid, deleted FROM revs " + "WHERE doc_id=? AND current ORDER BY sequence DESC"
                : "SELECT sequence, revid, deleted FROM revs " + "WHERE doc_id=? ORDER BY sequence DESC";

            var args = new [] { Convert.ToString (docNumericID) };
            var cursor = StorageEngine.RawQuery(sql, args);

            RevisionList result;
            try
            {
                cursor.MoveToNext();
                result = new RevisionList();
                while (!cursor.IsAfterLast())
                {
                    var rev = new RevisionInternal(docId, cursor.GetString(1), (cursor.GetInt(2) > 0), this);
                    rev.SetSequence(cursor.GetLong(0));
                    result.AddItem(rev);
                    cursor.MoveToNext();
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting all revisions of document", e);
                return null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        private String GetDesignDocFunction(String fnName, String key, ICollection<String> outLanguageList)
        {
            var path = fnName.Split('/');
            if (path.Length != 2)
            {
                return null;
            }

            var docId = string.Format("_design/{0}", path[0]);
            var rev = GetDocumentWithIDAndRev(docId, null, EnumSet.NoneOf<TDContentOptions>());
            if (rev == null)
            {
                return null;
            }

            var outLanguage = (string)rev.GetPropertyForKey("language");
            if (outLanguage != null)
            {
                outLanguageList.AddItem(outLanguage);
            }
            else
            {
                outLanguageList.AddItem("javascript");
            }

            var container = (IDictionary<String, Object>)rev.GetPropertyForKey(key);
            return (string)container.Get(path[1]);
        }

        internal Boolean Exists()
        {
            return new FilePath(Path).Exists();
        }

        internal static string MakeLocalDocumentId(string documentId)
        {
            return string.Format("_local/{0}", documentId);
        }

        internal RevisionInternal PutLocalRevision(RevisionInternal revision, string prevRevID)
        {
            var docID = revision.GetDocId();
            if (!docID.StartsWith ("_local/", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new CouchbaseLiteException(StatusCode.BadRequest);
            }

            if (!revision.IsDeleted())
            {
                // PUT:
                string newRevID;
                var json = EncodeDocumentJSON(revision);

                if (prevRevID != null)
                {
                    var generation = RevisionInternal.GenerationFromRevID(prevRevID);
                    if (generation == 0)
                    {
                        throw new CouchbaseLiteException(StatusCode.BadRequest);
                    }
                    newRevID = Sharpen.Extensions.ToString(++generation) + "-local";

                    var values = new ContentValues();
                    values["revid"] = newRevID;
                    values["json"] = json;

                    var whereArgs = new [] { docID, prevRevID };
                    try
                    {
                        var rowsUpdated = StorageEngine.Update("localdocs", values, "docid=? AND revid=?", whereArgs);
                        if (rowsUpdated == 0)
                        {
                            throw new CouchbaseLiteException(StatusCode.Conflict);
                        }
                    }
                    catch (SQLException e)
                    {
                        throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
                    }
                }
                else
                {
                    newRevID = "1-local";

                    var values = new ContentValues();
                    values["docid"] = docID;
                    values["revid"] = newRevID;
                    values["json"] = json;

                    try
                    {
                        StorageEngine.InsertWithOnConflict("localdocs", null, values, ConflictResolutionStrategy.Ignore);
                    }
                    catch (SQLException e)
                    {
                        throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
                    }
                }
                return revision.CopyWithDocID(docID, newRevID);
            }
            else
            {
                // DELETE:
                DeleteLocalDocument(docID, prevRevID);
                return revision;
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void DeleteLocalDocument(string docID, string revID)
        {
            if (docID == null)
            {
                throw new CouchbaseLiteException(StatusCode.BadRequest);
            }

            if (revID == null)
            {
                // Didn't specify a revision to delete: 404 or a 409, depending
                if (GetLocalDocument(docID, null) != null)
                {
                    throw new CouchbaseLiteException(StatusCode.Conflict);
                }
                else
                {
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }
            }

            var whereArgs = new [] { docID, revID };
            try
            {
                int rowsDeleted = StorageEngine.Delete("localdocs", "docid=? AND revid=?", whereArgs);
                if (rowsDeleted == 0)
                {
                    if (GetLocalDocument(docID, null) != null)
                    {
                        throw new CouchbaseLiteException(StatusCode.Conflict);
                    }
                    else
                    {
                        throw new CouchbaseLiteException(StatusCode.NotFound);
                    }
                }
            }
            catch (SQLException e)
            {
                throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
            }
        }

        internal RevisionInternal GetLocalDocument(string docID, string revID)
        {
            // docID already should contain "_local/" prefix
            RevisionInternal result = null;
            Cursor cursor = null;
            try
            {
                var args = new [] { docID };
                cursor = StorageEngine.RawQuery("SELECT revid, json FROM localdocs WHERE docid=?", CommandBehavior.SequentialAccess, args);

                if (cursor.MoveToNext())
                {
                    var gotRevID = cursor.GetString(0);
                    if (revID != null && (!revID.Equals(gotRevID)))
                    {
                        return null;
                    }

                    var json = cursor.GetBlob(1);
                    IDictionary<string, object> properties = null;
                    try
                    {
                        properties = Manager.GetObjectMapper().ReadValue<IDictionary<String, Object>>(json);
                        properties["_id"] = docID;
                        properties["_rev"] = gotRevID;

                        result = new RevisionInternal(docID, gotRevID, false, this);
                        result.SetProperties(properties);
                    }
                    catch (Exception e)
                    {
                        Log.W(Tag, "Error parsing local doc JSON", e);
                        return null;
                    }
                }
                return result;
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting local document", e);
                return null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        /// <summary>Inserts an already-existing revision replicated from a remote sqliteDb.</summary>
        /// <remarks>
        /// Inserts an already-existing revision replicated from a remote sqliteDb.
        /// It must already have a revision ID. This may create a conflict! The revision's history must be given; ancestor revision IDs that don't already exist locally will create phantom revisions with no content.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void ForceInsert(RevisionInternal rev, IList<string> revHistory, Uri source)
        {
            var inConflict = false;
            var docId = rev.GetDocId();
            var revId = rev.GetRevId();
            if (!IsValidDocumentId(docId) || (revId == null))
            {
                throw new CouchbaseLiteException(StatusCode.BadRequest);
            }
            int historyCount = 0;
            if (revHistory != null)
            {
                historyCount = revHistory.Count;
            }
            if (historyCount == 0)
            {
                revHistory = new AList<string>();
                revHistory.AddItem(revId);
                historyCount = 1;
            }
            else
            {
                if (!revHistory[0].Equals(rev.GetRevId()))
                {
                    throw new CouchbaseLiteException(StatusCode.BadRequest);
                }
            }
            bool success = false;
            BeginTransaction();
            try
            {
                // First look up all locally-known revisions of this document:
                long docNumericID = GetOrInsertDocNumericID(docId);
                RevisionList localRevs = GetAllRevisionsOfDocumentID(docId, docNumericID, false);
                if (localRevs == null)
                {
                    throw new CouchbaseLiteException(StatusCode.InternalServerError);
                }

                IList<bool> outIsDeleted = new AList<bool>();
                IList<bool> outIsConflict = new AList<bool>();
                bool oldWinnerWasDeletion = false;
                string oldWinningRevID = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict
                );
                if (outIsDeleted.Count > 0)
                {
                    oldWinnerWasDeletion = true;
                }
                if (outIsConflict.Count > 0)
                {
                    inConflict = true;
                }

                // Walk through the remote history in chronological order, matching each revision ID to
                // a local revision. When the list diverges, start creating blank local revisions to fill
                // in the local history:
                long sequence = 0;
                long localParentSequence = 0;
                string localParentRevID = null;
                for (int i = revHistory.Count - 1; i >= 0; --i)
                {
                    revId = revHistory[i];
                    RevisionInternal localRev = localRevs.RevWithDocIdAndRevId(docId, revId);
                    if (localRev != null)
                    {
                        // This revision is known locally. Remember its sequence as the parent of the next one:
                        sequence = localRev.GetSequence();
                        Debug.Assert((sequence > 0));
                        localParentSequence = sequence;
                        localParentRevID = revId;
                    }
                    else
                    {
                        // This revision isn't known, so add it:
                        RevisionInternal newRev;
                        IEnumerable<Byte> data = null;
                        bool current = false;
                        if (i == 0)
                        {
                            // Hey, this is the leaf revision we're inserting:
                            newRev = rev;
                            if (!rev.IsDeleted())
                            {
                                data = EncodeDocumentJSON(rev);
                                if (data == null)
                                {
                                    throw new CouchbaseLiteException(StatusCode.BadRequest);
                                }
                            }
                            current = true;
                        }
                        else
                        {
                            // It's an intermediate parent, so insert a stub:
                            newRev = new RevisionInternal(docId, revId, false, this);
                        }

                        // Insert it:
                        sequence = InsertRevision(newRev, docNumericID, sequence, current, data);
                        if (sequence <= 0)
                        {
                            throw new CouchbaseLiteException(StatusCode.InternalServerError);
                        }
                        if (i == 0)
                        {
                            // Write any changed attachments for the new revision. As the parent sequence use
                            // the latest local revision (this is to copy attachments from):
                            var attachments = GetAttachmentsFromRevision(rev);
                            if (attachments != null)
                            {
                                ProcessAttachmentsForRevision(attachments, rev, localParentSequence);
                                StubOutAttachmentsInRevision(attachments, rev);
                            }
                        }
                    }
                }
                // Mark the latest local rev as no longer current:
                if (localParentSequence > 0 && localParentSequence != sequence)
                {
                    ContentValues args = new ContentValues();
                    args["current"] = 0;
                    string[] whereArgs = new string[] { Convert.ToString(localParentSequence) };
                    try
                    {
                        var numRowsChanged = StorageEngine.Update("revs", args, "sequence=?", whereArgs);
                        if (numRowsChanged == 0)
                        {
                            inConflict = true;
                        }
                    }
                    catch (Exception)
                    {
                        throw new CouchbaseLiteException(StatusCode.InternalServerError);
                    }
                }

                var winningRev = Winner(docNumericID, oldWinningRevID, oldWinnerWasDeletion, rev);
                success = true;
                NotifyChange(rev, winningRev, source, inConflict);
            }
            catch (SQLException)
            {
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
            finally
            {
                EndTransaction(success);
            }
        }

        private Int64 GetOrInsertDocNumericID(String docId)
        {
            var docNumericId = GetDocNumericID(docId);
            if (docNumericId == 0)
            {
                docNumericId = InsertDocumentID(docId);
            }
            return docNumericId;
        }

        /// <summary>Deletes obsolete attachments from the sqliteDb and blob store.</summary>
        private Status GarbageCollectAttachments()
        {
            // First delete attachment rows for already-cleared revisions:
            // OPT: Could start after last sequence# we GC'd up to
            try
            {
                StorageEngine.ExecSQL("DELETE FROM attachments WHERE sequence IN (SELECT sequence from revs WHERE json IS null)");
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error deleting attachments", e);
            }

            // Now collect all remaining attachment IDs and tell the store to delete all but these:
            Cursor cursor = null;
            try
            {
                cursor = StorageEngine.RawQuery("SELECT DISTINCT key FROM attachments", CommandBehavior.SequentialAccess);
                cursor.MoveToNext();

                var allKeys = new AList<BlobKey>();
                while (!cursor.IsAfterLast())
                {
                    var key = new BlobKey(cursor.GetBlob(0));
                    allKeys.AddItem(key);
                    cursor.MoveToNext();
                }

                var numDeleted = Attachments.DeleteBlobsExceptWithKeys(allKeys);
                if (numDeleted < 0)
                {
                    return new Status(StatusCode.InternalServerError);
                }

                Log.V(Tag, "Deleted " + numDeleted + " attachments");

                return new Status(StatusCode.Ok);
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error finding attachment keys in use", e);
                return new Status(StatusCode.InternalServerError);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        internal Boolean SetLastSequence(String lastSequence, String checkpointId, Boolean push)
        {
            var values = new ContentValues();
            values.Put("remote", checkpointId);
            values["push"] = push;
            values["last_sequence"] = lastSequence;
            var newId = StorageEngine.InsertWithOnConflict("replicators", null, values, ConflictResolutionStrategy.Replace);
            Log.D(Tag, "Set Last Sequence: {0}: {1} / {2}".Fmt(lastSequence, checkpointId, newId));
            return (newId == -1);
        }

        internal String LastSequenceWithCheckpointId(string checkpointId)
        {
            Cursor cursor = null;
            string result = null;
            try
            {
                var args = new [] { checkpointId };
                cursor = StorageEngine.RawQuery("SELECT last_sequence FROM replicators WHERE remote=?", args);
                if (cursor.MoveToNext())
                {
                    result = cursor.GetString(0);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting last sequence", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            Log.D(Tag, "LastSequenceWithCheckpointId: {1} -> {0}".Fmt(result, checkpointId));
            return result;
        }

        private IDictionary<String, BlobStoreWriter> PendingAttachmentsByDigest
        {
            get {
                return _pendingAttachmentsByDigest ?? (_pendingAttachmentsByDigest = new Dictionary<String, BlobStoreWriter>());
            }
            set {
                _pendingAttachmentsByDigest = value;
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IEnumerable<QueryRow> QueryViewNamed(String viewName, QueryOptions options, IList<Int64> outLastSequence)
        {
            var before = Runtime.CurrentTimeMillis();
            var lastSequence = 0L;
            IEnumerable<QueryRow> rows;

            if (!String.IsNullOrEmpty (viewName)) {
                var view = GetView (viewName);
                if (view == null)
                {
                    throw new CouchbaseLiteException (StatusCode.NotFound);
                }
                    
                lastSequence = view.LastSequenceIndexed;
                if (options.GetStale () == IndexUpdateMode.Before || lastSequence <= 0) {
                    view.UpdateIndex ();
                    lastSequence = view.LastSequenceIndexed;
                } else {
                    if (options.GetStale () == IndexUpdateMode.After 
                        && lastSequence < GetLastSequenceNumber())
                        // NOTE: The exception is handled inside the thread.
                        // TODO: Consider using the async keyword instead.
                        try
                        {
                            view.UpdateIndex();
                        }
                        catch (CouchbaseLiteException e)
                        {
                            Log.E(Tag, "Error updating view index on background thread", e);
                        }
                }
                rows = view.QueryWithOptions (options);
            } else {
                // nil view means query _all_docs
                // note: this is a little kludgy, but we have to pull out the "rows" field from the
                // result dictionary because that's what we want.  should be refactored, but
                // it's a little tricky, so postponing.
                var allDocsResult = GetAllDocs (options);
                rows = (IList<QueryRow>)allDocsResult.Get ("rows");
                lastSequence = GetLastSequenceNumber ();
            }
            outLastSequence.AddItem(lastSequence);

            var delta = Runtime.CurrentTimeMillis() - before;
            Log.D(Tag, String.Format("Query view {0} completed in {1} milliseconds", viewName, delta));

            return rows;
        }

        internal RevisionList ChangesSince(long lastSeq, ChangesOptions options, FilterDelegate filter)
        {
            // http://wiki.apache.org/couchdb/HTTP_database_API#Changes
            if (options == null)
            {
                options = new ChangesOptions();
            }

            var includeDocs = options.IsIncludeDocs() || (filter != null);
            var additionalSelectColumns = string.Empty;

            if (includeDocs)
            {
                additionalSelectColumns = ", json";
            }

            var sql = "SELECT sequence, revs.doc_id, docid, revid, deleted" + additionalSelectColumns
                      + " FROM revs, docs " + "WHERE sequence > ? AND current=1 " + "AND revs.doc_id = docs.doc_id "
                      + "ORDER BY revs.doc_id, revid DESC";
            var args = lastSeq;

            Cursor cursor = null;
            RevisionList changes = null;

            try
            {
                cursor = StorageEngine.RawQuery(sql, CommandBehavior.SequentialAccess, args);
                cursor.MoveToNext();

                changes = new RevisionList();
                long lastDocId = 0;

                while (!cursor.IsAfterLast())
                {
                    if (!options.IsIncludeConflicts())
                    {
                        // Only count the first rev for a given doc (the rest will be losing conflicts):
                        var docNumericId = cursor.GetLong(1);
                        if (docNumericId == lastDocId)
                        {
                            cursor.MoveToNext();
                            continue;
                        }
                        lastDocId = docNumericId;
                    }

                    var sequence = cursor.GetLong(0);
                    var rev = new RevisionInternal(cursor.GetString(2), cursor.GetString(3), (cursor.GetInt(4) > 0), this);
                    rev.SetSequence(sequence);

                    if (includeDocs)
                    {
                        ExpandStoredJSONIntoRevisionWithAttachments(cursor.GetBlob(5), rev, options.GetContentOptions());
                    }
                    IDictionary<string, object> paramsFixMe = null;
                    // TODO: these should not be null
                    if (RunFilter(filter, paramsFixMe, rev))
                    {
                        changes.AddItem(rev);
                    }
                    cursor.MoveToNext();
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error looking for changes", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            if (options.IsSortBySequence())
            {
                changes.SortBySequence();
            }
            changes.Limit(options.GetLimit());
            return changes;
        }

        internal bool RunFilter(FilterDelegate filter, IDictionary<string, object> paramsIgnored, RevisionInternal rev)
        {
            if (filter == null)
            {
                return true;
            }
            var publicRev = new SavedRevision(this, rev);
            return filter(publicRev, null);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IDictionary<String, Object> GetAllDocs(QueryOptions options)
        {
            var result = new Dictionary<String, Object>();
            var rows = new AList<QueryRow>();
            if (options == null)
                options = new QueryOptions();

            var includeDeletedDocs = (options.GetAllDocsMode() == AllDocsMode.IncludeDeleted);
            var updateSeq = 0L;
            if (options.IsUpdateSeq())
            {
                updateSeq = GetLastSequenceNumber();
            }

            // TODO: needs to be atomic with the following SELECT
            var sql = new StringBuilder("SELECT revs.doc_id, docid, revid, sequence");
            if (options.IsIncludeDocs())
            {
                sql.Append(", json");
            }
            if (includeDeletedDocs)
            {
                sql.Append(", deleted");
            }
            sql.Append(" FROM revs, docs WHERE");

            if (options.GetKeys() != null)
            {
                if (options.GetKeys().Count() == 0)
                {
                    return result;
                }
                var commaSeperatedIds = JoinQuotedObjects(options.GetKeys());
                sql.Append(String.Format(" revs.doc_id IN (SELECT doc_id FROM docs WHERE docid IN ({0})) AND", commaSeperatedIds));
            }
            sql.Append(" docs.doc_id = revs.doc_id AND current=1");

            if (!includeDeletedDocs)
            {
                sql.Append(" AND deleted=0");
            }

            var args = new AList<String>();
            var minKey = options.GetStartKey();
            var maxKey = options.GetEndKey();
            var inclusiveMin = true;
            var inclusiveMax = options.IsInclusiveEnd();

            if (options.IsDescending())
            {
                minKey = maxKey;
                maxKey = options.GetStartKey();
                inclusiveMin = inclusiveMax;
                inclusiveMax = true;
            }
            if (minKey != null)
            {
                Debug.Assert((minKey is String));
                sql.Append((inclusiveMin ? " AND docid >= ?" : " AND docid > ?"));
                args.AddItem((string)minKey);
            }
            if (maxKey != null)
            {
                Debug.Assert((maxKey is string));
                sql.Append((inclusiveMax ? " AND docid <= ?" : " AND docid < ?"));
                args.AddItem((string)maxKey);
            }
            sql.Append(
                String.Format(" ORDER BY docid {0}, {1} revid DESC LIMIT ? OFFSET ?", 
                    options.IsDescending() ? "DESC" : "ASC", 
                    includeDeletedDocs ? "deleted ASC," : String.Empty
                )
            );
            args.AddItem(options.GetLimit().ToString());
            args.AddItem(options.GetSkip().ToString());

            Cursor cursor = null;
            var docs = new Dictionary<String, QueryRow>();
            try
            {
                cursor = StorageEngine.RawQuery(
                    sql.ToString(),
                    CommandBehavior.SequentialAccess,
                    args.ToArray()
                );

//                cursor.MoveToNext();

                var keepGoing = cursor.MoveToNext();
                while (keepGoing)
                {
                    var docNumericID = cursor.GetLong(0);

                    var includeDocs = options.IsIncludeDocs();

                    var docId = cursor.GetString(1);
                    var revId = cursor.GetString(2);
                    var sequenceNumber = cursor.GetLong(3);
                    byte[] json = null;
                    if (includeDocs)
                    {
                        json = cursor.GetBlob(4);
                    }
                    var deleted = includeDeletedDocs && cursor.GetInt(GetDeletedColumnIndex(options)) > 0;

                    IDictionary<String, Object> docContents = null;

                    if (includeDocs)
                    {
                        docContents = DocumentPropertiesFromJSON(json, docId, revId, deleted, sequenceNumber, options.GetContentOptions());
                    }
                    // Iterate over following rows with the same doc_id -- these are conflicts.
                    // Skip them, but collect their revIDs if the 'conflicts' option is set:
                    var conflicts = new List<string>();
                    while (((keepGoing = cursor.MoveToNext())) && cursor.GetLong(0) == docNumericID)
                    {
                       if (options.GetAllDocsMode() == AllDocsMode.ShowConflicts || options.GetAllDocsMode() == AllDocsMode.OnlyConflicts)
                       {
                           if (conflicts.IsEmpty())
                           {
                               conflicts.AddItem(revId);
                           }
                           conflicts.AddItem(cursor.GetString(2));
                       }
                    }
                    if (options.GetAllDocsMode() == AllDocsMode.OnlyConflicts && conflicts.IsEmpty())
                    {
                       continue;
                    }
                    var value = new Dictionary<string, object>();
                    value["rev"] = revId;
                    value["_conflicts"] = conflicts;
                    if (includeDeletedDocs)
                    {
                        value["deleted"] = deleted;
                    }
                    var change = new QueryRow(docId, sequenceNumber, docId, value, docContents);
                    change.Database = this;

                    if (options.GetKeys() != null)
                    {
                        docs[docId] = change;
                    }
                    else
                    {
                        rows.AddItem(change);
                    }
                }
                if (options.GetKeys() != null)
                {
                    foreach (var docIdObject in options.GetKeys())
                    {
                        if (docIdObject is string)
                        {
                            var docId = (string)docIdObject;
                            var change = docs.Get(docId);
                            if (change == null)
                            {
                                var value = new Dictionary<string, object>();
                                var docNumericID = GetDocNumericID(docId);
                                if (docNumericID > 0)
                                {
                                    bool deleted;
                                    var outIsDeleted = new AList<bool>();
                                    var outIsConflict = new AList<bool>();
                                    var revId = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict);
                                    if (outIsDeleted.Count > 0)
                                    {
                                        deleted = true;
                                    }
                                    if (revId != null)
                                    {
                                        value["rev"] = revId;
                                        value["deleted"] = true; // FIXME: SHould this be set the value of `deleted`?
                                    }
                                }
                                change = new QueryRow((value != null ? docId : null), 0, docId, value, null);
                                change.Database = this;
                            }
                            rows.AddItem(change);
                        }
                    }
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting all docs", e);
                throw new CouchbaseLiteException("Error getting all docs", e, new Status(StatusCode.InternalServerError));
            }
            finally
            {
                if (cursor != null)
                    cursor.Close();
            }
            result["rows"] = rows;
            result["total_rows"] = rows.Count;
            result.Put("offset", options.GetSkip());
            if (updateSeq != 0)
            {
                result["update_seq"] = updateSeq;
            }
            return result;
        }

        /// <summary>Returns the rev ID of the 'winning' revision of this document, and whether it's deleted.</summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal String WinningRevIDOfDoc(Int64 docNumericId, IList<Boolean> outIsDeleted, IList<Boolean> outIsConflict)
        {
            Cursor cursor = null;
            var args = new [] { Convert.ToString(docNumericId) };
            String revId = null;
            var sql = "SELECT revid, deleted FROM revs WHERE doc_id=? and current=1" 
                      + " ORDER BY deleted asc, revid desc LIMIT 2";

            try
            {
                cursor = StorageEngine.RawQuery(sql, args);
                cursor.MoveToNext();

                if (!cursor.IsAfterLast())
                {
                    revId = cursor.GetString(0);
                    var deleted = cursor.GetInt(1) > 0;
                    if (deleted)
                    {
                        outIsDeleted.AddItem(true);
                    }

                    // The document is in conflict if there are two+ result rows that are not deletions.
                    var hasNextResult = cursor.MoveToNext();
                    if (hasNextResult)
                    {
                        var isNextDeleted = cursor.GetInt(1) > 0;
                        var isInConflict = !deleted && hasNextResult && isNextDeleted;
                        if (isInConflict)
                        {
                            outIsConflict.AddItem(true);
                        }
                    }
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error", e);
                throw new CouchbaseLiteException("Error", e, new Status(StatusCode.InternalServerError));
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return revId;
        }

        internal IDictionary<String, Object> DocumentPropertiesFromJSON(IEnumerable<Byte> json, String docId, String revId, Boolean deleted, Int64 sequence, EnumSet<TDContentOptions> contentOptions)
        {
            var rev = new RevisionInternal(docId, revId, deleted, this);
            rev.SetSequence(sequence);

            IDictionary<String, Object> extra = ExtraPropertiesForRevision(rev, contentOptions);
            if (json == null)
            {
                return extra;
            }

            IDictionary<String, Object> docProperties = null;
            try
            {
                docProperties = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(json);
                docProperties.PutAll(extra);
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error serializing properties to JSON", e);
            }
            return docProperties;
        }


        /// <summary>Hack because cursor interface does not support cursor.getColumnIndex("deleted") yet.
        ///     </summary>
        internal Int32 GetDeletedColumnIndex(QueryOptions options)
        {
            Debug.Assert(options != null);

            return options.IsIncludeDocs() ? 5 : 4;
        }

        internal static String JoinQuotedObjects(IEnumerable<Object> objects)
        {
            var strings = new AList<String>();
            foreach (var obj in objects)
            {
                strings.AddItem(obj != null ? obj.ToString() : null);
            }
            return JoinQuoted(strings);
        }

        internal static String JoinQuoted(IList<String> strings)
        {
            if (strings.Count == 0)
            {
                return String.Empty;
            }

            var result = "'";
            var first = true;

            foreach (string str in strings)
            {
                if (first)
                    first = false;
                else
                    result = result + "','";

                result = result + Quote(str);
            }

            result = result + "'";

            return result;
        }

        internal static string Quote(string str)
        {
            return str.Replace("'", "''");
        }

        internal View RegisterView(View view)
        {
            if (view == null)
            {
                return null;
            }
            if (views == null)
            {
                views = new Dictionary<string, View>();
            }
            views.Put(view.Name, view);
            return view;
        }

        internal View MakeAnonymousView()
        {
            for (var i = 0; true; ++i)
            {
                var name = String.Format("anon{0}", i);
                var existing = GetExistingView(name);
                if (existing == null)
                {
                    // this name has not been used yet, so let's use it
                    return GetView(name);
                }
            }
        }

        internal IList<View> GetAllViews()
        {
            Cursor cursor = null;
            IList<View> result = null;
            try
            {
                cursor = StorageEngine.RawQuery("SELECT name FROM views");
                result = new AList<View>();
                if (cursor.MoveToNext())
                {
                    var name = cursor.GetString(0);
                    var view = GetView(name);
                    result.Add(view);
                }
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error getting all views", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        internal Status DeleteViewNamed(String name)
        {
            var result = new Status(StatusCode.InternalServerError);
            try
            {
                var whereArgs = new [] { name };
                var rowsAffected = StorageEngine.Delete("views", "name=?", whereArgs);

                if (rowsAffected > 0)
                {
                    result.SetCode(StatusCode.Ok);
                }
                else
                {
                    result.SetCode(StatusCode.NotFound);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error deleting view", e);
            }
            return result;
        }

        /// <summary>
        /// Creates a one-shot query with the given map function. This is equivalent to creating an
        /// anonymous View and then deleting it immediately after querying it. It may be useful during
        /// development, but in general this is inefficient if this map will be used more than once,
        /// because the entire view has to be regenerated from scratch every time.
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="map">Map.</param>
        internal Query SlowQuery(MapDelegate map) 
        {
            return new Query(this, map);
        }

        internal RevisionInternal GetParentRevision(RevisionInternal rev)
        {
            // First get the parent's sequence:
            var seq = rev.GetSequence();
            if (seq > 0)
            {
                seq = LongForQuery("SELECT parent FROM revs WHERE sequence=?", new [] { Convert.ToString(seq) });
            }
            else
            {
                var docNumericID = GetDocNumericID(rev.GetDocId());
                if (docNumericID <= 0)
                {
                    return null;
                }
                var args = new [] { Convert.ToString(docNumericID), rev.GetRevId() };
                seq = LongForQuery("SELECT parent FROM revs WHERE doc_id=? and revid=?", args);
            }
            if (seq == 0)
            {
                return null;
            }

            // Now get its revID and deletion status:
            RevisionInternal result = null;
            var queryArgs = new [] { Convert.ToString(seq) };
            var queryString = "SELECT revid, deleted FROM revs WHERE sequence=?";

            Cursor cursor = null;
            try
            {
                cursor = StorageEngine.RawQuery(queryString, queryArgs);
                if (cursor.MoveToNext())
                {
                    string revId = cursor.GetString(0);
                    bool deleted = (cursor.GetInt(1) > 0);
                    result = new RevisionInternal(rev.GetDocId(), revId, deleted, this);
                    result.SetSequence(seq);
                }
            }
            finally
            {
                cursor.Close();
            }
            return result;
        }

        /// <summary>The latest sequence number used.</summary>
        /// <remarks>
        /// The latest sequence number used.  Every new revision is assigned a new sequence number,
        /// so this property increases monotonically as changes are made to the database. It can be
        /// used to check whether the database has changed between two points in time.
        /// </remarks>
        internal Int64 GetLastSequenceNumber()
        {
            string sql = "SELECT MAX(sequence) FROM revs";
            Cursor cursor = null;
            long result = 0;
            try
            {
                cursor = StorageEngine.RawQuery(sql);
                if (cursor.MoveToNext())
                {
                    result = cursor.GetLong(0);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting last sequence", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }
               
        /// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
        internal Int64 LongForQuery(string sqlQuery, IEnumerable<string> args)
        {
            Cursor cursor = null;
            var result = 0L;
            try
            {
                cursor = StorageEngine.RawQuery(sqlQuery, args.ToArray());
                if (cursor.MoveToNext())
                {
                    result = cursor.GetLong(0);
                }
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        /// <summary>Purges specific revisions, which deletes them completely from the local database _without_ adding a "tombstone" revision.
        ///     </summary>
        /// <remarks>
        /// Purges specific revisions, which deletes them completely from the local database _without_ adding a "tombstone" revision. It's as though they were never there.
        /// This operation is described here: http://wiki.apache.org/couchdb/Purge_Documents
        /// </remarks>
        /// <param name="docsToRevs">A dictionary mapping document IDs to arrays of revision IDs.
        ///     </param>
        /// <resultOn>success will point to an NSDictionary with the same form as docsToRev, containing the doc/revision IDs that were actually removed.
        ///     </resultOn>
        internal IDictionary<String, Object> PurgeRevisions(IDictionary<String, IList<String>> docsToRevs)
        {
            var result = new Dictionary<String, Object>();
            RunInTransaction(() => PurgeRevisionsTask(this, docsToRevs, result));
            // no such document, skip it
            // Delete all revisions if magic "*" revision ID is given:
            // Iterate over all the revisions of the doc, in reverse sequence order.
            // Keep track of all the sequences to delete, i.e. the given revs and ancestors,
            // but not any non-given leaf revs or their ancestors.
            // Purge it and maybe its parent:
            // Keep it and its parent:
            // Now delete the sequences to be purged.
            return result;
        }

        internal void RemoveDocumentFromCache(Document document)
        {
            DocumentCache.Remove(document.Id);
            UnsavedRevisionDocumentCache.Remove(document.Id);
        }

        internal BlobStoreWriter AttachmentWriter { get { return new BlobStoreWriter(Attachments); } }

        internal BlobStore Attachments { get; set; }

        internal String PrivateUUID ()
        {
            string result = null;
            Cursor cursor = null;
            try
            {
                cursor = StorageEngine.RawQuery("SELECT value FROM info WHERE key='privateUUID'");
                if (cursor.MoveToNext())
                {
                    result = cursor.GetString(0);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error querying privateUUID", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        internal String PublicUUID()
        {
            string result = null;
            Cursor cursor = null;
            try
            {
                cursor = StorageEngine.RawQuery("SELECT value FROM info WHERE key='publicUUID'");
                if (cursor.MoveToNext())
                {
                    result = cursor.GetString(0);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error querying privateUUID", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        internal BlobStoreWriter GetAttachmentWriter()
        {
            return new BlobStoreWriter(Attachments);
        }

        internal Boolean ReplaceUUIDs()
        {
            var query = "UPDATE INFO SET value='" + Misc.TDCreateUUID() + "' where key = 'privateUUID';";

            try
            {
                StorageEngine.ExecSQL(query);
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error updating UUIDs", e);
                return false;
            }

            query = "UPDATE INFO SET value='" + Misc.TDCreateUUID() + "' where key = 'publicUUID';";

            try
            {
                StorageEngine.ExecSQL(query);
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error updating UUIDs", e);
                return false;
            }

            return true;
        }

        internal Attachment GetAttachmentForSequence (long sequence, string filename)
        {
            Debug.Assert((sequence > 0));
            Debug.Assert((filename != null));

            Cursor cursor = null;
            var args = new [] { Convert.ToString(sequence), filename };
            try
            {
                cursor = StorageEngine.RawQuery("SELECT key, type FROM attachments WHERE sequence=? AND filename=?", args);

                if (!cursor.MoveToNext())
                {
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }

                var keyData = cursor.GetBlob(0);

                //TODO add checks on key here? (ios version)
                var key = new BlobKey(keyData);
                var contentStream = Attachments.BlobStreamForKey(key);
                if (contentStream == null)
                {
                    Log.E(Tag, "Failed to load attachment");
                    throw new CouchbaseLiteException(StatusCode.InternalServerError);
                }
                else
                {
                    var result = new Attachment(contentStream, cursor.GetString(1));
                    result.Compressed = Attachments.IsGZipped(key);
                    return result;
                }
            }
            catch (SQLException)
            {
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        internal void RememberAttachmentWritersForDigests(IDictionary<String, BlobStoreWriter> blobsByDigest)
        {
            PendingAttachmentsByDigest.PutAll(blobsByDigest);
        }

        internal void RememberAttachmentWriter (BlobStoreWriter writer)
        {
            var digest = writer.MD5DigestString();
            PendingAttachmentsByDigest[digest] = writer;
        }

        internal Int64 GetDocNumericID(string docId)
        {
            Cursor cursor = null;
            string[] args = new string[] { docId };
            long result = -1;
            try
            {
                cursor = StorageEngine.RawQuery("SELECT doc_id FROM docs WHERE docid=?", args);
                if (cursor.MoveToNext())
                {
                    result = cursor.GetLong(0);
                }
                else
                {
                    result = 0;
                }
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error getting doc numeric id", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        /// <summary>Begins a database transaction.</summary>
        /// <remarks>
        /// Begins a database transaction. Transactions can nest.
        /// Every beginTransaction() must be balanced by a later endTransaction()
        /// </remarks>
        internal Boolean BeginTransaction()
        {
            try
            {
                StorageEngine.BeginTransaction();

                ++transactionLevel;

                Log.D(Tag, "Begin transaction (level " + transactionLevel + ")");
            }
            catch (SQLException e)
            {
                Log.E(Tag," Error calling beginTransaction()" , e);

                return false;
            }
            return true;
        }

        /// <summary>Commits or aborts (rolls back) a transaction.</summary>
        /// <param name="commit">If true, commits; if false, aborts and rolls back, undoing all changes made since the matching -beginTransaction call, *including* any committed nested transactions.
        ///     </param>
        internal Boolean EndTransaction(bool commit)
        {
            Debug.Assert((transactionLevel > 0));

            if (commit)
            {
                Log.D(Tag, "Committing transaction (level " + transactionLevel + ")");

                StorageEngine.SetTransactionSuccessful();
                StorageEngine.EndTransaction();
            }
            else
            {
                Log.I(Tag, " CANCEL transaction (level " + transactionLevel + ")");
                try
                {
                    StorageEngine.EndTransaction();
                }
                catch (SQLException e)
                {
                    Log.E(Tag, " Error calling endTransaction()", e);

                    return false;
                }
            }

            --transactionLevel;
            PostChangeNotifications();

            return true;
        }

        internal static Boolean PurgeRevisionsTask(Database enclosingDatabase, IDictionary<String, IList<String>> docsToRevs, IDictionary<String, Object> result)
        {
            foreach (string docID in docsToRevs.Keys)
            {
                long docNumericID = enclosingDatabase.GetDocNumericID(docID);
                if (docNumericID == -1)
                {
                    continue;
                }
                var revsPurged = new AList<string>();
                var revIDs = docsToRevs [docID];
                if (revIDs == null)
                {
                    return false;
                }
                else
                {
                    if (revIDs.Count == 0)
                    {
                        revsPurged = new AList<string>();
                    }
                    else
                    {
                        if (revIDs.Contains("*"))
                        {
                            try
                            {
                                var args = new[] { Convert.ToString(docNumericID) };
                                enclosingDatabase.StorageEngine.ExecSQL("DELETE FROM revs WHERE doc_id=?", args);
                            }
                            catch (SQLException e)
                            {
                                Log.E(Tag, "Error deleting revisions", e);
                                return false;
                            }
                            revsPurged = new AList<string>();
                            revsPurged.AddItem("*");
                        }
                        else
                        {
                            Cursor cursor = null;
                            try
                            {
                                var args = new [] { Convert.ToString(docNumericID) };
                                var queryString = "SELECT revid, sequence, parent FROM revs WHERE doc_id=? ORDER BY sequence DESC";
                                cursor = enclosingDatabase.StorageEngine.RawQuery(queryString, args);
                                if (!cursor.MoveToNext())
                                {
                                    Log.W(Tag, "No results for query: " + queryString);
                                    return false;
                                }
                                var seqsToPurge = new HashSet<long>();
                                var seqsToKeep = new HashSet<long>();
                                var revsToPurge = new HashSet<string>();
                                while (!cursor.IsAfterLast())
                                {
                                    string revID = cursor.GetString(0);
                                    long sequence = cursor.GetLong(1);
                                    long parent = cursor.GetLong(2);
                                    if (seqsToPurge.Contains(sequence) || revIDs.Contains(revID) && !seqsToKeep.Contains
                                            (sequence))
                                    {
                                        seqsToPurge.AddItem(sequence);
                                        revsToPurge.AddItem(revID);
                                        if (parent > 0)
                                        {
                                            seqsToPurge.AddItem(parent);
                                        }
                                    }
                                    else
                                    {
                                        seqsToPurge.Remove(sequence);
                                        revsToPurge.Remove(revID);
                                        seqsToKeep.AddItem(parent);
                                    }
                                    cursor.MoveToNext();
                                }
                                seqsToPurge.RemoveAll(seqsToKeep);
                                Log.I(Tag, String.Format("Purging doc '{0}' revs ({1}); asked for ({2})", docID, revsToPurge, revIDs));
                                if (seqsToPurge.Count > 0)
                                {
                                    string seqsToPurgeList = String.Join(",", seqsToPurge);
                                    string sql = string.Format("DELETE FROM revs WHERE sequence in ({0})", seqsToPurgeList);
                                    try
                                    {
                                        enclosingDatabase.StorageEngine.ExecSQL(sql);
                                    }
                                    catch (SQLException e)
                                    {
                                        Log.E(Tag, "Error deleting revisions via: " + sql, e);
                                        return false;
                                    }
                                }
                                Collections.AddAll(revsPurged, revsToPurge);
                            }
                            catch (SQLException e)
                            {
                                Log.E(Tag, "Error getting revisions", e);
                                return false;
                            }
                            finally
                            {
                                if (cursor != null)
                                {
                                    cursor.Close();
                                }
                            }
                        }
                    }
                }
                result[docID] = revsPurged;
            }
            return true;
        }

        internal RevisionInternal GetDocumentWithIDAndRev(String id, String rev, EnumSet<TDContentOptions> contentOptions)
        {
            RevisionInternal result = null;
            string sql;
            Cursor cursor = null;
            try
            {
                cursor = null;
                var cols = "revid, deleted, sequence";
                if (!contentOptions.Contains(TDContentOptions.TDNoBody))
                {
                    cols += ", json";
                }
                if (rev != null)
                {
                    sql = "SELECT " + cols + " FROM revs, docs WHERE docs.docid=? AND revs.doc_id=docs.doc_id AND revid=? LIMIT 1";
                    var args = new[] { id, rev };
                    cursor = StorageEngine.RawQuery(sql, args);
                }
                else
                {
                    sql = "SELECT " + cols + " FROM revs, docs WHERE docs.docid=? AND revs.doc_id=docs.doc_id and current=1 and deleted=0 ORDER BY revid DESC LIMIT 1";
                    var args = new[] { id };
                    cursor = StorageEngine.RawQuery(sql, CommandBehavior.SequentialAccess, args);
                }
                if (cursor.MoveToNext())
                {
                    if (rev == null)
                    {
                        rev = cursor.GetString(0);
                    }
                    var deleted = cursor.GetInt(1) > 0;
                    result = new RevisionInternal(id, rev, deleted, this);
                    result.SetSequence(cursor.GetLong(2));
                    if (!contentOptions.Equals(EnumSet.Of(TDContentOptions.TDNoBody)))
                    {
                        byte[] json = null;
                        if (!contentOptions.Contains(TDContentOptions.TDNoBody))
                        {
                            json = cursor.GetBlob(3);
                        }
                        ExpandStoredJSONIntoRevisionWithAttachments(json, result, contentOptions);
                    }
                }
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error getting document with id and rev", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        /// <summary>Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
        ///     </summary>
        /// <remarks>
        /// Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
        /// Rev must already have its revID and sequence properties set.
        /// </remarks>
        internal void ExpandStoredJSONIntoRevisionWithAttachments(IEnumerable<Byte> json, RevisionInternal rev, EnumSet<TDContentOptions> contentOptions)
        {
            var extra = ExtraPropertiesForRevision(rev, contentOptions);

            if (json != null)
            {
                rev.SetJson(AppendDictToJSON(json, extra));
            }
            else
            {
                rev.SetProperties(extra);
            }
        }

        /// <summary>Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
        ///     </summary>
        /// <remarks>
        /// Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
        /// Rev must already have its revID and sequence properties set.
        /// </remarks>
        internal IDictionary<String, Object> ExtraPropertiesForRevision(RevisionInternal rev, EnumSet<TDContentOptions> contentOptions)
        {
            var docId = rev.GetDocId();
            var revId = rev.GetRevId();

            var sequenceNumber = rev.GetSequence();

            Debug.Assert((revId != null));
            Debug.Assert((sequenceNumber > 0));

            // Get attachment metadata, and optionally the contents:
            var attachmentsDict = GetAttachmentsDictForSequenceWithContent(sequenceNumber, contentOptions);

            // Get more optional stuff to put in the properties:
            //OPT: This probably ends up making redundant SQL queries if multiple options are enabled.
            var localSeq = -1L;
            if (contentOptions.Contains(TDContentOptions.TDIncludeLocalSeq))
            {
                localSeq = sequenceNumber;
            }
            IDictionary<string, object> revHistory = null;
            if (contentOptions.Contains(TDContentOptions.TDIncludeRevs))
            {
                revHistory = GetRevisionHistoryDict(rev);
            }
            IList<object> revsInfo = null;
            if (contentOptions.Contains(TDContentOptions.TDIncludeRevsInfo))
            {
                revsInfo = new AList<object>();
                var revHistoryFull = GetRevisionHistory(rev);
                foreach (RevisionInternal historicalRev in revHistoryFull)
                {
                    var revHistoryItem = new Dictionary<string, object>();
                    var status = "available";
                    if (historicalRev.IsDeleted())
                    {
                        status = "deleted";
                    }
                    // TODO: Detect missing revisions, set status="missing"
                    if (historicalRev.IsMissing())
                    {
                        status = "missing";
                    }
                    revHistoryItem.Put("rev", historicalRev.GetRevId());
                    revHistoryItem["status"] = status;
                    revsInfo.AddItem(revHistoryItem);
                }
            }
            IList<string> conflicts = null;
            if (contentOptions.Contains(TDContentOptions.TDIncludeConflicts))
            {
                var revs = GetAllRevisionsOfDocumentID(docId, true);
                if (revs.Count > 1)
                {
                    conflicts = new AList<string>();
                    foreach (RevisionInternal historicalRev in revs)
                    {
                        if (!historicalRev.Equals(rev))
                        {
                            conflicts.AddItem(historicalRev.GetRevId());
                        }
                    }
                }
            }

            var result = new Dictionary<string, object>();
            result["_id"] = docId;
            result["_rev"] = revId;

            if (rev.IsDeleted())
            {
                result["_deleted"] = true;
            }
            if (attachmentsDict != null)
            {
                result["_attachments"] = attachmentsDict;
            }
            if (localSeq > -1)
            {
                result["_local_seq"] = localSeq;
            }
            if (revHistory != null)
            {
                result["_revisions"] = revHistory;
            }
            if (revsInfo != null)
            {
                result["_revs_info"] = revsInfo;
            }
            if (conflicts != null)
            {
                result["_conflicts"] = conflicts;
            }
            return result;
        }

        /// <summary>Returns an array of TDRevs in reverse chronological order, starting with the given revision.
        ///     </summary>
        /// <remarks>Returns an array of TDRevs in reverse chronological order, starting with the given revision.
        ///     </remarks>
        internal IList<RevisionInternal> GetRevisionHistory(RevisionInternal rev)
        {
            string docId = rev.GetDocId();
            string revId = rev.GetRevId();

            Debug.Assert(((docId != null) && (revId != null)));

            long docNumericId = GetDocNumericID(docId);
            if (docNumericId < 0)
            {
                return null;
            }
            else
            {
                if (docNumericId == 0)
                {
                    return new AList<RevisionInternal>();
                }
            }

            Cursor cursor = null;
            IList<RevisionInternal> result;
            var args = new [] { Convert.ToString(docNumericId) };
            var sql = "SELECT sequence, parent, revid, deleted, json isnull  FROM revs WHERE doc_id=? ORDER BY sequence DESC";

            try
            {
                cursor = StorageEngine.RawQuery(sql, args);
                cursor.MoveToNext();

                long lastSequence = 0;
                result = new AList<RevisionInternal>();

                while (!cursor.IsAfterLast())
                {
                    var sequence = cursor.GetLong(0);
                    var parent = cursor.GetLong(1);

                    bool matches = false;
                    if (lastSequence == 0)
                    {
                        matches = revId.Equals(cursor.GetString(2));
                    }
                    else
                    {
                        matches = (sequence == lastSequence);
                    }
                    if (matches)
                    {
                        revId = cursor.GetString(2);
                        var deleted = (cursor.GetInt(3) > 0);
                        var missing = (cursor.GetInt(4) > 0);

                        var aRev = new RevisionInternal(docId, revId, deleted, this);
                        aRev.SetSequence(sequence);
                        aRev.SetMissing(missing);
                        result.AddItem(aRev);

                        if (parent > -1)
                            lastSequence = parent;

                        if (lastSequence == 0)
                        {
                            break;
                        }
                    }
                    cursor.MoveToNext();
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting revision history", e);
                return null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        /// <summary>Returns the revision history as a _revisions dictionary, as returned by the REST API's ?revs=true option.
        ///     </summary>
        internal IDictionary<String, Object> GetRevisionHistoryDict(RevisionInternal rev)
        {
            return MakeRevisionHistoryDict(GetRevisionHistory(rev));
        }

        internal IDictionary<string, object> GetRevisionHistoryDictStartingFromAnyAncestor(RevisionInternal rev, IList<string>ancestorRevIDs)
        {
            var history = GetRevisionHistory(rev); // This is in reverse order, newest ... oldest
            if (ancestorRevIDs != null && ancestorRevIDs.Any())
            {
                for (var i = 0; i < history.Count; i++)
                {
                    if (ancestorRevIDs.Contains(history[i].GetRevId()))
                    {
                        var newHistory = new List<RevisionInternal>();
                        for (var index = 0; index < i + 1; index++) 
                        {
                            newHistory.Add(history[index]);
                        }
                        history = newHistory;
                        break;
                    }
                }
            }

            return MakeRevisionHistoryDict(history);
        }

        internal static IDictionary<string, object> MakeRevisionHistoryDict(IList<RevisionInternal> history)
        {
            if (history == null)
                return null;

            // Try to extract descending numeric prefixes:
            var suffixes = new AList<string>();
            var start = -1;
            var lastRevNo = -1;

            foreach (var rev in history)
            {
                var revNo = ParseRevIDNumber(rev.GetRevId());
                var suffix = ParseRevIDSuffix(rev.GetRevId());
                if (revNo > 0 && suffix.Length > 0)
                {
                    if (start < 0)
                    {
                        start = revNo;
                    }
                    else
                    {
                        if (revNo != lastRevNo - 1)
                        {
                            start = -1;
                            break;
                        }
                    }
                    lastRevNo = revNo;
                    suffixes.AddItem(suffix);
                }
                else
                {
                    start = -1;
                    break;
                }
            }

            var result = new Dictionary<String, Object>();
            if (start == -1)
            {
                // we failed to build sequence, just stuff all the revs in list
                suffixes = new AList<string>();
                foreach (RevisionInternal rev_1 in history)
                {
                    suffixes.AddItem(rev_1.GetRevId());
                }
            }
            else
            {
                result["start"] = start;
            }

            result["ids"] = suffixes;
            return result;
        }

        /// <summary>Parses the _revisions dict from a document into an array of revision ID strings.</summary>
        internal static IList<string> ParseCouchDBRevisionHistory(IDictionary<String, Object> docProperties)
        {
            var revs = (JObject)docProperties.Get("_revisions");
            var revisions = revs.ToObject<Dictionary<String, Object>>();
            if (revisions == null)
            {
                return new List<string>();
            }

            var ids = (JArray)revisions["ids"];
            if (ids == null || ids.Count == 0)
            {
                return new List<string>();
            }

            var revIDs = ids.Values<String>().ToList();
            var start = (Int64)revisions.Get("start");
            for (var i = 0; i < revIDs.Count; i++)
            {
                var revID = revIDs[i];
                revIDs.Set(i, Sharpen.Extensions.ToString(start--) + "-" + revID);
            }

            return revIDs;
        }

        // Splits a revision ID into its generation number and opaque suffix string
        internal static int ParseRevIDNumber(string rev)
        {
            var result = -1;
            var dashPos = rev.IndexOf("-", StringComparison.InvariantCultureIgnoreCase);

            if (dashPos >= 0)
            {
                try
                {
                    var revIdStr = rev.Substring(0, dashPos);

                    // Should return -1 when the string has WC at the beginning or at the end.
                    if (revIdStr.Length > 0 && 
                        (char.IsWhiteSpace(revIdStr[0]) || 
                            char.IsWhiteSpace(revIdStr[revIdStr.Length - 1])))
                    {
                        return result;
                    }

                    result = Int32.Parse(revIdStr);
                }
                catch (FormatException)
                {

                }
            }
            // ignore, let it return -1
            return result;
        }

        // Splits a revision ID into its generation number and opaque suffix string
        internal static string ParseRevIDSuffix(string rev)
        {
            var result = String.Empty;
            int dashPos = rev.IndexOf("-", StringComparison.InvariantCultureIgnoreCase);
            if (dashPos >= 0)
            {
                result = Runtime.Substring(rev, dashPos + 1);
            }
            return result;
        }

        /// <summary>Constructs an "_attachments" dictionary for a revision, to be inserted in its JSON body.</summary>
        internal IDictionary<String, Object> GetAttachmentsDictForSequenceWithContent(long sequence, EnumSet<TDContentOptions> contentOptions)
        {
            Debug.Assert((sequence > 0));

            Cursor cursor = null;
            var args = new Object[] { sequence };

            try
            {
                cursor = StorageEngine.RawQuery("SELECT filename, key, type, length, revpos FROM attachments WHERE sequence=?", CommandBehavior.SequentialAccess, args);
                if (!cursor.MoveToNext())
                {
                    return null;
                }

                var result = new Dictionary<String, Object>();

                while (!cursor.IsAfterLast())
                {
                    var dataSuppressed = false;
                    var filename = cursor.GetString(0);
                    var keyData = cursor.GetBlob(1);
                    var contentType = cursor.GetString(2);
                    var length = cursor.GetInt(3);
                    var revpos = cursor.GetInt(4);

                    var key = new BlobKey(keyData);
                    var digestString = "sha1-" + Convert.ToBase64String(keyData);

                    var dataBase64 = (string) null;
                    if (contentOptions.Contains(TDContentOptions.TDIncludeAttachments))
                    {
                        if (contentOptions.Contains(TDContentOptions.TDBigAttachmentsFollow) && 
                            length >= Database.BigAttachmentLength)
                        {
                            dataSuppressed = true;
                        }
                        else
                        {
                            byte[] data = Attachments.BlobForKey(key);
                            if (data != null)
                            {
                                dataBase64 = Convert.ToBase64String(data);
                            }
                            else
                            {
                                // <-- very expensive
                                Log.W(Tag, "Error loading attachment");
                            }
                        }
                    }
                    var attachment = new Dictionary<string, object>();
                    if (!(dataBase64 != null || dataSuppressed))
                    {
                        attachment["stub"] = true;
                    }
                    if (dataBase64 != null)
                    {
                        attachment["data"] = dataBase64;
                    }
                    if (dataSuppressed) {
                        attachment.Put ("follows", true);
                    }
                    attachment["digest"] = digestString;

                    attachment["content_type"] = contentType;
                    attachment["length"] = length;
                    attachment["revpos"] = revpos;

                    result[filename] = attachment;

                    cursor.MoveToNext();
                }
                return result;
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error getting attachments for sequence", e);
                return null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        /// <summary>Splices the contents of an NSDictionary into JSON data (that already represents a dict), without parsing the JSON.</summary>
        internal IEnumerable<Byte> AppendDictToJSON(IEnumerable<Byte> json, IDictionary<String, Object> dict)
        {
            if (dict.Count == 0)
                return json;

            Byte[] extraJSON;
            try
            {
                extraJSON = Manager.GetObjectMapper().WriteValueAsBytes(dict).ToArray();
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error convert extra JSON to bytes", e);
                return null;
            }

            var jsonArray = json.ToArray ();
            int jsonLength = jsonArray.Length;
            int extraLength = extraJSON.Length;

            if (jsonLength == 2)
            {
                // Original JSON was empty
                return extraJSON;
            }

            var newJson = new byte[jsonLength + extraLength - 1];
            Array.Copy(jsonArray.ToArray(), 0, newJson, 0, jsonLength - 1);

            // Copy json w/o trailing '}'
            newJson[jsonLength - 1] = (byte)(',');

            // Add a ','
            Array.Copy(extraJSON, 1, newJson, jsonLength, extraLength - 1);

            return newJson;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal PutRevision(RevisionInternal rev, String prevRevId, Status resultStatus)
        {
            return PutRevision(rev, prevRevId, false, resultStatus);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal PutRevision(RevisionInternal rev, String prevRevId, Boolean allowConflict)
        {
            Status ignoredStatus = new Status();
            return PutRevision(rev, prevRevId, allowConflict, ignoredStatus);
        }

        /// <summary>Stores a new (or initial) revision of a document.</summary>
        /// <remarks>
        /// Stores a new (or initial) revision of a document.
        /// This is what's invoked by a PUT or POST. As with those, the previous revision ID must be supplied when necessary and the call will fail if it doesn't match.
        /// </remarks>
        /// <param name="oldRev">The revision to add. If the docID is null, a new UUID will be assigned. Its revID must be null. It must have a JSON body.
        ///     </param>
        /// <param name="prevRevId">The ID of the revision to replace (same as the "?rev=" parameter to a PUT), or null if this is a new document.
        ///     </param>
        /// <param name="allowConflict">If false, an error status 409 will be returned if the insertion would create a conflict, i.e. if the previous revision already has a child.
        ///     </param>
        /// <param name="resultStatus">On return, an HTTP status code indicating success or failure.
        ///     </param>
        /// <returns>A new RevisionInternal with the docID, revID and sequence filled in (but no body).
        ///     </returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal PutRevision(RevisionInternal oldRev, String prevRevId, Boolean allowConflict, Status resultStatus)
        {
            // prevRevId is the rev ID being replaced, or nil if an insert
            var docId = oldRev.GetDocId();
            var deleted = oldRev.IsDeleted();

            if ((oldRev == null) || ((prevRevId != null) && (docId == null)) || (deleted && (docId == null)) || ((docId != null) && !IsValidDocumentId(docId)))
            {
                throw new CouchbaseLiteException(StatusCode.BadRequest);
            }
            BeginTransaction();
            Cursor cursor = null;
            var inConflict = false;
            RevisionInternal winningRev = null;
            RevisionInternal newRev = null;

            // PART I: In which are performed lookups and validations prior to the insert...
            var docNumericID = (docId != null) ? GetDocNumericID(docId) : 0;
            var parentSequence = 0L;
            string oldWinningRevID = null;
            try
            {
                var oldWinnerWasDeletion = false;
                var wasConflicted = false;
                if (docNumericID > 0)
                {
                    var outIsDeleted = new AList<bool>();
                    var outIsConflict = new AList<bool>();
                    try
                    {
                        oldWinningRevID = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict);
                        oldWinnerWasDeletion |= outIsDeleted.Count > 0;
                        wasConflicted |= outIsConflict.Count > 0;
                    }
                    catch (Exception e)
                    {
                        Sharpen.Runtime.PrintStackTrace(e);
                    }
                }
                if (prevRevId != null)
                {
                    // Replacing: make sure given prevRevID is current & find its sequence number:
                    if (docNumericID <= 0)
                    {
                        var msg = string.Format("No existing revision found with doc id: {0}", docId);
                        throw new CouchbaseLiteException(msg, StatusCode.NotFound);
                    }
                    var args = new [] { Convert.ToString(docNumericID), prevRevId };
                    var additionalWhereClause = String.Empty;
                    if (!allowConflict)
                    {
                        additionalWhereClause = "AND current=1";
                    }
                    cursor = StorageEngine.RawQuery(
                        "SELECT sequence FROM revs WHERE doc_id=? AND revid=? "
                        + additionalWhereClause + " LIMIT 1", args);
                    if (cursor.MoveToNext())
                    {
                        parentSequence = cursor.GetLong(0);
                    }
                    if (parentSequence == 0)
                    {
                        // Not found: either a 404 or a 409, depending on whether there is any current revision
                        if (!allowConflict && ExistsDocumentWithIDAndRev(docId, null))
                        {
                            var msg = string.Format("Conflicts not allowed and there is already an existing doc with id: {0}", docId);
                            throw new CouchbaseLiteException(msg, StatusCode.Conflict);
                        }
                        else
                        {
                            var msg = string.Format("No existing revision found with doc id: {0}", docId);
                            throw new CouchbaseLiteException(msg, StatusCode.NotFound);
                        }
                    }

                    if (Validations != null && Validations.Count > 0)
                    {
                        // Fetch the previous revision and validate the new one against it:
                        var prevRev = new RevisionInternal(docId, prevRevId, false, this);
                        ValidateRevision(oldRev, prevRev);
                    }
                    // Make replaced rev non-current:
                    var updateContent = new ContentValues();
                    updateContent["current"] = 0;

                    StorageEngine.Update("revs", updateContent, "sequence=" + parentSequence, null);
                }
                else
                {
                    // Inserting first revision.
                    if (deleted && (docId != null))
                    {
                        // Didn't specify a revision to delete: 404 or a 409, depending
                        if (ExistsDocumentWithIDAndRev(docId, null))
                        {
                            throw new CouchbaseLiteException(StatusCode.Conflict);
                        }
                        else
                        {
                            throw new CouchbaseLiteException(StatusCode.NotFound);
                        }
                    }
                    // Validate:
                    ValidateRevision(oldRev, null);
                    if (docId != null)
                    {
                        // Inserting first revision, with docID given (PUT):
                        if (docNumericID <= 0)
                        {
                            // Doc doesn't exist at all; create it:
                            docNumericID = InsertDocumentID(docId);
                            if (docNumericID <= 0)
                            {
                                return null;
                            }
                        }
                        else
                        {
                            // Doc ID exists; check whether current winning revision is deleted:
                            if (oldWinnerWasDeletion)
                            {
                                prevRevId = oldWinningRevID;
                                parentSequence = GetSequenceOfDocument(docNumericID, prevRevId, false);
                            }
                            else
                            {
                                if (oldWinningRevID != null)
                                {
                                    // The current winning revision is not deleted, so this is a conflict
                                    throw new CouchbaseLiteException(StatusCode.Conflict);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Inserting first revision, with no docID given (POST): generate a unique docID:
                        docId = Database.GenerateDocumentId();
                        docNumericID = InsertDocumentID(docId);
                        if (docNumericID <= 0)
                        {
                            return null;
                        }
                    }
                }
                // There may be a conflict if (a) the document was already in conflict, or
                // (b) a conflict is created by adding a non-deletion child of a non-winning rev.
                inConflict = wasConflicted 
                          || (!deleted && prevRevId != null && oldWinningRevID != null && !prevRevId.Equals(oldWinningRevID));
                // PART II: In which insertion occurs...
                // Get the attachments:
                var attachments = GetAttachmentsFromRevision(oldRev);
                // Bump the revID and update the JSON:
                var newRevId = GenerateNextRevisionID(prevRevId);
                IEnumerable<byte> data = null;


                if(oldRev.GetProperties() != null && oldRev.GetProperties().Any()) {
                    data = EncodeDocumentJSON(oldRev);
                    if (data == null)
                    {
                        // bad or missing json
                        throw new CouchbaseLiteException(StatusCode.BadRequest);
                    }
                }
                else 
                {
                    data = Encoding.UTF8.GetBytes("{}"); // NOTE.ZJG: Confirm w/ Traun. This prevents a null reference exception in call to InsertRevision below.
                }

                newRev = oldRev.CopyWithDocID(docId, newRevId);
                StubOutAttachmentsInRevision(attachments, newRev);
                // Now insert the rev itself:
                var newSequence = InsertRevision(newRev, docNumericID, parentSequence, true, data);
                if (newSequence == 0)
                {
                    return null;
                }
                // Store any attachments:
                if (attachments != null)
                {
                    ProcessAttachmentsForRevision(attachments, newRev, parentSequence);
                }

                // Figure out what the new winning rev ID is:
                winningRev = Winner(docNumericID, oldWinningRevID, oldWinnerWasDeletion, newRev);

                 // Success!
                if (deleted)
                {
                    resultStatus.SetCode(StatusCode.Ok);
                }
                else
                {
                    resultStatus.SetCode(StatusCode.Created);
                }
            }
            catch (SQLException e1)
            {
                Log.E(Tag, "Error putting revision", e1);
                return null;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
                EndTransaction(resultStatus.IsSuccessful());

                if (!string.IsNullOrEmpty(docId))
                {
                    UnsavedRevisionDocumentCache.Remove(docId);
                }
            }

            // EPILOGUE: A change notification is sent...
            NotifyChange(newRev, winningRev, null, inConflict);
            return newRev;
        }

        internal RevisionInternal Winner(Int64 docNumericID, String oldWinningRevID, Boolean oldWinnerWasDeletion, RevisionInternal newRev)
        {
            if (oldWinningRevID == null)
            {
                return newRev;
            }
            var newRevID = newRev.GetRevId();
            if (!newRev.IsDeleted())
            {
                if (oldWinnerWasDeletion || RevisionInternal.CBLCompareRevIDs(newRevID, oldWinningRevID) > 0)
                {
                    return newRev;
                }
            }
            else
            {
                // this is now the winning live revision
                if (oldWinnerWasDeletion)
                {
                    if (RevisionInternal.CBLCompareRevIDs(newRevID, oldWinningRevID) > 0)
                    {
                        return newRev;
                    }
                }
                else
                {
                    // doc still deleted, but this beats previous deletion rev
                    // Doc was alive. How does this deletion affect the winning rev ID?
                    var outIsDeleted = new AList<bool>();
                    var outIsConflict = new AList<bool>();
                    var winningRevID = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict);

                    if (!winningRevID.Equals(oldWinningRevID))
                    {
                        if (winningRevID.Equals(newRev.GetRevId()))
                        {
                            return newRev;
                        }
                        else
                        {
                            var deleted = false;
                            var winningRev = new RevisionInternal(newRev.GetDocId(), winningRevID, deleted, this);
                            return winningRev;
                        }
                    }
                }
            }
            return null;
        }

        private Int64 GetSequenceOfDocument(Int64 docNumericId, String revId, Boolean onlyCurrent)
        {
            var result = -1L;
            Cursor cursor = null;
            try
            {
                var extraSql = (onlyCurrent ? "AND current=1" : string.Empty);
                var sql = string.Format("SELECT sequence FROM revs WHERE doc_id=? AND revid=? {0} LIMIT 1", extraSql);
                var args = new [] { string.Empty + docNumericId, revId };
                cursor = StorageEngine.RawQuery(sql, args);
                result = cursor.MoveToNext()
                             ? cursor.GetLong(0)
                             : 0;
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error getting getSequenceOfDocument", e);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return result;
        }

        internal void PostChangeNotifications()
        {
            // This is a 'while' instead of an 'if' because when we finish posting notifications, there
            // might be new ones that have arrived as a result of notification handlers making document
            // changes of their own (the replicator manager will do this.) So we need to check again.
            while (transactionLevel == 0 && open && !PostingChangeNotifications && ChangesToNotify.Count > 0)
            {
                try
                {
                    PostingChangeNotifications = true;

                    IList<DocumentChange> outgoingChanges = new AList<DocumentChange>();
                    foreach (var change in ChangesToNotify)
                    {
                        outgoingChanges.Add(change);
                    }
                    ChangesToNotify.Clear();

                    Boolean isExternal = false;
                    foreach (var change in outgoingChanges)
                    {
                        var document = GetDocument(change.DocumentId);
                        document.RevisionAdded(change);
                        if (change.SourceUrl != null)
                        {
                            isExternal = true;
                        }
                    }

                    var args = new DatabaseChangeEventArgs { 
                        Changes = outgoingChanges,
                        IsExternal = isExternal,
                        Source = this
                    } ;

                    var changeEvent = Changed;
                    if (changeEvent != null)
                        changeEvent(this, args);
                }
                catch (Exception e)
                {
                    Log.E(Tag, " got exception posting change notifications", e);
                }
                finally
                {
                    PostingChangeNotifications = false;
                }
            }
        }

        internal void NotifyChange(RevisionInternal rev, RevisionInternal winningRev, Uri source, bool inConflict)
        {
            var change = new DocumentChange(rev, winningRev, inConflict, source);
            ChangesToNotify.Add(change);

            PostChangeNotifications();
        }

        /// <summary>
        /// Given a newly-added revision, adds the necessary attachment rows to the sqliteDb and
        /// stores inline attachments into the blob store.
        /// </summary>
        /// <remarks>
        /// Given a newly-added revision, adds the necessary attachment rows to the sqliteDb and
        /// stores inline attachments into the blob store.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void ProcessAttachmentsForRevision(IDictionary<string, AttachmentInternal> attachments, RevisionInternal rev, long parentSequence)
        {
            Debug.Assert((rev != null));
            var newSequence = rev.GetSequence();
            Debug.Assert((newSequence > parentSequence));
            var generation = rev.GetGeneration();
            Debug.Assert((generation > 0));

            // If there are no attachments in the new rev, there's nothing to do:
            IDictionary<string, object> revAttachments = null;
            var properties = rev.GetProperties ();
            if (properties != null)
            {
                revAttachments = properties.Get("_attachments").AsDictionary<string, object>();
            }

            if (revAttachments == null || revAttachments.Count == 0 || rev.IsDeleted())
            {
                return;
            }

            foreach (string name in revAttachments.Keys)
            {
                var attachment = attachments.Get(name);
                if (attachment != null)
                {
                    // Determine the revpos, i.e. generation # this was added in. Usually this is
                    // implicit, but a rev being pulled in replication will have it set already.
                    if (attachment.GetRevpos() == 0)
                    {
                        attachment.SetRevpos(generation);
                    }
                    else
                    {
                        if (attachment.GetRevpos() > generation)
                        {
                            Log.W(Tag, string.Format("Attachment {0} {1} has unexpected revpos {2}, setting to {3}", rev, name, attachment.GetRevpos(), generation));
                            attachment.SetRevpos(generation);
                        }
                    }
                    // Finally insert the attachment:
                    InsertAttachmentForSequence(attachment, newSequence);
                }
                else
                {
                    // It's just a stub, so copy the previous revision's attachment entry:
                    //? Should I enforce that the type and digest (if any) match?
                    CopyAttachmentNamedFromSequenceToSequence(name, parentSequence, newSequence);
                }
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void CopyAttachmentNamedFromSequenceToSequence(string name, long fromSeq, long toSeq)
        {
            Debug.Assert((name != null));
            Debug.Assert((toSeq > 0));

            if (fromSeq < 0)
            {
                throw new CouchbaseLiteException(StatusCode.NotFound);
            }

            Cursor cursor = null;
            var args = new [] { Convert.ToString(toSeq), name, Convert.ToString(fromSeq), name };

            try
            {
                StorageEngine.ExecSQL("INSERT INTO attachments (sequence, filename, key, type, length, revpos) "
                    + "SELECT ?, ?, key, type, length, revpos FROM attachments " + "WHERE sequence=? AND filename=?", args);
                cursor = StorageEngine.RawQuery("SELECT changes()");
                cursor.MoveToNext();

                int rowsUpdated = cursor.GetInt(0);
                if (rowsUpdated == 0)
                {
                    // Oops. This means a glitch in our attachment-management or pull code,
                    // or else a bug in the upstream server.
                    Log.W(Tag, "Can't find inherited attachment " + name 
                          + " from seq# " + Convert.ToString(fromSeq) + " to copy to " + Convert.ToString(toSeq));
                    throw new CouchbaseLiteException(StatusCode.NotFound);
                }
                else
                {
                    return;
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error copying attachment", e);
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void InsertAttachmentForSequence(AttachmentInternal attachment, long sequence)
        {
            InsertAttachmentForSequenceWithNameAndType(sequence, attachment.GetName(), attachment.GetContentType(), attachment.GetRevpos(), attachment.GetBlobKey());
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void InsertAttachmentForSequenceWithNameAndType(InputStream contentStream, long sequence, string name, string contentType, int revpos)
        {
            Debug.Assert((sequence > 0));
            Debug.Assert((name != null));

            var key = new BlobKey();
            if (!Attachments.StoreBlobStream(contentStream, key))
            {
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
            InsertAttachmentForSequenceWithNameAndType(sequence, name, contentType, revpos, key);
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void InsertAttachmentForSequenceWithNameAndType(long sequence, string name, string contentType, int revpos, BlobKey key)
        {
            try
            {
                var args = new ContentValues();
                args["sequence"] = sequence;
                args["filename"] = name;
                if (key != null)
                {
                    args.Put("key", key.GetBytes());
                    args.Put("length", Attachments.GetSizeOfBlob(key));
                }
                args["type"] = contentType;
                args["revpos"] = revpos;
                var result = StorageEngine.Insert("attachments", null, args);
                if (result == -1)
                {
                    var msg = "Insert attachment failed (returned -1)";
                    Log.E(Tag, msg);
                    throw new CouchbaseLiteException(msg, StatusCode.InternalServerError);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error inserting attachment", e);
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void InstallAttachment(AttachmentInternal attachment, IDictionary<String, Object> attachInfo)
        {
            var digest = (string)attachInfo.Get("digest");
            if (digest == null)
            {
                throw new CouchbaseLiteException(StatusCode.BadAttachment);
            }

            if (PendingAttachmentsByDigest != null && PendingAttachmentsByDigest.ContainsKey(digest))
            {
                var writer = PendingAttachmentsByDigest.Get(digest);
                try
                {
                    var blobStoreWriter = writer;
                    blobStoreWriter.Install();
                    attachment.SetBlobKey(blobStoreWriter.GetBlobKey());
                    attachment.SetLength(blobStoreWriter.GetLength());
                }
                catch (Exception e)
                {
                    throw new CouchbaseLiteException(e, StatusCode.StatusAttachmentError);
                }
            }
        }

        internal Int64 InsertRevision(RevisionInternal rev, long docNumericID, long parentSequence, Boolean current, IEnumerable<byte> data)
        {
            var rowId = 0L;
            try
            {
                var args = new ContentValues();
                args["doc_id"] = docNumericID;
                args.Put("revid", rev.GetRevId());
                if (parentSequence != 0)
                {
                    args["parent"] = parentSequence;
                }

                args["current"] = current;
                args.Put("deleted", rev.IsDeleted());

                if (data != null)
                {
                    args.Put("json", data.ToArray());
                }

                rowId = StorageEngine.Insert("revs", null, args);
                rev.SetSequence(rowId);
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error inserting revision", e);
            }
            return rowId;
        }

        internal void StubOutAttachmentsInRevision(IDictionary<String, AttachmentInternal> attachments, RevisionInternal rev)
        {
            var properties = rev.GetProperties();
            var attachmentProps = properties.Get("_attachments");
            if (attachmentProps != null)
            {
                var nuAttachments = new Dictionary<string, object>();
                foreach (var kvp in attachmentProps.AsDictionary<string,object>())
                {
                    var attachmentValue = kvp.Value.AsDictionary<string,object>();
                    if (attachmentValue.ContainsKey("follows") || attachmentValue.ContainsKey("data"))
                    {
                        attachmentValue.Remove("follows");
                        attachmentValue.Remove("data");

                        attachmentValue["stub"] = true;
                        if (attachmentValue.Get("revpos") == null)
                        {
                            attachmentValue.Put("revpos", rev.GetGeneration());
                        }

                        var attachmentObject = attachments.Get(kvp.Key);
                        if (attachmentObject != null)
                        {
                            attachmentValue.Put("length", attachmentObject.GetLength());
                            if (attachmentObject.GetBlobKey() != null)
                            {
                                attachmentValue.Put("digest", attachmentObject.GetBlobKey().Base64Digest());
                            }
                        }
                    }
                    nuAttachments[kvp.Key] = attachmentValue;
                }

                properties["_attachments"] = nuAttachments;  
            }
        }

        internal static void StubOutAttachmentsInRevBeforeRevPos(RevisionInternal rev, long minRevPos, bool attachmentsFollow)
        {
            if (minRevPos <= 1 && !attachmentsFollow)
            {
                return;
            }

            rev.MutateAttachments((name, attachment) =>
            {
                var revPos = 0L;
                if (attachment.ContainsKey("revpos"))
                {
                    revPos = Convert.ToInt64(attachment["revpos"]);
                }

                var includeAttachment = (revPos == 0 || revPos >= minRevPos);
                var stubItOut = !includeAttachment && (!attachment.ContainsKey("stub") || (bool)attachment["stub"] == false);
                var addFollows = includeAttachment && attachmentsFollow && (!attachment.ContainsKey("follows") || (bool)attachment["follows"] == false);

                if (!stubItOut && !addFollows)
                {
                    return attachment; // no change
                }

                // Need to modify attachment entry
                var editedAttachment = new Dictionary<string, object>(attachment);
                editedAttachment.Remove("data");

                if (stubItOut)
                {
                    // ...then remove the 'data' and 'follows' key:
                    editedAttachment.Remove("follows");
                    editedAttachment["stub"] = true;
                    Log.V(Tag, String.Format("Stubbed out attachment {0}: revpos {1} < {2}", rev, revPos, minRevPos));
                }
                else if (addFollows)
                {
                    editedAttachment.Remove("stub");
                    editedAttachment["follows"] = true;
                    Log.V(Tag, String.Format("Added 'follows' for attachment {0}: revpos {1} >= {2}", rev, revPos, minRevPos));
                }

                return editedAttachment;
            });
        }

        /// <summary>INSERTION:</summary>
        internal IEnumerable<Byte> EncodeDocumentJSON(RevisionInternal rev)
        {
            var origProps = rev.GetProperties();
            if (origProps == null)
            {
                return null;
            }
            // Don't allow any "_"-prefixed keys. Known ones we'll ignore, unknown ones are an error.
            var properties = new Dictionary<String, Object>(origProps.Count);
            foreach (var key in origProps.Keys)
            {
                if (key.StartsWith("_", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!KnownSpecialKeys.Contains(key))
                    {
                        Log.E(Tag, "Database: Invalid top-level key '" + key + "' in document to be inserted");
                        return null;
                    }
                }
                else
                {
                    properties.Put(key, origProps.Get(key));
                }
            }
            IEnumerable<byte> json = null;
            try
            {
                json = Manager.GetObjectMapper().WriteValueAsBytes(properties);
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error serializing " + rev + " to JSON", e);
            }
            return json;
        }

        /// <summary>
        /// Given a revision, read its _attachments dictionary (if any), convert each attachment to a
        /// AttachmentInternal object, and return a dictionary mapping names-&gt;CBL_Attachments.
        /// </summary>
        /// <remarks>
        /// Given a revision, read its _attachments dictionary (if any), convert each attachment to a
        /// AttachmentInternal object, and return a dictionary mapping names-&gt;CBL_Attachments.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IDictionary<String, AttachmentInternal> GetAttachmentsFromRevision(RevisionInternal rev)
        {
            var revAttachments = rev.GetPropertyForKey("_attachments").AsDictionary<string, object>();
            if (revAttachments == null || revAttachments.Count == 0 || rev.IsDeleted())
            {
                return new Dictionary<string, AttachmentInternal>();
            }

            var attachments = new Dictionary<string, AttachmentInternal>();
            foreach (var name in revAttachments.Keys)
            {
                var attachInfo = revAttachments.Get(name).AsDictionary<string, object>();
                var contentType = (string)attachInfo.Get("content_type");
                var attachment = new AttachmentInternal(name, contentType);
                var newContentBase64 = (string)attachInfo.Get("data");
                if (newContentBase64 != null)
                {
                    // If there's inline attachment data, decode and store it:
                    byte[] newContents;
                    try
                    {
                        newContents = StringUtils.ConvertFromUnpaddedBase64String (newContentBase64);
                    }
                    catch (IOException e)
                    {
                        throw new CouchbaseLiteException(e, StatusCode.BadEncoding);
                    }
                    attachment.SetLength(newContents.Length);
                    var outBlobKey = new BlobKey();
                    var storedBlob = Attachments.StoreBlob(newContents, outBlobKey);
                    attachment.SetBlobKey(outBlobKey);
                    if (!storedBlob)
                    {
                        throw new CouchbaseLiteException(StatusCode.StatusAttachmentError);
                    }
                }
                else
                {
                    if (attachInfo.ContainsKey("follows") && ((bool)attachInfo.Get("follows")))
                    {
                        // "follows" means the uploader provided the attachment in a separate MIME part.
                        // This means it's already been registered in _pendingAttachmentsByDigest;
                        // I just need to look it up by its "digest" property and install it into the store:
                        InstallAttachment(attachment, attachInfo);
                    }
                    else
                    {
                        // This item is just a stub; validate and skip it
                        if (((bool)attachInfo.Get("stub")) == false)
                        {
                            throw new CouchbaseLiteException("Expected this attachment to be a stub", StatusCode.
                                                             BadAttachment);
                        }

                        var revPos = Convert.ToInt64(attachInfo.Get("revpos"));
                        if (revPos <= 0)
                        {
                            throw new CouchbaseLiteException("Invalid revpos: " + revPos, StatusCode.BadAttachment
                                                            );
                        }

                        continue;
                    }
                }
                // Handle encoded attachment:
                string encodingStr = (string)attachInfo.Get("encoding");
                if (encodingStr != null && encodingStr.Length > 0)
                {
                    if (Runtime.EqualsIgnoreCase(encodingStr, "gzip"))
                    {
                        attachment.SetEncoding(AttachmentInternal.AttachmentEncoding.AttachmentEncodingGZIP
                                              );
                    }
                    else
                    {
                        throw new CouchbaseLiteException("Unnkown encoding: " + encodingStr, StatusCode.BadEncoding
                                                        );
                    }
                    attachment.SetEncodedLength(attachment.GetLength());
                    if (attachInfo.ContainsKey("length"))
                    {
                        attachment.SetLength((long)attachInfo.Get("length"));
                    }
                }
                if (attachInfo.ContainsKey("revpos"))
                {
                    var revpos = Convert.ToInt32(attachInfo.Get("revpos"));
                    attachment.SetRevpos(revpos);
                }
                else
                {
                    attachment.SetRevpos(1);
                }
                attachments[name] = attachment;
            }
            return attachments;
        }

        internal String GenerateNextRevisionID(string revisionId)
        {
            // Revision IDs have a generation count, a hyphen, and a UUID.
            var generation = 0;
            if (revisionId != null)
            {
                generation = RevisionInternal.GenerationFromRevID(revisionId);
                if (generation == 0)
                {
                    return null;
                }
            }
            var digest = Misc.TDCreateUUID();
            // TODO: Generate canonical digest of body
            return Sharpen.Extensions.ToString(generation + 1) + "-" + digest;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal RevisionInternal LoadRevisionBody(RevisionInternal rev, EnumSet<TDContentOptions> contentOptions)
        {
            if (rev.GetBody() != null && contentOptions == EnumSet.NoneOf<TDContentOptions>() && rev.GetSequence() != 0)
            {
                return rev;
            }

            Debug.Assert(((rev.GetDocId() != null) && (rev.GetRevId() != null)));
            Cursor cursor = null;
            var result = new Status(StatusCode.NotFound);
            try
            {
                // TODO: on ios this query is:
                // TODO: "SELECT sequence, json FROM revs WHERE doc_id=@ AND revid=@ LIMIT 1"
                var sql = "SELECT sequence, json FROM revs, docs WHERE revid=? AND docs.docid=? AND revs.doc_id=docs.doc_id LIMIT 1";
                var args = new [] { rev.GetRevId(), rev.GetDocId() };

                cursor = StorageEngine.RawQuery(sql, CommandBehavior.SequentialAccess, args);
                if (cursor.MoveToNext())
                {
                    result.SetCode(StatusCode.Ok);
                    rev.SetSequence(cursor.GetLong(0));
                    ExpandStoredJSONIntoRevisionWithAttachments(cursor.GetBlob(1), rev, contentOptions);
                }
            }
            catch (SQLException e)
            {
                Log.E(Tag, "Error loading revision body", e);
                throw new CouchbaseLiteException(StatusCode.InternalServerError);
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            if (result.GetCode() == StatusCode.NotFound)
            {
                throw new CouchbaseLiteException(result.GetCode());
            }
            return rev;
        }

        internal Int64 InsertDocumentID(String docId)
        {
            var rowId = -1L;
            try
            {
                ContentValues args = new ContentValues();
                args["docid"] = docId;
                rowId = StorageEngine.Insert("docs", null, args);
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error inserting document id", e);
            }
            return rowId;
        }

        internal Boolean ExistsDocumentWithIDAndRev(String docId, String revId)
        {
            return GetDocumentWithIDAndRev(docId, revId, EnumSet.Of(TDContentOptions.TDNoBody)) != null;
        }

        internal Int32 FindMissingRevisions(RevisionList touchRevs)
        {
            var numRevisionsRemoved = 0;
            if (touchRevs.Count == 0)
            {
                return numRevisionsRemoved;
            }

            var quotedDocIds = JoinQuoted(touchRevs.GetAllDocIds());
            var quotedRevIds = JoinQuoted(touchRevs.GetAllRevIds());
            var sql = "SELECT docid, revid FROM revs, docs " + "WHERE docid IN (" + quotedDocIds
                         + ") AND revid in (" + quotedRevIds + ")" + " AND revs.doc_id == docs.doc_id";
            Cursor cursor = null;
            try
            {
                cursor = StorageEngine.RawQuery(sql);
                cursor.MoveToNext();
                while (!cursor.IsAfterLast())
                {
                    var rev = touchRevs.RevWithDocIdAndRevId(cursor.GetString(0), cursor.GetString(1));
                    if (rev != null)
                    {
                        touchRevs.Remove(rev);
                        numRevisionsRemoved += 1;
                    }
                    cursor.MoveToNext();
                }
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return numRevisionsRemoved;
        }

        /// <summary>DOCUMENT & REV IDS:</summary>
        internal Boolean IsValidDocumentId(string id)
        {
            // http://wiki.apache.org/couchdb/HTTP_Document_API#Documents
            if (String.IsNullOrEmpty (id)) {
                return false;
            }

            return id [0] != '_' || id.StartsWith ("_design/", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>VALIDATION</summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal void ValidateRevision(RevisionInternal newRev, RevisionInternal oldRev)
        {
            if (Validations == null || Validations.Count == 0)
            {
                return;
            }

            var context = new ValidationContext(this, oldRev, newRev);
            var publicRev = new SavedRevision(this, newRev);
            foreach (var validationName in Validations.Keys)
            {
                var validation = GetValidation(validationName);
                validation(publicRev, context);
                if (context.RejectMessage != null)
                {
                    throw new CouchbaseLiteException(context.RejectMessage, StatusCode.Forbidden);
                }
            }
        }

        internal Boolean Initialize(String statements)
        {
            try
            {
                foreach (string statement in statements.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    StorageEngine.ExecSQL(statement);
                }
            }
            catch (SQLException)
            {
                Close();
                return false;
            }
            return true;
        }

        internal String AttachmentStorePath {
            get {
                var attachmentStorePath = Path;
                int lastDotPosition = attachmentStorePath.LastIndexOf(".", StringComparison.InvariantCultureIgnoreCase);
                if (lastDotPosition > 0)
                {
                    attachmentStorePath = Runtime.Substring(attachmentStorePath, 0, lastDotPosition);
                }
                attachmentStorePath = attachmentStorePath + FilePath.separator + "attachments";
                return attachmentStorePath;
            }
        }

        internal Boolean Open()
        {
            if (open)
            {
                return true;
            }

            // Create the storage engine.
            StorageEngine = SQLiteStorageEngineFactory.CreateStorageEngine();

            // Try to open the storage engine and stop if we fail.
            if (StorageEngine == null || !StorageEngine.Open(Path))
            {
                string msg = "Unable to create a storage engine, fatal error";
                Log.E(Tag, msg);
                throw new CouchbaseLiteException(msg);
            }

            // Stuff we need to initialize every time the sqliteDb opens:
            if (!Initialize("PRAGMA foreign_keys = ON;"))
            {
                Log.E(Tag, "Error turning on foreign keys");
                return false;
            }

            // Check the user_version number we last stored in the sqliteDb:
            var dbVersion = StorageEngine.GetVersion();

            // Incompatible version changes increment the hundreds' place:
            if (dbVersion >= 100)
            {
                Log.W(Tag, "Database: Database version (" + dbVersion + ") is newer than I know how to work with");
                StorageEngine.Close();
                return false;
            }

            if (dbVersion < 1)
            {
                // First-time initialization:
                // (Note: Declaring revs.sequence as AUTOINCREMENT means the values will always be
                // monotonically increasing, never reused. See <http://www.sqlite.org/autoinc.html>)
                if (!Initialize(Schema))
                {
                    StorageEngine.Close();
                    return false;
                }

                dbVersion = 3;
            }

            if (dbVersion < 2)
            {
                // Version 2: added attachments.revpos
                var upgradeSql = "ALTER TABLE attachments ADD COLUMN revpos INTEGER DEFAULT 0; PRAGMA user_version = 2";

                if (!Initialize(upgradeSql))
                {
                    StorageEngine.Close();
                    return false;
                }
                dbVersion = 2;
            }

            if (dbVersion < 3)
            {
                var upgradeSql = "CREATE TABLE localdocs ( " + "docid TEXT UNIQUE NOT NULL, " 
                                    + "revid TEXT NOT NULL, " + "json BLOB); " + "CREATE INDEX localdocs_by_docid ON localdocs(docid); "
                                    + "PRAGMA user_version = 3";
                if (!Initialize(upgradeSql))
                {
                    StorageEngine.Close();
                    return false;
                }
                dbVersion = 3;
            }

            if (dbVersion < 4)
            {
                var upgradeSql = "CREATE TABLE info ( " + "key TEXT PRIMARY KEY, " + "value TEXT); "
                                    + "INSERT INTO INFO (key, value) VALUES ('privateUUID', '" + Misc.TDCreateUUID(
                                       ) + "'); " + "INSERT INTO INFO (key, value) VALUES ('publicUUID',  '" + Misc.TDCreateUUID
                                    () + "'); " + "PRAGMA user_version = 4";
                if (!Initialize(upgradeSql))
                {
                    StorageEngine.Close();
                    return false;
                }
            }

            try
            {
                Attachments = new BlobStore(AttachmentStorePath);
            }
            catch (ArgumentException e)
            {
                Log.E(Tag, "Could not initialize attachment store", e);
                StorageEngine.Close();
                return false;
            }

            open = true;

            return true;
        }

        internal Boolean Close()
        {
            if (!open)
            {
                return false;
            }
            if (views != null)
            {
                foreach (View view in views.Values)
                {
                    view.DatabaseClosing();
                }
            }
            views = null;
            if (ActiveReplicators != null)
            {
                // 
                var activeReplicators = new Replication[ActiveReplicators.Count];
                ActiveReplicators.CopyTo(activeReplicators, 0);
                foreach (Replication replicator in activeReplicators)
                {
                    replicator.DatabaseClosing();
                }
                ActiveReplicators = null;
            }
            if (StorageEngine != null && StorageEngine.IsOpen)
            {
                StorageEngine.Close();
            }
            open = false;
            transactionLevel = 0;
            return true;
        }

        internal void AddReplication(Replication replication)
        {
            AllReplicators.AddItem(replication);
        }

        internal void ForgetReplication(Replication replication)
        {
            AllReplicators.Remove(replication);
        }

        internal void AddActiveReplication(Replication replication)
        {
            ActiveReplicators.AddItem(replication);
            replication.Changed += (sender, e) => 
            {
                if (!e.Source.IsRunning && ActiveReplicators != null)
                {
                    ActiveReplicators.Remove(e.Source);
                }
            };
        }

        internal int PruneRevsToMaxDepth(int maxDepth)
        {
            int outPruned = 0;
            bool shouldCommit = false;
            IDictionary<long, int> toPrune = new Dictionary<long, int>();
            if (maxDepth == 0)
            {
                maxDepth = MaxRevTreeDepth;
            }

            // First find which docs need pruning, and by how much:
            Cursor cursor = null;
            var sql = "SELECT doc_id, MIN(revid), MAX(revid) FROM revs GROUP BY doc_id";
            long docNumericID = -1;
            var minGen = 0;
            var maxGen = 0;
            try
            {
                cursor = StorageEngine.RawQuery(sql);
                while (cursor.MoveToNext())
                {
                    docNumericID = cursor.GetLong(0);
                    var minGenRevId = cursor.GetString(1);
                    var maxGenRevId = cursor.GetString(2);
                    minGen = RevisionInternal.GenerationFromRevID(minGenRevId);
                    maxGen = RevisionInternal.GenerationFromRevID(maxGenRevId);
                    if ((maxGen - minGen + 1) > maxDepth)
                    {
                        toPrune.Put(docNumericID, (maxGen - minGen));
                    }
                }

                BeginTransaction();

                if (toPrune.Count == 0)
                {
                    return 0;
                }

                foreach (long id in toPrune.Keys)
                {
                    string minIDToKeep = String.Format("{0}-", (toPrune.Get(id) + 1));
                    string[] deleteArgs = new string[] { System.Convert.ToString(docNumericID), minIDToKeep };
                    int rowsDeleted = StorageEngine.Delete("revs", "doc_id=? AND revid < ? AND current=0", deleteArgs);
                    outPruned += rowsDeleted;
                }
                shouldCommit = true;
            }
            catch (Exception e)
            {
                throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
            }
            finally
            {
                EndTransaction(shouldCommit);
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
            return outPruned;
        }


    #endregion
    
    #region EventArgs Subclasses

        ///
        /// <summary>The event raised when a <see cref="Couchbase.Lite.Database"/> changes</summary>
        ///
        public class DatabaseChangeEventArgs : EventArgs {
            //Properties
            /// <summary>
            /// Gets the <see cref="Couchbase.Lite.Database"/> that raised the event.
            /// </summary>
            /// <value>The <see cref="Couchbase.Lite.Database"/> that raised the event.</value>
            public Database Source { get; internal set; }

            /// <summary>
            /// Returns true if the change was not made by a Document belonging to this Database 
            /// (e.g. it came from another process or from a pull Replication), otherwise false.
            /// </summary>
            /// <value>true if the change was not made by a Document belonging to this Database 
            /// (e.g. it came from another process or from a pull Replication), otherwise false</value>
            public Boolean IsExternal { get; internal set; }

            /// <summary>
            /// Gets the DocumentChange details for the Documents that caused the Database change.
            /// </summary>
            /// <value>The DocumentChange details for the Documents that caused the Database change.</value>
            public IEnumerable<DocumentChange> Changes { get; internal set; }
        }

    #endregion
    
    }

    #region Global Delegates

    /// <summary>
    /// A delegate that can validate a key/value change.
    /// </summary>
    public delegate Boolean ValidateChangeDelegate(String key, Object oldValue, Object newValue);

    /// <summary>
    /// A delegate that can be run asynchronously on a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate void RunAsyncDelegate(Database database);

    /// <summary>
    /// A delegate that can be used to accept/reject new <see cref="Couchbase.Lite.Revision"/>s being added to a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate Boolean ValidateDelegate(Revision newRevision, IValidationContext context);

    /// <summary>
    /// A delegate that can be used to include/exclude <see cref="Couchbase.Lite.Revision"/>s during push <see cref="Couchbase.Lite.Replication"/>.
    /// </summary>
    public delegate Boolean FilterDelegate(SavedRevision revision, Dictionary<String, Object> filterParams);

    /// <summary>
    /// A delegate that can be invoked to compile source code into a <see cref="FilterDelegate"/>.
    /// </summary>
    public delegate FilterDelegate CompileFilterDelegate(String source, String language);

    /// <summary>
    /// A delegate that can be run in a transaction on a <see cref="Couchbase.Lite.Database"/>.
    /// </summary>
    public delegate Boolean RunInTransactionDelegate();

    #endregion
}

