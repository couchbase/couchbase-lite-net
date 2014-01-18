/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>A CouchbaseLite database.</summary>
	/// <remarks>A CouchbaseLite database.</remarks>
	public class Database
	{
		private const int MaxDocCacheSize = 50;

		private static ReplicationFilterCompiler filterCompiler;

		private string path;

		private string name;

		private SQLiteStorageEngine database;

		private bool open = false;

		private int transactionLevel = 0;

		public const string Tag = "Database";

		public const string TagSql = "CBLSQL";

		private IDictionary<string, View> views;

		private IDictionary<string, ReplicationFilter> filters;

		private IDictionary<string, Validator> validations;

		private IDictionary<string, BlobStoreWriter> pendingAttachmentsByDigest;

		private IList<Replication> activeReplicators;

		private BlobStore attachments;

		private Manager manager;

		private IList<Database.ChangeListener> changeListeners;

		private LruCache<string, Document> docCache;

		private long startTime;

		public static int kBigAttachmentLength = (16 * 1024);

		/// <summary>Options for what metadata to include in document bodies</summary>
		public enum TDContentOptions
		{
			TDIncludeAttachments,
			TDIncludeConflicts,
			TDIncludeRevs,
			TDIncludeRevsInfo,
			TDIncludeLocalSeq,
			TDNoBody,
			TDBigAttachmentsFollow
		}

		private static readonly ICollection<string> KnownSpecialKeys;

		static Database()
		{
			// Length that constitutes a 'big' attachment
			KnownSpecialKeys = new HashSet<string>();
			KnownSpecialKeys.AddItem("_id");
			KnownSpecialKeys.AddItem("_rev");
			KnownSpecialKeys.AddItem("_attachments");
			KnownSpecialKeys.AddItem("_deleted");
			KnownSpecialKeys.AddItem("_revisions");
			KnownSpecialKeys.AddItem("_revs_info");
			KnownSpecialKeys.AddItem("_conflicts");
			KnownSpecialKeys.AddItem("_deleted_conflicts");
		}

		public const string Schema = string.Empty + "CREATE TABLE docs ( " + "        doc_id INTEGER PRIMARY KEY, "
			 + "        docid TEXT UNIQUE NOT NULL); " + "    CREATE INDEX docs_docid ON docs(docid); "
			 + "    CREATE TABLE revs ( " + "        sequence INTEGER PRIMARY KEY AUTOINCREMENT, "
			 + "        doc_id INTEGER NOT NULL REFERENCES docs(doc_id) ON DELETE CASCADE, "
			 + "        revid TEXT NOT NULL, " + "        parent INTEGER REFERENCES revs(sequence) ON DELETE SET NULL, "
			 + "        current BOOLEAN, " + "        deleted BOOLEAN DEFAULT 0, " + "        json BLOB); "
			 + "    CREATE INDEX revs_by_id ON revs(revid, doc_id); " + "    CREATE INDEX revs_current ON revs(doc_id, current); "
			 + "    CREATE INDEX revs_parent ON revs(parent); " + "    CREATE TABLE localdocs ( "
			 + "        docid TEXT UNIQUE NOT NULL, " + "        revid TEXT NOT NULL, " + "        json BLOB); "
			 + "    CREATE INDEX localdocs_by_docid ON localdocs(docid); " + "    CREATE TABLE views ( "
			 + "        view_id INTEGER PRIMARY KEY, " + "        name TEXT UNIQUE NOT NULL,"
			 + "        version TEXT, " + "        lastsequence INTEGER DEFAULT 0); " + "    CREATE INDEX views_by_name ON views(name); "
			 + "    CREATE TABLE maps ( " + "        view_id INTEGER NOT NULL REFERENCES views(view_id) ON DELETE CASCADE, "
			 + "        sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE, "
			 + "        key TEXT NOT NULL COLLATE JSON, " + "        value TEXT); " + "    CREATE INDEX maps_keys on maps(view_id, key COLLATE JSON); "
			 + "    CREATE TABLE attachments ( " + "        sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE, "
			 + "        filename TEXT NOT NULL, " + "        key BLOB NOT NULL, " + "        type TEXT, "
			 + "        length INTEGER NOT NULL, " + "        revpos INTEGER DEFAULT 0); " +
			 "    CREATE INDEX attachments_by_sequence on attachments(sequence, filename); "
			 + "    CREATE TABLE replicators ( " + "        remote TEXT NOT NULL, " + "        push BOOLEAN, "
			 + "        last_sequence TEXT, " + "        UNIQUE (remote, push)); " + "    PRAGMA user_version = 3";

		// at the end, update user_version
		/// <summary>Returns the currently registered filter compiler (nil by default).</summary>
		/// <remarks>Returns the currently registered filter compiler (nil by default).</remarks>
		[InterfaceAudience.Public]
		public static ReplicationFilterCompiler GetFilterCompiler()
		{
			return filterCompiler;
		}

		/// <summary>Registers an object that can compile source code into executable filter blocks.
		/// 	</summary>
		/// <remarks>Registers an object that can compile source code into executable filter blocks.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public static void SetFilterCompiler(ReplicationFilterCompiler filterCompiler)
		{
			Couchbase.Lite.Database.filterCompiler = filterCompiler;
		}

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		public Database(string path, Manager manager)
		{
			System.Diagnostics.Debug.Assert((path.StartsWith("/")));
			//path must be absolute
			this.path = path;
			this.name = FileDirUtils.GetDatabaseNameFromPath(path);
			this.manager = manager;
			this.changeListeners = new AList<Database.ChangeListener>();
			this.docCache = new LruCache<string, Document>(MaxDocCacheSize);
			this.startTime = Runtime.CurrentTimeMillis();
		}

		/// <summary>Get the database's name.</summary>
		/// <remarks>Get the database's name.</remarks>
		[InterfaceAudience.Public]
		public virtual string GetName()
		{
			return name;
		}

		/// <summary>The database manager that owns this database.</summary>
		/// <remarks>The database manager that owns this database.</remarks>
		[InterfaceAudience.Public]
		public virtual Manager GetManager()
		{
			return manager;
		}

		/// <summary>The number of documents in the database.</summary>
		/// <remarks>The number of documents in the database.</remarks>
		[InterfaceAudience.Public]
		public virtual int GetDocumentCount()
		{
			string sql = "SELECT COUNT(DISTINCT doc_id) FROM revs WHERE current=1 AND deleted=0";
			Cursor cursor = null;
			int result = 0;
			try
			{
				cursor = database.RawQuery(sql, null);
				if (cursor.MoveToNext())
				{
					result = cursor.GetInt(0);
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.Lite.Database.Tag, "Error getting document count", e);
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

		/// <summary>The latest sequence number used.</summary>
		/// <remarks>
		/// The latest sequence number used.  Every new revision is assigned a new sequence number,
		/// so this property increases monotonically as changes are made to the database. It can be
		/// used to check whether the database has changed between two points in time.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual long GetLastSequenceNumber()
		{
			string sql = "SELECT MAX(sequence) FROM revs";
			Cursor cursor = null;
			long result = 0;
			try
			{
				cursor = database.RawQuery(sql, null);
				if (cursor.MoveToNext())
				{
					result = cursor.GetLong(0);
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.Lite.Database.Tag, "Error getting last sequence", e);
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

		/// <summary>Get all the replicators associated with this database.</summary>
		/// <remarks>Get all the replicators associated with this database.</remarks>
		[InterfaceAudience.Public]
		public virtual IList<Replication> GetAllReplications()
		{
			return activeReplicators;
		}

		/// <summary>
		/// Compacts the database file by purging non-current revisions, deleting unused attachment files,
		/// and running a SQLite "VACUUM" command.
		/// </summary>
		/// <remarks>
		/// Compacts the database file by purging non-current revisions, deleting unused attachment files,
		/// and running a SQLite "VACUUM" command.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Status Compact()
		{
			// Can't delete any rows because that would lose revision tree history.
			// But we can remove the JSON of non-current revisions, which is most of the space.
			try
			{
				Log.V(Couchbase.Lite.Database.Tag, "Deleting JSON of old revisions...");
				ContentValues args = new ContentValues();
				args.Put("json", (string)null);
				database.Update("revs", args, "current=0", null);
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.Lite.Database.Tag, "Error compacting", e);
				return new Status(Status.InternalServerError);
			}
			Log.V(Couchbase.Lite.Database.Tag, "Deleting old attachments...");
			Status result = GarbageCollectAttachments();
			Log.V(Couchbase.Lite.Database.Tag, "Vacuuming SQLite sqliteDb...");
			try
			{
				database.ExecSQL("VACUUM");
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.Lite.Database.Tag, "Error vacuuming sqliteDb", e);
				return new Status(Status.InternalServerError);
			}
			return result;
		}

		/// <summary>Deletes the database.</summary>
		/// <remarks>Deletes the database.</remarks>
		[InterfaceAudience.Public]
		public virtual bool Delete()
		{
			if (open)
			{
				if (!Close())
				{
					return false;
				}
			}
			else
			{
				if (!Exists())
				{
					return true;
				}
			}
			FilePath file = new FilePath(path);
			FilePath attachmentsFile = new FilePath(GetAttachmentStorePath());
			bool deleteStatus = file.Delete();
			//recursively delete attachments path
			bool deleteAttachmentStatus = FileDirUtils.DeleteRecursive(attachmentsFile);
			return deleteStatus && deleteAttachmentStatus;
		}

		/// <summary>Instantiates a Document object with the given ID.</summary>
		/// <remarks>
		/// Instantiates a Document object with the given ID.
		/// Doesn't touch the on-disk sqliteDb; a document with that ID doesn't
		/// even need to exist yet. CBLDocuments are cached, so there will
		/// never be more than one instance (in this sqliteDb) at a time with
		/// the same documentID.
		/// NOTE: the caching described above is not implemented yet
		/// </remarks>
		/// <param name="documentId"></param>
		/// <returns></returns>
		[InterfaceAudience.Public]
		public virtual Document GetDocument(string documentId)
		{
			if (documentId == null || documentId.Length == 0)
			{
				return null;
			}
			Document doc = docCache.Get(documentId);
			if (doc == null)
			{
				doc = new Document(this, documentId);
				if (doc == null)
				{
					return null;
				}
				docCache.Put(documentId, doc);
			}
			return doc;
		}

		/// <summary>Gets the Document with the given id, or null if it does not exist.</summary>
		/// <remarks>Gets the Document with the given id, or null if it does not exist.</remarks>
		[InterfaceAudience.Public]
		public virtual Document GetExistingDocument(string documentId)
		{
			if (documentId == null || documentId.Length == 0)
			{
				return null;
			}
			RevisionInternal revisionInternal = GetDocumentWithIDAndRev(documentId, null, EnumSet
				.NoneOf<Database.TDContentOptions>());
			if (revisionInternal == null)
			{
				return null;
			}
			return GetDocument(documentId);
		}

		/// <summary>Creates a new Document object with no properties and a new (random) UUID.
		/// 	</summary>
		/// <remarks>
		/// Creates a new Document object with no properties and a new (random) UUID.
		/// The document will be saved to the database when you call -createRevision: on it.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Document CreateDocument()
		{
			return GetDocument(Misc.TDCreateUUID());
		}

		/// <summary>Returns the contents of the local document with the given ID, or nil if none exists.
		/// 	</summary>
		/// <remarks>Returns the contents of the local document with the given ID, or nil if none exists.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual IDictionary<string, object> GetExistingLocalDocument(string documentId
			)
		{
			RevisionInternal revInt = GetLocalDocument(MakeLocalDocumentId(documentId), null);
			if (revInt == null)
			{
				return null;
			}
			return revInt.GetProperties();
		}

		/// <summary>Sets the contents of the local document with the given ID.</summary>
		/// <remarks>
		/// Sets the contents of the local document with the given ID. Unlike CouchDB, no revision-ID
		/// checking is done; the put always succeeds. If the properties dictionary is nil, the document
		/// will be deleted.
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public virtual bool PutLocalDocument(IDictionary<string, object> properties, string
			 id)
		{
			// TODO: the iOS implementation wraps this in a transaction, this should do the same.
			id = MakeLocalDocumentId(id);
			RevisionInternal prevRev = GetLocalDocument(id, null);
			if (prevRev == null && properties == null)
			{
				return false;
			}
			bool deleted = false;
			if (properties == null)
			{
				deleted = true;
			}
			RevisionInternal rev = new RevisionInternal(id, null, deleted, this);
			if (properties != null)
			{
				rev.SetProperties(properties);
			}
			if (prevRev == null)
			{
				return PutLocalRevision(rev, null) != null;
			}
			else
			{
				return PutLocalRevision(rev, prevRev.GetRevId()) != null;
			}
		}

		/// <summary>Deletes the local document with the given ID.</summary>
		/// <remarks>Deletes the local document with the given ID.</remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Public]
		public virtual bool DeleteLocalDocument(string id)
		{
			id = MakeLocalDocumentId(id);
			RevisionInternal prevRev = GetLocalDocument(id, null);
			if (prevRev == null)
			{
				return false;
			}
			DeleteLocalDocument(id, prevRev.GetRevId());
			return true;
		}

		/// <summary>Returns a query that matches all documents in the database.</summary>
		/// <remarks>
		/// Returns a query that matches all documents in the database.
		/// This is like querying an imaginary view that emits every document's ID as a key.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Query CreateAllDocumentsQuery()
		{
			return new Query(this, (View)null);
		}

		/// <summary>Returns a View object for the view with the given name.</summary>
		/// <remarks>
		/// Returns a View object for the view with the given name.
		/// (This succeeds even if the view doesn't already exist, but the view won't be added to
		/// the database until the View is assigned a map function.)
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual View GetView(string name)
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

		/// <summary>Returns the existing View with the given name, or nil if none.</summary>
		/// <remarks>Returns the existing View with the given name, or nil if none.</remarks>
		[InterfaceAudience.Public]
		public virtual View GetExistingView(string name)
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
			view = new View(this, name);
			if (view.GetViewId() == 0)
			{
				return null;
			}
			return RegisterView(view);
		}

		/// <summary>Returns the existing document validation function (block) registered with the given name.
		/// 	</summary>
		/// <remarks>
		/// Returns the existing document validation function (block) registered with the given name.
		/// Note that validations are not persistent -- you have to re-register them on every launch.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Validator GetValidation(string name)
		{
			Validator result = null;
			if (validations != null)
			{
				result = validations.Get(name);
			}
			return result;
		}

		/// <summary>Defines or clears a named document validation function.</summary>
		/// <remarks>
		/// Defines or clears a named document validation function.
		/// Before any change to the database, all registered validation functions are called and given a
		/// chance to reject it. (This includes incoming changes from a pull replication.)
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual void SetValidation(string name, Validator validator)
		{
			if (validations == null)
			{
				validations = new Dictionary<string, Validator>();
			}
			if (validator != null)
			{
				validations.Put(name, validator);
			}
			else
			{
				Sharpen.Collections.Remove(validations, name);
			}
		}

		/// <summary>Returns the existing filter function (block) registered with the given name.
		/// 	</summary>
		/// <remarks>
		/// Returns the existing filter function (block) registered with the given name.
		/// Note that filters are not persistent -- you have to re-register them on every launch.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual ReplicationFilter GetFilter(string filterName)
		{
			ReplicationFilter result = null;
			if (filters != null)
			{
				result = filters.Get(filterName);
			}
			if (result == null)
			{
				ReplicationFilterCompiler filterCompiler = GetFilterCompiler();
				if (filterCompiler == null)
				{
					return null;
				}
				IList<string> outLanguageList = new AList<string>();
				string sourceCode = GetDesignDocFunction(filterName, "filters", outLanguageList);
				if (sourceCode == null)
				{
					return null;
				}
				string language = outLanguageList[0];
				ReplicationFilter filter = filterCompiler.CompileFilterFunction(sourceCode, language
					);
				if (filter == null)
				{
					Log.W(Couchbase.Lite.Database.Tag, string.Format("Filter %s failed to compile", filterName
						));
					return null;
				}
				SetFilter(filterName, filter);
				return filter;
			}
			return result;
		}

		/// <summary>Define or clear a named filter function.</summary>
		/// <remarks>
		/// Define or clear a named filter function.
		/// Filters are used by push replications to choose which documents to send.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual void SetFilter(string filterName, ReplicationFilter filter)
		{
			if (filters == null)
			{
				filters = new Dictionary<string, ReplicationFilter>();
			}
			if (filter != null)
			{
				filters.Put(filterName, filter);
			}
			else
			{
				Sharpen.Collections.Remove(filters, filterName);
			}
		}

		/// <summary>Runs the block within a transaction.</summary>
		/// <remarks>
		/// Runs the block within a transaction. If the block returns NO, the transaction is rolled back.
		/// Use this when performing bulk write operations like multiple inserts/updates;
		/// it saves the overhead of multiple SQLite commits, greatly improving performance.
		/// Does not commit the transaction if the code throws an Exception.
		/// TODO: the iOS version has a retry loop, so there should be one here too
		/// </remarks>
		/// <param name="databaseFunction"></param>
		[InterfaceAudience.Public]
		public virtual bool RunInTransaction(TransactionTask databaseFunction)
		{
			bool shouldCommit = true;
			BeginTransaction();
			try
			{
				shouldCommit = databaseFunction.Run();
			}
			catch (Exception e)
			{
				shouldCommit = false;
				Log.E(Couchbase.Lite.Database.Tag, e.ToString(), e);
				throw new RuntimeException(e);
			}
			finally
			{
				EndTransaction(shouldCommit);
			}
			return shouldCommit;
		}

		/// <summary>Runs the delegate asynchronously.</summary>
		/// <remarks>Runs the delegate asynchronously.</remarks>
		[InterfaceAudience.Public]
		public virtual Future RunAsync(AsyncTask function)
		{
			return GetManager().RunAsync(new _Runnable_588(this, function));
		}

		private sealed class _Runnable_588 : Runnable
		{
			public _Runnable_588(Database _enclosing, AsyncTask function)
			{
				this._enclosing = _enclosing;
				this.function = function;
			}

			public void Run()
			{
				function.Run(this._enclosing);
			}

			private readonly Database _enclosing;

			private readonly AsyncTask function;
		}

		/// <summary>
		/// Creates a replication that will 'push' to a database at the given URL, or returns an existing
		/// such replication if there already is one.
		/// </summary>
		/// <remarks>
		/// Creates a replication that will 'push' to a database at the given URL, or returns an existing
		/// such replication if there already is one.
		/// </remarks>
		/// <param name="remote"></param>
		/// <returns></returns>
		[InterfaceAudience.Public]
		public virtual Replication CreatePushReplication(Uri remote)
		{
			bool continuous = false;
			return new Pusher(this, remote, continuous, manager.GetWorkExecutor());
		}

		/// <summary>
		/// Creates a replication that will 'pull' from a database at the given URL, or returns an existing
		/// such replication if there already is one.
		/// </summary>
		/// <remarks>
		/// Creates a replication that will 'pull' from a database at the given URL, or returns an existing
		/// such replication if there already is one.
		/// </remarks>
		/// <param name="remote"></param>
		/// <returns></returns>
		[InterfaceAudience.Public]
		public virtual Replication CreatePullReplication(Uri remote)
		{
			bool continuous = false;
			return new Puller(this, remote, continuous, manager.GetWorkExecutor());
		}

		/// <summary>Adds a Database change delegate that will be called whenever a Document within the Database changes.
		/// 	</summary>
		/// <remarks>Adds a Database change delegate that will be called whenever a Document within the Database changes.
		/// 	</remarks>
		/// <param name="listener"></param>
		[InterfaceAudience.Public]
		public virtual void AddChangeListener(Database.ChangeListener listener)
		{
			changeListeners.AddItem(listener);
		}

		/// <summary>Removes the specified delegate as a listener for the Database change event.
		/// 	</summary>
		/// <remarks>Removes the specified delegate as a listener for the Database change event.
		/// 	</remarks>
		/// <param name="listener"></param>
		[InterfaceAudience.Public]
		public virtual void RemoveChangeListener(Database.ChangeListener listener)
		{
			changeListeners.Remove(listener);
		}

		[InterfaceAudience.Public]
		public override string ToString()
		{
			return this.GetType().FullName + "[" + path + "]";
		}

		public class ChangeEvent
		{
			private Database source;

			private bool isExternal;

			private IList<DocumentChange> changes;

			public ChangeEvent(Database source, bool isExternal, IList<DocumentChange> changes
				)
			{
				this.source = source;
				this.isExternal = isExternal;
				this.changes = changes;
			}

			public virtual Database GetSource()
			{
				return source;
			}

			public virtual bool IsExternal()
			{
				return isExternal;
			}

			public virtual IList<DocumentChange> GetChanges()
			{
				return changes;
			}
		}

		public interface ChangeListener
		{
			void Changed(Database.ChangeEvent @event);
		}

		/// <summary>Returns the already-instantiated cached Document with the given ID, or nil if none is yet cached.
		/// 	</summary>
		/// <remarks>Returns the already-instantiated cached Document with the given ID, or nil if none is yet cached.
		/// 	</remarks>
		[InterfaceAudience.Private]
		protected internal virtual Document GetCachedDocument(string documentID)
		{
			return docCache.Get(documentID);
		}

		/// <summary>Empties the cache of recently used Document objects.</summary>
		/// <remarks>
		/// Empties the cache of recently used Document objects.
		/// API calls will now instantiate and return new instances.
		/// </remarks>
		[InterfaceAudience.Private]
		protected internal virtual void ClearDocumentCache()
		{
			docCache.EvictAll();
		}

		[InterfaceAudience.Private]
		protected internal virtual void RemoveDocumentFromCache(Document document)
		{
			docCache.Remove(document.GetId());
		}

		[InterfaceAudience.Private]
		public virtual bool Exists()
		{
			return new FilePath(path).Exists();
		}

		[InterfaceAudience.Private]
		public virtual string GetAttachmentStorePath()
		{
			string attachmentStorePath = path;
			int lastDotPosition = attachmentStorePath.LastIndexOf('.');
			if (lastDotPosition > 0)
			{
				attachmentStorePath = Sharpen.Runtime.Substring(attachmentStorePath, 0, lastDotPosition
					);
			}
			attachmentStorePath = attachmentStorePath + FilePath.separator + "attachments";
			return attachmentStorePath;
		}

		[InterfaceAudience.Private]
		public static Database CreateEmptyDBAtPath(string path, Manager manager)
		{
			if (!FileDirUtils.RemoveItemIfExists(path))
			{
				return null;
			}
			Database result = new Database(path, manager);
			FilePath af = new FilePath(result.GetAttachmentStorePath());
			//recursively delete attachments path
			if (!FileDirUtils.DeleteRecursive(af))
			{
				return null;
			}
			if (!result.Open())
			{
				return null;
			}
			return result;
		}

		[InterfaceAudience.Private]
		public virtual bool Initialize(string statements)
		{
			try
			{
				foreach (string statement in statements.Split(";"))
				{
					database.ExecSQL(statement);
				}
			}
			catch (SQLException)
			{
				Close();
				return false;
			}
			return true;
		}

		[InterfaceAudience.Private]
		public virtual bool Open()
		{
			if (open)
			{
				return true;
			}
			// Create the storage engine.
			database = SQLiteStorageEngineFactory.CreateStorageEngine();
			// Try to open the storage engine and stop if we fail.
			if (database == null || !database.Open(path))
			{
				return false;
			}
			// Stuff we need to initialize every time the sqliteDb opens:
			if (!Initialize("PRAGMA foreign_keys = ON;"))
			{
				Log.E(Database.Tag, "Error turning on foreign keys");
				return false;
			}
			// Check the user_version number we last stored in the sqliteDb:
			int dbVersion = database.GetVersion();
			// Incompatible version changes increment the hundreds' place:
			if (dbVersion >= 100)
			{
				Log.W(Database.Tag, "Database: Database version (" + dbVersion + ") is newer than I know how to work with"
					);
				database.Close();
				return false;
			}
			if (dbVersion < 1)
			{
				// First-time initialization:
				// (Note: Declaring revs.sequence as AUTOINCREMENT means the values will always be
				// monotonically increasing, never reused. See <http://www.sqlite.org/autoinc.html>)
				if (!Initialize(Schema))
				{
					database.Close();
					return false;
				}
				dbVersion = 3;
			}
			if (dbVersion < 2)
			{
				// Version 2: added attachments.revpos
				string upgradeSql = "ALTER TABLE attachments ADD COLUMN revpos INTEGER DEFAULT 0; "
					 + "PRAGMA user_version = 2";
				if (!Initialize(upgradeSql))
				{
					database.Close();
					return false;
				}
				dbVersion = 2;
			}
			if (dbVersion < 3)
			{
				string upgradeSql = "CREATE TABLE localdocs ( " + "docid TEXT UNIQUE NOT NULL, " 
					+ "revid TEXT NOT NULL, " + "json BLOB); " + "CREATE INDEX localdocs_by_docid ON localdocs(docid); "
					 + "PRAGMA user_version = 3";
				if (!Initialize(upgradeSql))
				{
					database.Close();
					return false;
				}
				dbVersion = 3;
			}
			if (dbVersion < 4)
			{
				string upgradeSql = "CREATE TABLE info ( " + "key TEXT PRIMARY KEY, " + "value TEXT); "
					 + "INSERT INTO INFO (key, value) VALUES ('privateUUID', '" + Misc.TDCreateUUID(
					) + "'); " + "INSERT INTO INFO (key, value) VALUES ('publicUUID',  '" + Misc.TDCreateUUID
					() + "'); " + "PRAGMA user_version = 4";
				if (!Initialize(upgradeSql))
				{
					database.Close();
					return false;
				}
			}
			try
			{
				attachments = new BlobStore(GetAttachmentStorePath());
			}
			catch (ArgumentException e)
			{
				Log.E(Database.Tag, "Could not initialize attachment store", e);
				database.Close();
				return false;
			}
			open = true;
			return true;
		}

		[InterfaceAudience.Private]
		public virtual bool Close()
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
			if (activeReplicators != null)
			{
				foreach (Replication replicator in activeReplicators)
				{
					replicator.DatabaseClosing();
				}
				activeReplicators = null;
			}
			if (database != null && database.IsOpen())
			{
				database.Close();
			}
			open = false;
			transactionLevel = 0;
			return true;
		}

		[InterfaceAudience.Private]
		public virtual string GetPath()
		{
			return path;
		}

		// Leave this package protected, so it can only be used
		// View uses this accessor
		[InterfaceAudience.Private]
		internal virtual SQLiteStorageEngine GetDatabase()
		{
			return database;
		}

		[InterfaceAudience.Private]
		public virtual BlobStore GetAttachments()
		{
			return attachments;
		}

		[InterfaceAudience.Private]
		public virtual BlobStoreWriter GetAttachmentWriter()
		{
			return new BlobStoreWriter(GetAttachments());
		}

		[InterfaceAudience.Private]
		public virtual long TotalDataSize()
		{
			FilePath f = new FilePath(path);
			long size = f.Length() + attachments.TotalDataSize();
			return size;
		}

		/// <summary>Begins a database transaction.</summary>
		/// <remarks>
		/// Begins a database transaction. Transactions can nest.
		/// Every beginTransaction() must be balanced by a later endTransaction()
		/// </remarks>
		[InterfaceAudience.Private]
		public virtual bool BeginTransaction()
		{
			try
			{
				database.BeginTransaction();
				++transactionLevel;
				Log.I(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " Begin transaction (level "
					 + Sharpen.Extensions.ToString(transactionLevel) + ")");
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, Sharpen.Thread.CurrentThread().GetName() + " Error calling beginTransaction()"
					, e);
				return false;
			}
			return true;
		}

		/// <summary>Commits or aborts (rolls back) a transaction.</summary>
		/// <remarks>Commits or aborts (rolls back) a transaction.</remarks>
		/// <param name="commit">If true, commits; if false, aborts and rolls back, undoing all changes made since the matching -beginTransaction call, *including* any committed nested transactions.
		/// 	</param>
		[InterfaceAudience.Private]
		public virtual bool EndTransaction(bool commit)
		{
			System.Diagnostics.Debug.Assert((transactionLevel > 0));
			if (commit)
			{
				Log.I(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " Committing transaction (level "
					 + Sharpen.Extensions.ToString(transactionLevel) + ")");
				database.SetTransactionSuccessful();
				database.EndTransaction();
			}
			else
			{
				Log.I(TagSql, Sharpen.Thread.CurrentThread().GetName() + " CANCEL transaction (level "
					 + Sharpen.Extensions.ToString(transactionLevel) + ")");
				try
				{
					database.EndTransaction();
				}
				catch (SQLException e)
				{
					Log.E(Database.Tag, Sharpen.Thread.CurrentThread().GetName() + " Error calling endTransaction()"
						, e);
					return false;
				}
			}
			--transactionLevel;
			return true;
		}

		[InterfaceAudience.Private]
		public virtual string PrivateUUID()
		{
			string result = null;
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery("SELECT value FROM info WHERE key='privateUUID'", null
					);
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

		[InterfaceAudience.Private]
		public virtual string PublicUUID()
		{
			string result = null;
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery("SELECT value FROM info WHERE key='publicUUID'", null);
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

		/// <summary>Splices the contents of an NSDictionary into JSON data (that already represents a dict), without parsing the JSON.
		/// 	</summary>
		/// <remarks>Splices the contents of an NSDictionary into JSON data (that already represents a dict), without parsing the JSON.
		/// 	</remarks>
		[InterfaceAudience.Private]
		public virtual byte[] AppendDictToJSON(byte[] json, IDictionary<string, object> dict
			)
		{
			if (dict.Count == 0)
			{
				return json;
			}
			byte[] extraJSON = null;
			try
			{
				extraJSON = Manager.GetObjectMapper().WriteValueAsBytes(dict);
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error convert extra JSON to bytes", e);
				return null;
			}
			int jsonLength = json.Length;
			int extraLength = extraJSON.Length;
			if (jsonLength == 2)
			{
				// Original JSON was empty
				return extraJSON;
			}
			byte[] newJson = new byte[jsonLength + extraLength - 1];
			System.Array.Copy(json, 0, newJson, 0, jsonLength - 1);
			// Copy json w/o trailing '}'
			newJson[jsonLength - 1] = (byte)(',');
			// Add a ','
			System.Array.Copy(extraJSON, 1, newJson, jsonLength, extraLength - 1);
			return newJson;
		}

		/// <summary>Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
		/// 	</summary>
		/// <remarks>
		/// Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
		/// Rev must already have its revID and sequence properties set.
		/// </remarks>
		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> ExtraPropertiesForRevision(RevisionInternal
			 rev, EnumSet<Database.TDContentOptions> contentOptions)
		{
			string docId = rev.GetDocId();
			string revId = rev.GetRevId();
			long sequenceNumber = rev.GetSequence();
			System.Diagnostics.Debug.Assert((revId != null));
			System.Diagnostics.Debug.Assert((sequenceNumber > 0));
			// Get attachment metadata, and optionally the contents:
			IDictionary<string, object> attachmentsDict = GetAttachmentsDictForSequenceWithContent
				(sequenceNumber, contentOptions);
			// Get more optional stuff to put in the properties:
			//OPT: This probably ends up making redundant SQL queries if multiple options are enabled.
			long localSeq = null;
			if (contentOptions.Contains(Database.TDContentOptions.TDIncludeLocalSeq))
			{
				localSeq = sequenceNumber;
			}
			IDictionary<string, object> revHistory = null;
			if (contentOptions.Contains(Database.TDContentOptions.TDIncludeRevs))
			{
				revHistory = GetRevisionHistoryDict(rev);
			}
			IList<object> revsInfo = null;
			if (contentOptions.Contains(Database.TDContentOptions.TDIncludeRevsInfo))
			{
				revsInfo = new AList<object>();
				IList<RevisionInternal> revHistoryFull = GetRevisionHistory(rev);
				foreach (RevisionInternal historicalRev in revHistoryFull)
				{
					IDictionary<string, object> revHistoryItem = new Dictionary<string, object>();
					string status = "available";
					if (historicalRev.IsDeleted())
					{
						status = "deleted";
					}
					if (historicalRev.IsMissing())
					{
						status = "missing";
					}
					revHistoryItem.Put("rev", historicalRev.GetRevId());
					revHistoryItem.Put("status", status);
					revsInfo.AddItem(revHistoryItem);
				}
			}
			IList<string> conflicts = null;
			if (contentOptions.Contains(Database.TDContentOptions.TDIncludeConflicts))
			{
				RevisionList revs = GetAllRevisionsOfDocumentID(docId, true);
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
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("_id", docId);
			result.Put("_rev", revId);
			if (rev.IsDeleted())
			{
				result.Put("_deleted", true);
			}
			if (attachmentsDict != null)
			{
				result.Put("_attachments", attachmentsDict);
			}
			if (localSeq != null)
			{
				result.Put("_local_seq", localSeq);
			}
			if (revHistory != null)
			{
				result.Put("_revisions", revHistory);
			}
			if (revsInfo != null)
			{
				result.Put("_revs_info", revsInfo);
			}
			if (conflicts != null)
			{
				result.Put("_conflicts", conflicts);
			}
			return result;
		}

		/// <summary>Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
		/// 	</summary>
		/// <remarks>
		/// Inserts the _id, _rev and _attachments properties into the JSON data and stores it in rev.
		/// Rev must already have its revID and sequence properties set.
		/// </remarks>
		[InterfaceAudience.Private]
		public virtual void ExpandStoredJSONIntoRevisionWithAttachments(byte[] json, RevisionInternal
			 rev, EnumSet<Database.TDContentOptions> contentOptions)
		{
			IDictionary<string, object> extra = ExtraPropertiesForRevision(rev, contentOptions
				);
			if (json != null)
			{
				rev.SetJson(AppendDictToJSON(json, extra));
			}
			else
			{
				rev.SetProperties(extra);
			}
		}

		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> DocumentPropertiesFromJSON(byte[] json
			, string docId, string revId, bool deleted, long sequence, EnumSet<Database.TDContentOptions
			> contentOptions)
		{
			RevisionInternal rev = new RevisionInternal(docId, revId, deleted, this);
			rev.SetSequence(sequence);
			IDictionary<string, object> extra = ExtraPropertiesForRevision(rev, contentOptions
				);
			if (json == null)
			{
				return extra;
			}
			IDictionary<string, object> docProperties = null;
			try
			{
				docProperties = Manager.GetObjectMapper().ReadValue<IDictionary>(json);
				docProperties.PutAll(extra);
				return docProperties;
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error serializing properties to JSON", e);
			}
			return docProperties;
		}

		[InterfaceAudience.Private]
		public virtual RevisionInternal GetDocumentWithIDAndRev(string id, string rev, EnumSet
			<Database.TDContentOptions> contentOptions)
		{
			RevisionInternal result = null;
			string sql;
			Cursor cursor = null;
			try
			{
				cursor = null;
				string cols = "revid, deleted, sequence";
				if (!contentOptions.Contains(Database.TDContentOptions.TDNoBody))
				{
					cols += ", json";
				}
				if (rev != null)
				{
					sql = "SELECT " + cols + " FROM revs, docs WHERE docs.docid=? AND revs.doc_id=docs.doc_id AND revid=? LIMIT 1";
					string[] args = new string[] { id, rev };
					cursor = database.RawQuery(sql, args);
				}
				else
				{
					sql = "SELECT " + cols + " FROM revs, docs WHERE docs.docid=? AND revs.doc_id=docs.doc_id and current=1 and deleted=0 ORDER BY revid DESC LIMIT 1";
					string[] args = new string[] { id };
					cursor = database.RawQuery(sql, args);
				}
				if (cursor.MoveToNext())
				{
					if (rev == null)
					{
						rev = cursor.GetString(0);
					}
					bool deleted = (cursor.GetInt(1) > 0);
					result = new RevisionInternal(id, rev, deleted, this);
					result.SetSequence(cursor.GetLong(2));
					if (!contentOptions.Equals(EnumSet.Of(Database.TDContentOptions.TDNoBody)))
					{
						byte[] json = null;
						if (!contentOptions.Contains(Database.TDContentOptions.TDNoBody))
						{
							json = cursor.GetBlob(3);
						}
						ExpandStoredJSONIntoRevisionWithAttachments(json, result, contentOptions);
					}
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting document with id and rev", e);
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

		[InterfaceAudience.Private]
		public virtual bool ExistsDocumentWithIDAndRev(string docId, string revId)
		{
			return GetDocumentWithIDAndRev(docId, revId, EnumSet.Of(Database.TDContentOptions
				.TDNoBody)) != null;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual RevisionInternal LoadRevisionBody(RevisionInternal rev, EnumSet<Database.TDContentOptions
			> contentOptions)
		{
			if (rev.GetBody() != null && contentOptions == EnumSet.NoneOf<Database.TDContentOptions
				>() && rev.GetSequence() != 0)
			{
				return rev;
			}
			System.Diagnostics.Debug.Assert(((rev.GetDocId() != null) && (rev.GetRevId() != null
				)));
			Cursor cursor = null;
			Status result = new Status(Status.NotFound);
			try
			{
				// TODO: on ios this query is:
				// TODO: "SELECT sequence, json FROM revs WHERE doc_id=? AND revid=? LIMIT 1"
				string sql = "SELECT sequence, json FROM revs, docs WHERE revid=? AND docs.docid=? AND revs.doc_id=docs.doc_id LIMIT 1";
				string[] args = new string[] { rev.GetRevId(), rev.GetDocId() };
				cursor = database.RawQuery(sql, args);
				if (cursor.MoveToNext())
				{
					result.SetCode(Status.Ok);
					rev.SetSequence(cursor.GetLong(0));
					ExpandStoredJSONIntoRevisionWithAttachments(cursor.GetBlob(1), rev, contentOptions
						);
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error loading revision body", e);
				throw new CouchbaseLiteException(Status.InternalServerError);
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
			return rev;
		}

		[InterfaceAudience.Private]
		public virtual long GetDocNumericID(string docId)
		{
			Cursor cursor = null;
			string[] args = new string[] { docId };
			long result = -1;
			try
			{
				cursor = database.RawQuery("SELECT doc_id FROM docs WHERE docid=?", args);
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
				Log.E(Database.Tag, "Error getting doc numeric id", e);
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

		/// <summary>Returns all the known revisions (or all current/conflicting revisions) of a document.
		/// 	</summary>
		/// <remarks>Returns all the known revisions (or all current/conflicting revisions) of a document.
		/// 	</remarks>
		[InterfaceAudience.Private]
		public virtual RevisionList GetAllRevisionsOfDocumentID(string docId, long docNumericID
			, bool onlyCurrent)
		{
			string sql = null;
			if (onlyCurrent)
			{
				sql = "SELECT sequence, revid, deleted FROM revs " + "WHERE doc_id=? AND current ORDER BY sequence DESC";
			}
			else
			{
				sql = "SELECT sequence, revid, deleted FROM revs " + "WHERE doc_id=? ORDER BY sequence DESC";
			}
			string[] args = new string[] { System.Convert.ToString(docNumericID) };
			Cursor cursor = null;
			cursor = database.RawQuery(sql, args);
			RevisionList result;
			try
			{
				cursor.MoveToNext();
				result = new RevisionList();
				while (!cursor.IsAfterLast())
				{
					RevisionInternal rev = new RevisionInternal(docId, cursor.GetString(1), (cursor.GetInt
						(2) > 0), this);
					rev.SetSequence(cursor.GetLong(0));
					result.AddItem(rev);
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting all revisions of document", e);
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

		[InterfaceAudience.Private]
		public virtual RevisionList GetAllRevisionsOfDocumentID(string docId, bool onlyCurrent
			)
		{
			long docNumericId = GetDocNumericID(docId);
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
					return GetAllRevisionsOfDocumentID(docId, docNumericId, onlyCurrent);
				}
			}
		}

		[InterfaceAudience.Private]
		public virtual IList<string> GetConflictingRevisionIDsOfDocID(string docID)
		{
			long docIdNumeric = GetDocNumericID(docID);
			if (docIdNumeric < 0)
			{
				return null;
			}
			IList<string> result = new AList<string>();
			Cursor cursor = null;
			try
			{
				string[] args = new string[] { System.Convert.ToString(docIdNumeric) };
				cursor = database.RawQuery("SELECT revid FROM revs WHERE doc_id=? AND current " +
					 "ORDER BY revid DESC OFFSET 1", args);
				cursor.MoveToNext();
				while (!cursor.IsAfterLast())
				{
					result.AddItem(cursor.GetString(0));
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting all revisions of document", e);
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

		[InterfaceAudience.Private]
		public virtual string FindCommonAncestorOf(RevisionInternal rev, IList<string> revIDs
			)
		{
			string result = null;
			if (revIDs.Count == 0)
			{
				return null;
			}
			string docId = rev.GetDocId();
			long docNumericID = GetDocNumericID(docId);
			if (docNumericID <= 0)
			{
				return null;
			}
			string quotedRevIds = JoinQuoted(revIDs);
			string sql = "SELECT revid FROM revs " + "WHERE doc_id=? and revid in (" + quotedRevIds
				 + ") and revid <= ? " + "ORDER BY revid DESC LIMIT 1";
			string[] args = new string[] { System.Convert.ToString(docNumericID) };
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery(sql, args);
				cursor.MoveToNext();
				if (!cursor.IsAfterLast())
				{
					result = cursor.GetString(0);
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting all revisions of document", e);
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

		/// <summary>Returns an array of TDRevs in reverse chronological order, starting with the given revision.
		/// 	</summary>
		/// <remarks>Returns an array of TDRevs in reverse chronological order, starting with the given revision.
		/// 	</remarks>
		[InterfaceAudience.Private]
		public virtual IList<RevisionInternal> GetRevisionHistory(RevisionInternal rev)
		{
			string docId = rev.GetDocId();
			string revId = rev.GetRevId();
			System.Diagnostics.Debug.Assert(((docId != null) && (revId != null)));
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
			string sql = "SELECT sequence, parent, revid, deleted, json isnull FROM revs " + 
				"WHERE doc_id=? ORDER BY sequence DESC";
			string[] args = new string[] { System.Convert.ToString(docNumericId) };
			Cursor cursor = null;
			IList<RevisionInternal> result;
			try
			{
				cursor = database.RawQuery(sql, args);
				cursor.MoveToNext();
				long lastSequence = 0;
				result = new AList<RevisionInternal>();
				while (!cursor.IsAfterLast())
				{
					long sequence = cursor.GetLong(0);
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
						bool deleted = (cursor.GetInt(3) > 0);
						bool missing = (cursor.GetInt(4) > 0);
						RevisionInternal aRev = new RevisionInternal(docId, revId, deleted, this);
						aRev.SetMissing(missing);
						aRev.SetSequence(cursor.GetLong(0));
						result.AddItem(aRev);
						lastSequence = cursor.GetLong(1);
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
				Log.E(Database.Tag, "Error getting revision history", e);
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

		// Splits a revision ID into its generation number and opaque suffix string
		[InterfaceAudience.Private]
		public static int ParseRevIDNumber(string rev)
		{
			int result = -1;
			int dashPos = rev.IndexOf("-");
			if (dashPos >= 0)
			{
				try
				{
					result = System.Convert.ToInt32(Sharpen.Runtime.Substring(rev, 0, dashPos));
				}
				catch (FormatException)
				{
				}
			}
			// ignore, let it return -1
			return result;
		}

		// Splits a revision ID into its generation number and opaque suffix string
		[InterfaceAudience.Private]
		public static string ParseRevIDSuffix(string rev)
		{
			string result = null;
			int dashPos = rev.IndexOf("-");
			if (dashPos >= 0)
			{
				result = Sharpen.Runtime.Substring(rev, dashPos + 1);
			}
			return result;
		}

		[InterfaceAudience.Private]
		public static IDictionary<string, object> MakeRevisionHistoryDict(IList<RevisionInternal
			> history)
		{
			if (history == null)
			{
				return null;
			}
			// Try to extract descending numeric prefixes:
			IList<string> suffixes = new AList<string>();
			int start = -1;
			int lastRevNo = -1;
			foreach (RevisionInternal rev in history)
			{
				int revNo = ParseRevIDNumber(rev.GetRevId());
				string suffix = ParseRevIDSuffix(rev.GetRevId());
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
			IDictionary<string, object> result = new Dictionary<string, object>();
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
				result.Put("start", start);
			}
			result.Put("ids", suffixes);
			return result;
		}

		/// <summary>Returns the revision history as a _revisions dictionary, as returned by the REST API's ?revs=true option.
		/// 	</summary>
		/// <remarks>Returns the revision history as a _revisions dictionary, as returned by the REST API's ?revs=true option.
		/// 	</remarks>
		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> GetRevisionHistoryDict(RevisionInternal
			 rev)
		{
			return MakeRevisionHistoryDict(GetRevisionHistory(rev));
		}

		[InterfaceAudience.Private]
		public virtual RevisionList ChangesSince(long lastSeq, ChangesOptions options, ReplicationFilter
			 filter)
		{
			// http://wiki.apache.org/couchdb/HTTP_database_API#Changes
			if (options == null)
			{
				options = new ChangesOptions();
			}
			bool includeDocs = options.IsIncludeDocs() || (filter != null);
			string additionalSelectColumns = string.Empty;
			if (includeDocs)
			{
				additionalSelectColumns = ", json";
			}
			string sql = "SELECT sequence, revs.doc_id, docid, revid, deleted" + additionalSelectColumns
				 + " FROM revs, docs " + "WHERE sequence > ? AND current=1 " + "AND revs.doc_id = docs.doc_id "
				 + "ORDER BY revs.doc_id, revid DESC";
			string[] args = new string[] { System.Convert.ToString(lastSeq) };
			Cursor cursor = null;
			RevisionList changes = null;
			try
			{
				cursor = database.RawQuery(sql, args);
				cursor.MoveToNext();
				changes = new RevisionList();
				long lastDocId = 0;
				while (!cursor.IsAfterLast())
				{
					if (!options.IsIncludeConflicts())
					{
						// Only count the first rev for a given doc (the rest will be losing conflicts):
						long docNumericId = cursor.GetLong(1);
						if (docNumericId == lastDocId)
						{
							cursor.MoveToNext();
							continue;
						}
						lastDocId = docNumericId;
					}
					RevisionInternal rev = new RevisionInternal(cursor.GetString(2), cursor.GetString
						(3), (cursor.GetInt(4) > 0), this);
					rev.SetSequence(cursor.GetLong(0));
					if (includeDocs)
					{
						ExpandStoredJSONIntoRevisionWithAttachments(cursor.GetBlob(5), rev, options.GetContentOptions
							());
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
				Log.E(Database.Tag, "Error looking for changes", e);
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

		[InterfaceAudience.Private]
		public virtual bool RunFilter(ReplicationFilter filter, IDictionary<string, object
			> paramsIgnored, RevisionInternal rev)
		{
			if (filter == null)
			{
				return true;
			}
			SavedRevision publicRev = new SavedRevision(this, rev);
			return filter.Filter(publicRev, null);
		}

		[InterfaceAudience.Private]
		public virtual string GetDesignDocFunction(string fnName, string key, IList<string
			> outLanguageList)
		{
			string[] path = fnName.Split("/");
			if (path.Length != 2)
			{
				return null;
			}
			string docId = string.Format("_design/%s", path[0]);
			RevisionInternal rev = GetDocumentWithIDAndRev(docId, null, EnumSet.NoneOf<Database.TDContentOptions
				>());
			if (rev == null)
			{
				return null;
			}
			string outLanguage = (string)rev.GetPropertyForKey("language");
			if (outLanguage != null)
			{
				outLanguageList.AddItem(outLanguage);
			}
			else
			{
				outLanguageList.AddItem("javascript");
			}
			IDictionary<string, object> container = (IDictionary<string, object>)rev.GetPropertyForKey
				(key);
			return (string)container.Get(path[1]);
		}

		/// <summary>VIEWS:</summary>
		[InterfaceAudience.Private]
		public virtual View RegisterView(View view)
		{
			if (view == null)
			{
				return null;
			}
			if (views == null)
			{
				views = new Dictionary<string, View>();
			}
			views.Put(view.GetName(), view);
			return view;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual IList<QueryRow> QueryViewNamed(string viewName, QueryOptions options
			, IList<long> outLastSequence)
		{
			long before = Runtime.CurrentTimeMillis();
			long lastSequence = 0;
			IList<QueryRow> rows = null;
			if (viewName != null && viewName.Length > 0)
			{
				View view = GetView(viewName);
				if (view == null)
				{
					throw new CouchbaseLiteException(new Status(Status.NotFound));
				}
				lastSequence = view.GetLastSequenceIndexed();
				if (options.GetStale() == Query.IndexUpdateMode.Before || lastSequence <= 0)
				{
					view.UpdateIndex();
					lastSequence = view.GetLastSequenceIndexed();
				}
				else
				{
					if (options.GetStale() == Query.IndexUpdateMode.After && lastSequence < GetLastSequenceNumber
						())
					{
						new Sharpen.Thread(new _Runnable_1639(view)).Start();
					}
				}
				rows = view.QueryWithOptions(options);
			}
			else
			{
				// nil view means query _all_docs
				// note: this is a little kludgy, but we have to pull out the "rows" field from the
				// result dictionary because that's what we want.  should be refactored, but
				// it's a little tricky, so postponing.
				IDictionary<string, object> allDocsResult = GetAllDocs(options);
				rows = (IList<QueryRow>)allDocsResult.Get("rows");
				lastSequence = GetLastSequenceNumber();
			}
			outLastSequence.AddItem(lastSequence);
			long delta = Runtime.CurrentTimeMillis() - before;
			Log.D(Database.Tag, string.Format("Query view %s completed in %d milliseconds", viewName
				, delta));
			return rows;
		}

		private sealed class _Runnable_1639 : Runnable
		{
			public _Runnable_1639(View view)
			{
				this.view = view;
			}

			public void Run()
			{
				try
				{
					view.UpdateIndex();
				}
				catch (CouchbaseLiteException e)
				{
					Log.E(Database.Tag, "Error updating view index on background thread", e);
				}
			}

			private readonly View view;
		}

		[InterfaceAudience.Private]
		internal virtual View MakeAnonymousView()
		{
			for (int i = 0; true; ++i)
			{
				string name = string.Format("anon%d", i);
				View existing = GetExistingView(name);
				if (existing == null)
				{
					// this name has not been used yet, so let's use it
					return GetView(name);
				}
			}
		}

		[InterfaceAudience.Private]
		public virtual IList<View> GetAllViews()
		{
			Cursor cursor = null;
			IList<View> result = null;
			try
			{
				cursor = database.RawQuery("SELECT name FROM views", null);
				cursor.MoveToNext();
				result = new AList<View>();
				while (!cursor.IsAfterLast())
				{
					result.AddItem(GetView(cursor.GetString(0)));
					cursor.MoveToNext();
				}
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error getting all views", e);
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

		[InterfaceAudience.Private]
		public virtual Status DeleteViewNamed(string name)
		{
			Status result = new Status(Status.InternalServerError);
			try
			{
				string[] whereArgs = new string[] { name };
				int rowsAffected = database.Delete("views", "name=?", whereArgs);
				if (rowsAffected > 0)
				{
					result.SetCode(Status.Ok);
				}
				else
				{
					result.SetCode(Status.NotFound);
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error deleting view", e);
			}
			return result;
		}

		/// <summary>Hack because cursor interface does not support cursor.getColumnIndex("deleted") yet.
		/// 	</summary>
		/// <remarks>Hack because cursor interface does not support cursor.getColumnIndex("deleted") yet.
		/// 	</remarks>
		[InterfaceAudience.Private]
		public virtual int GetDeletedColumnIndex(QueryOptions options)
		{
			if (options.IsIncludeDocs())
			{
				return 5;
			}
			else
			{
				return 4;
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> GetAllDocs(QueryOptions options)
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			IList<QueryRow> rows = new AList<QueryRow>();
			if (options == null)
			{
				options = new QueryOptions();
			}
			bool includeDeletedDocs = (options.GetAllDocsMode() == Query.AllDocsMode.IncludeDeleted
				);
			long updateSeq = 0;
			if (options.IsUpdateSeq())
			{
				updateSeq = GetLastSequenceNumber();
			}
			// TODO: needs to be atomic with the following SELECT
			StringBuilder sql = new StringBuilder("SELECT revs.doc_id, docid, revid, sequence"
				);
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
				if (options.GetKeys().Count == 0)
				{
					return result;
				}
				string commaSeperatedIds = JoinQuotedObjects(options.GetKeys());
				sql.Append(string.Format(" revs.doc_id IN (SELECT doc_id FROM docs WHERE docid IN (%s)) AND"
					, commaSeperatedIds));
			}
			sql.Append(" docs.doc_id = revs.doc_id AND current=1");
			if (!includeDeletedDocs)
			{
				sql.Append(" AND deleted=0");
			}
			IList<string> args = new AList<string>();
			object minKey = options.GetStartKey();
			object maxKey = options.GetEndKey();
			bool inclusiveMin = true;
			bool inclusiveMax = options.IsInclusiveEnd();
			if (options.IsDescending())
			{
				minKey = maxKey;
				maxKey = options.GetStartKey();
				inclusiveMin = inclusiveMax;
				inclusiveMax = true;
			}
			if (minKey != null)
			{
				System.Diagnostics.Debug.Assert((minKey is string));
				sql.Append((inclusiveMin ? " AND docid >= ?" : " AND docid > ?"));
				args.AddItem((string)minKey);
			}
			if (maxKey != null)
			{
				System.Diagnostics.Debug.Assert((maxKey is string));
				sql.Append((inclusiveMax ? " AND docid <= ?" : " AND docid < ?"));
				args.AddItem((string)maxKey);
			}
			sql.Append(string.Format(" ORDER BY docid %s, %s revid DESC LIMIT ? OFFSET ?", (options
				.IsDescending() ? "DESC" : "ASC"), (includeDeletedDocs ? "deleted ASC," : string.Empty
				)));
			args.AddItem(Sharpen.Extensions.ToString(options.GetLimit()));
			args.AddItem(Sharpen.Extensions.ToString(options.GetSkip()));
			Cursor cursor = null;
			IDictionary<string, QueryRow> docs = new Dictionary<string, QueryRow>();
			try
			{
				cursor = database.RawQuery(sql.ToString(), Sharpen.Collections.ToArray(args, new 
					string[args.Count]));
				bool keepGoing = cursor.MoveToNext();
				while (keepGoing)
				{
					long docNumericID = cursor.GetLong(0);
					string docId = cursor.GetString(1);
					string revId = cursor.GetString(2);
					long sequenceNumber = cursor.GetLong(3);
					bool deleted = includeDeletedDocs && cursor.GetInt(GetDeletedColumnIndex(options)
						) > 0;
					IDictionary<string, object> docContents = null;
					if (options.IsIncludeDocs())
					{
						byte[] json = cursor.GetBlob(4);
						docContents = DocumentPropertiesFromJSON(json, docId, revId, deleted, sequenceNumber
							, options.GetContentOptions());
					}
					// Iterate over following rows with the same doc_id -- these are conflicts.
					// Skip them, but collect their revIDs if the 'conflicts' option is set:
					IList<string> conflicts = new AList<string>();
					while (((keepGoing = cursor.MoveToNext()) == true) && cursor.GetLong(0) == docNumericID
						)
					{
						if (options.GetAllDocsMode() == Query.AllDocsMode.ShowConflicts || options.GetAllDocsMode
							() == Query.AllDocsMode.OnlyConflicts)
						{
							if (conflicts.IsEmpty())
							{
								conflicts.AddItem(revId);
							}
							conflicts.AddItem(cursor.GetString(2));
						}
					}
					if (options.GetAllDocsMode() == Query.AllDocsMode.OnlyConflicts && conflicts.IsEmpty
						())
					{
						continue;
					}
					IDictionary<string, object> value = new Dictionary<string, object>();
					value.Put("rev", revId);
					value.Put("_conflicts", conflicts);
					if (includeDeletedDocs)
					{
						value.Put("deleted", (deleted ? true : null));
					}
					QueryRow change = new QueryRow(docId, sequenceNumber, docId, value, docContents);
					change.SetDatabase(this);
					if (options.GetKeys() != null)
					{
						docs.Put(docId, change);
					}
					else
					{
						rows.AddItem(change);
					}
				}
				if (options.GetKeys() != null)
				{
					foreach (object docIdObject in options.GetKeys())
					{
						if (docIdObject is string)
						{
							string docId = (string)docIdObject;
							QueryRow change = docs.Get(docId);
							if (change == null)
							{
								IDictionary<string, object> value = new Dictionary<string, object>();
								long docNumericID = GetDocNumericID(docId);
								if (docNumericID > 0)
								{
									bool deleted;
									IList<bool> outIsDeleted = new AList<bool>();
									IList<bool> outIsConflict = new AList<bool>();
									string revId = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict);
									if (outIsDeleted.Count > 0)
									{
										deleted = true;
									}
									if (revId != null)
									{
										value.Put("rev", revId);
										value.Put("deleted", true);
									}
								}
								change = new QueryRow((value != null ? docId : null), 0, docId, value, null);
								change.SetDatabase(this);
							}
							rows.AddItem(change);
						}
					}
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting all docs", e);
				throw new CouchbaseLiteException("Error getting all docs", e, new Status(Status.InternalServerError
					));
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
			result.Put("rows", rows);
			result.Put("total_rows", rows.Count);
			result.Put("offset", options.GetSkip());
			if (updateSeq != 0)
			{
				result.Put("update_seq", updateSeq);
			}
			return result;
		}

		/// <summary>Returns the rev ID of the 'winning' revision of this document, and whether it's deleted.
		/// 	</summary>
		/// <remarks>Returns the rev ID of the 'winning' revision of this document, and whether it's deleted.
		/// 	</remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal virtual string WinningRevIDOfDoc(long docNumericId, IList<bool> outIsDeleted
			, IList<bool> outIsConflict)
		{
			Cursor cursor = null;
			string sql = "SELECT revid, deleted FROM revs" + " WHERE doc_id=? and current=1" 
				+ " ORDER BY deleted asc, revid desc LIMIT 2";
			string[] args = new string[] { System.Convert.ToString(docNumericId) };
			string revId = null;
			try
			{
				cursor = database.RawQuery(sql, args);
				cursor.MoveToNext();
				if (!cursor.IsAfterLast())
				{
					revId = cursor.GetString(0);
					bool deleted = cursor.GetInt(1) > 0;
					if (deleted)
					{
						outIsDeleted.AddItem(true);
					}
					// The document is in conflict if there are two+ result rows that are not deletions.
					bool hasNextResult = cursor.MoveToNext();
					if (hasNextResult)
					{
						bool isNextDeleted = cursor.GetInt(1) > 0;
						bool isInConflict = !deleted && hasNextResult && isNextDeleted;
						if (isInConflict)
						{
							outIsConflict.AddItem(true);
						}
					}
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error", e);
				throw new CouchbaseLiteException("Error", e, new Status(Status.InternalServerError
					));
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

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal virtual void InsertAttachmentForSequence(AttachmentInternal attachment, 
			long sequence)
		{
			InsertAttachmentForSequenceWithNameAndType(sequence, attachment.GetName(), attachment
				.GetContentType(), attachment.GetRevpos(), attachment.GetBlobKey());
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual void InsertAttachmentForSequenceWithNameAndType(InputStream contentStream
			, long sequence, string name, string contentType, int revpos)
		{
			System.Diagnostics.Debug.Assert((sequence > 0));
			System.Diagnostics.Debug.Assert((name != null));
			BlobKey key = new BlobKey();
			if (!attachments.StoreBlobStream(contentStream, key))
			{
				throw new CouchbaseLiteException(Status.InternalServerError);
			}
			InsertAttachmentForSequenceWithNameAndType(sequence, name, contentType, revpos, key
				);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual void InsertAttachmentForSequenceWithNameAndType(long sequence, string
			 name, string contentType, int revpos, BlobKey key)
		{
			try
			{
				ContentValues args = new ContentValues();
				args.Put("sequence", sequence);
				args.Put("filename", name);
				if (key != null)
				{
					args.Put("key", key.GetBytes());
					args.Put("length", attachments.GetSizeOfBlob(key));
				}
				args.Put("type", contentType);
				args.Put("revpos", revpos);
				long result = database.Insert("attachments", null, args);
				if (result == -1)
				{
					string msg = "Insert attachment failed (returned -1)";
					Log.E(Database.Tag, msg);
					throw new CouchbaseLiteException(msg, Status.InternalServerError);
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error inserting attachment", e);
				throw new CouchbaseLiteException(e, Status.InternalServerError);
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal virtual void InstallAttachment(AttachmentInternal attachment, IDictionary
			<string, object> attachInfo)
		{
			string digest = (string)attachInfo.Get("digest");
			if (digest == null)
			{
				throw new CouchbaseLiteException(Status.BadAttachment);
			}
			if (pendingAttachmentsByDigest != null && pendingAttachmentsByDigest.ContainsKey(
				digest))
			{
				BlobStoreWriter writer = pendingAttachmentsByDigest.Get(digest);
				try
				{
					BlobStoreWriter blobStoreWriter = (BlobStoreWriter)writer;
					blobStoreWriter.Install();
					attachment.SetBlobKey(blobStoreWriter.GetBlobKey());
					attachment.SetLength(blobStoreWriter.GetLength());
				}
				catch (Exception e)
				{
					throw new CouchbaseLiteException(e, Status.StatusAttachmentError);
				}
			}
		}

		[InterfaceAudience.Private]
		private IDictionary<string, BlobStoreWriter> GetPendingAttachmentsByDigest()
		{
			if (pendingAttachmentsByDigest == null)
			{
				pendingAttachmentsByDigest = new Dictionary<string, BlobStoreWriter>();
			}
			return pendingAttachmentsByDigest;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual void CopyAttachmentNamedFromSequenceToSequence(string name, long fromSeq
			, long toSeq)
		{
			System.Diagnostics.Debug.Assert((name != null));
			System.Diagnostics.Debug.Assert((toSeq > 0));
			if (fromSeq < 0)
			{
				throw new CouchbaseLiteException(Status.NotFound);
			}
			Cursor cursor = null;
			string[] args = new string[] { System.Convert.ToString(toSeq), name, System.Convert.ToString
				(fromSeq), name };
			try
			{
				database.ExecSQL("INSERT INTO attachments (sequence, filename, key, type, length, revpos) "
					 + "SELECT ?, ?, key, type, length, revpos FROM attachments " + "WHERE sequence=? AND filename=?"
					, args);
				cursor = database.RawQuery("SELECT changes()", null);
				cursor.MoveToNext();
				int rowsUpdated = cursor.GetInt(0);
				if (rowsUpdated == 0)
				{
					// Oops. This means a glitch in our attachment-management or pull code,
					// or else a bug in the upstream server.
					Log.W(Database.Tag, "Can't find inherited attachment " + name + " from seq# " + System.Convert.ToString
						(fromSeq) + " to copy to " + System.Convert.ToString(toSeq));
					throw new CouchbaseLiteException(Status.NotFound);
				}
				else
				{
					return;
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error copying attachment", e);
				throw new CouchbaseLiteException(Status.InternalServerError);
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
		}

		/// <summary>Returns the content and MIME type of an attachment</summary>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual Attachment GetAttachmentForSequence(long sequence, string filename
			)
		{
			System.Diagnostics.Debug.Assert((sequence > 0));
			System.Diagnostics.Debug.Assert((filename != null));
			Cursor cursor = null;
			string[] args = new string[] { System.Convert.ToString(sequence), filename };
			try
			{
				cursor = database.RawQuery("SELECT key, type FROM attachments WHERE sequence=? AND filename=?"
					, args);
				if (!cursor.MoveToNext())
				{
					throw new CouchbaseLiteException(Status.NotFound);
				}
				byte[] keyData = cursor.GetBlob(0);
				//TODO add checks on key here? (ios version)
				BlobKey key = new BlobKey(keyData);
				InputStream contentStream = attachments.BlobStreamForKey(key);
				if (contentStream == null)
				{
					Log.E(Database.Tag, "Failed to load attachment");
					throw new CouchbaseLiteException(Status.InternalServerError);
				}
				else
				{
					Attachment result = new Attachment(contentStream, cursor.GetString(1));
					result.SetGZipped(attachments.IsGZipped(key));
					return result;
				}
			}
			catch (SQLException)
			{
				throw new CouchbaseLiteException(Status.InternalServerError);
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
		}

		/// <summary>Returns the location of an attachment's file in the blob store.</summary>
		/// <remarks>Returns the location of an attachment's file in the blob store.</remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal virtual string GetAttachmentPathForSequence(long sequence, string filename
			)
		{
			System.Diagnostics.Debug.Assert((sequence > 0));
			System.Diagnostics.Debug.Assert((filename != null));
			Cursor cursor = null;
			string filePath = null;
			string[] args = new string[] { System.Convert.ToString(sequence), filename };
			try
			{
				cursor = database.RawQuery("SELECT key, type, encoding FROM attachments WHERE sequence=? AND filename=?"
					, args);
				if (!cursor.MoveToNext())
				{
					throw new CouchbaseLiteException(Status.NotFound);
				}
				byte[] keyData = cursor.GetBlob(0);
				BlobKey key = new BlobKey(keyData);
				filePath = GetAttachments().PathForKey(key);
				return filePath;
			}
			catch (SQLException)
			{
				throw new CouchbaseLiteException(Status.InternalServerError);
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
		}

		/// <summary>Constructs an "_attachments" dictionary for a revision, to be inserted in its JSON body.
		/// 	</summary>
		/// <remarks>Constructs an "_attachments" dictionary for a revision, to be inserted in its JSON body.
		/// 	</remarks>
		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> GetAttachmentsDictForSequenceWithContent
			(long sequence, EnumSet<Database.TDContentOptions> contentOptions)
		{
			System.Diagnostics.Debug.Assert((sequence > 0));
			Cursor cursor = null;
			string[] args = new string[] { System.Convert.ToString(sequence) };
			try
			{
				cursor = database.RawQuery("SELECT filename, key, type, length, revpos FROM attachments WHERE sequence=?"
					, args);
				if (!cursor.MoveToNext())
				{
					return null;
				}
				IDictionary<string, object> result = new Dictionary<string, object>();
				while (!cursor.IsAfterLast())
				{
					bool dataSuppressed = false;
					int length = cursor.GetInt(3);
					byte[] keyData = cursor.GetBlob(1);
					BlobKey key = new BlobKey(keyData);
					string digestString = "sha1-" + Base64.EncodeBytes(keyData);
					string dataBase64 = null;
					if (contentOptions.Contains(Database.TDContentOptions.TDIncludeAttachments))
					{
						if (contentOptions.Contains(Database.TDContentOptions.TDBigAttachmentsFollow) && 
							length >= Database.kBigAttachmentLength)
						{
							dataSuppressed = true;
						}
						else
						{
							byte[] data = attachments.BlobForKey(key);
							if (data != null)
							{
								dataBase64 = Base64.EncodeBytes(data);
							}
							else
							{
								// <-- very expensive
								Log.W(Database.Tag, "Error loading attachment");
							}
						}
					}
					IDictionary<string, object> attachment = new Dictionary<string, object>();
					if (dataBase64 == null || dataSuppressed == true)
					{
						attachment.Put("stub", true);
					}
					if (dataBase64 != null)
					{
						attachment.Put("data", dataBase64);
					}
					if (dataSuppressed == true)
					{
						attachment.Put("follows", true);
					}
					attachment.Put("digest", digestString);
					string contentType = cursor.GetString(2);
					attachment.Put("content_type", contentType);
					attachment.Put("length", length);
					attachment.Put("revpos", cursor.GetInt(4));
					string filename = cursor.GetString(0);
					result.Put(filename, attachment);
					cursor.MoveToNext();
				}
				return result;
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting attachments for sequence", e);
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

		/// <summary>Modifies a RevisionInternal's body by changing all attachments with revpos &lt; minRevPos into stubs.
		/// 	</summary>
		/// <remarks>Modifies a RevisionInternal's body by changing all attachments with revpos &lt; minRevPos into stubs.
		/// 	</remarks>
		/// <param name="rev"></param>
		/// <param name="minRevPos"></param>
		[InterfaceAudience.Private]
		public virtual void StubOutAttachmentsIn(RevisionInternal rev, int minRevPos)
		{
			if (minRevPos <= 1)
			{
				return;
			}
			IDictionary<string, object> properties = (IDictionary<string, object>)rev.GetProperties
				();
			IDictionary<string, object> attachments = null;
			if (properties != null)
			{
				attachments = (IDictionary<string, object>)properties.Get("_attachments");
			}
			IDictionary<string, object> editedProperties = null;
			IDictionary<string, object> editedAttachments = null;
			foreach (string name in attachments.Keys)
			{
				IDictionary<string, object> attachment = (IDictionary<string, object>)attachments
					.Get(name);
				int revPos = (int)attachment.Get("revpos");
				object stub = attachment.Get("stub");
				if (revPos > 0 && revPos < minRevPos && (stub == null))
				{
					// Strip this attachment's body. First make its dictionary mutable:
					if (editedProperties == null)
					{
						editedProperties = new Dictionary<string, object>(properties);
						editedAttachments = new Dictionary<string, object>(attachments);
						editedProperties.Put("_attachments", editedAttachments);
					}
					// ...then remove the 'data' and 'follows' key:
					IDictionary<string, object> editedAttachment = new Dictionary<string, object>(attachment
						);
					Sharpen.Collections.Remove(editedAttachment, "data");
					Sharpen.Collections.Remove(editedAttachment, "follows");
					editedAttachment.Put("stub", true);
					editedAttachments.Put(name, editedAttachment);
					Log.D(Database.Tag, "Stubbed out attachment" + rev + " " + name + ": revpos" + revPos
						 + " " + minRevPos);
				}
			}
			if (editedProperties != null)
			{
				rev.SetProperties(editedProperties);
			}
		}

		[InterfaceAudience.Private]
		internal virtual void StubOutAttachmentsInRevision(IDictionary<string, AttachmentInternal
			> attachments, RevisionInternal rev)
		{
			IDictionary<string, object> properties = rev.GetProperties();
			IDictionary<string, object> attachmentsFromProps = (IDictionary<string, object>)properties
				.Get("_attachments");
			if (attachmentsFromProps != null)
			{
				foreach (string attachmentKey in attachmentsFromProps.Keys)
				{
					IDictionary<string, object> attachmentFromProps = (IDictionary<string, object>)attachmentsFromProps
						.Get(attachmentKey);
					if (attachmentFromProps.Get("follows") != null || attachmentFromProps.Get("data")
						 != null)
					{
						Sharpen.Collections.Remove(attachmentFromProps, "follows");
						Sharpen.Collections.Remove(attachmentFromProps, "data");
						attachmentFromProps.Put("stub", true);
						if (attachmentFromProps.Get("revpos") == null)
						{
							attachmentFromProps.Put("revpos", rev.GetGeneration());
						}
						AttachmentInternal attachmentObject = attachments.Get(attachmentKey);
						if (attachmentObject != null)
						{
							attachmentFromProps.Put("length", attachmentObject.GetLength());
							if (attachmentObject.GetBlobKey() != null)
							{
								// case with Large Attachment
								attachmentFromProps.Put("digest", attachmentObject.GetBlobKey().Base64Digest());
							}
						}
						attachmentsFromProps.Put(attachmentKey, attachmentFromProps);
					}
				}
			}
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
		[InterfaceAudience.Private]
		internal virtual void ProcessAttachmentsForRevision(IDictionary<string, AttachmentInternal
			> attachments, RevisionInternal rev, long parentSequence)
		{
			System.Diagnostics.Debug.Assert((rev != null));
			long newSequence = rev.GetSequence();
			System.Diagnostics.Debug.Assert((newSequence > parentSequence));
			int generation = rev.GetGeneration();
			System.Diagnostics.Debug.Assert((generation > 0));
			// If there are no attachments in the new rev, there's nothing to do:
			IDictionary<string, object> revAttachments = null;
			IDictionary<string, object> properties = (IDictionary<string, object>)rev.GetProperties
				();
			if (properties != null)
			{
				revAttachments = (IDictionary<string, object>)properties.Get("_attachments");
			}
			if (revAttachments == null || revAttachments.Count == 0 || rev.IsDeleted())
			{
				return;
			}
			foreach (string name in revAttachments.Keys)
			{
				AttachmentInternal attachment = attachments.Get(name);
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
							Log.W(Database.Tag, string.Format("Attachment %s %s has unexpected revpos %s, setting to %s"
								, rev, name, attachment.GetRevpos(), generation));
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

		/// <summary>Updates or deletes an attachment, creating a new document revision in the process.
		/// 	</summary>
		/// <remarks>
		/// Updates or deletes an attachment, creating a new document revision in the process.
		/// Used by the PUT / DELETE methods called on attachment URLs.
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual RevisionInternal UpdateAttachment(string filename, InputStream contentStream
			, string contentType, string docID, string oldRevID)
		{
			bool isSuccessful = false;
			if (filename == null || filename.Length == 0 || (contentStream != null && contentType
				 == null) || (oldRevID != null && docID == null) || (contentStream != null && docID
				 == null))
			{
				throw new CouchbaseLiteException(Status.BadRequest);
			}
			BeginTransaction();
			try
			{
				RevisionInternal oldRev = new RevisionInternal(docID, oldRevID, false, this);
				if (oldRevID != null)
				{
					// Load existing revision if this is a replacement:
					try
					{
						LoadRevisionBody(oldRev, EnumSet.NoneOf<Database.TDContentOptions>());
					}
					catch (CouchbaseLiteException e)
					{
						if (e.GetCBLStatus().GetCode() == Status.NotFound && ExistsDocumentWithIDAndRev(docID
							, null))
						{
							throw new CouchbaseLiteException(Status.Conflict);
						}
					}
					IDictionary<string, object> oldRevProps = oldRev.GetProperties();
					IDictionary<string, object> attachments = null;
					if (oldRevProps != null)
					{
						attachments = (IDictionary<string, object>)oldRevProps.Get("_attachments");
					}
					if (contentStream == null && attachments != null && !attachments.ContainsKey(filename
						))
					{
						throw new CouchbaseLiteException(Status.NotFound);
					}
					// Remove the _attachments stubs so putRevision: doesn't copy the rows for me
					// OPT: Would be better if I could tell loadRevisionBody: not to add it
					if (attachments != null)
					{
						IDictionary<string, object> properties = new Dictionary<string, object>(oldRev.GetProperties
							());
						Sharpen.Collections.Remove(properties, "_attachments");
						oldRev.SetBody(new Body(properties));
					}
				}
				else
				{
					// If this creates a new doc, it needs a body:
					oldRev.SetBody(new Body(new Dictionary<string, object>()));
				}
				// Create a new revision:
				Status putStatus = new Status();
				RevisionInternal newRev = PutRevision(oldRev, oldRevID, false, putStatus);
				if (newRev == null)
				{
					return null;
				}
				if (oldRevID != null)
				{
					// Copy all attachment rows _except_ for the one being updated:
					string[] args = new string[] { System.Convert.ToString(newRev.GetSequence()), System.Convert.ToString
						(oldRev.GetSequence()), filename };
					database.ExecSQL("INSERT INTO attachments " + "(sequence, filename, key, type, length, revpos) "
						 + "SELECT ?, filename, key, type, length, revpos FROM attachments " + "WHERE sequence=? AND filename != ?"
						, args);
				}
				if (contentStream != null)
				{
					// If not deleting, add a new attachment entry:
					InsertAttachmentForSequenceWithNameAndType(contentStream, newRev.GetSequence(), filename
						, contentType, newRev.GetGeneration());
				}
				isSuccessful = true;
				return newRev;
			}
			catch (SQLException e)
			{
				Log.E(Tag, "Error updating attachment", e);
				throw new CouchbaseLiteException(new Status(Status.InternalServerError));
			}
			finally
			{
				EndTransaction(isSuccessful);
			}
		}

		[InterfaceAudience.Private]
		public virtual void RememberAttachmentWritersForDigests(IDictionary<string, BlobStoreWriter
			> blobsByDigest)
		{
			GetPendingAttachmentsByDigest().PutAll(blobsByDigest);
		}

		[InterfaceAudience.Private]
		internal virtual void RememberAttachmentWriter(BlobStoreWriter writer)
		{
			GetPendingAttachmentsByDigest().Put(writer.MD5DigestString(), writer);
		}

		/// <summary>Deletes obsolete attachments from the sqliteDb and blob store.</summary>
		/// <remarks>Deletes obsolete attachments from the sqliteDb and blob store.</remarks>
		[InterfaceAudience.Private]
		public virtual Status GarbageCollectAttachments()
		{
			// First delete attachment rows for already-cleared revisions:
			// OPT: Could start after last sequence# we GC'd up to
			try
			{
				database.ExecSQL("DELETE FROM attachments WHERE sequence IN " + "(SELECT sequence from revs WHERE json IS null)"
					);
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error deleting attachments", e);
			}
			// Now collect all remaining attachment IDs and tell the store to delete all but these:
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery("SELECT DISTINCT key FROM attachments", null);
				cursor.MoveToNext();
				IList<BlobKey> allKeys = new AList<BlobKey>();
				while (!cursor.IsAfterLast())
				{
					BlobKey key = new BlobKey(cursor.GetBlob(0));
					allKeys.AddItem(key);
					cursor.MoveToNext();
				}
				int numDeleted = attachments.DeleteBlobsExceptWithKeys(allKeys);
				if (numDeleted < 0)
				{
					return new Status(Status.InternalServerError);
				}
				Log.V(Database.Tag, "Deleted " + numDeleted + " attachments");
				return new Status(Status.Ok);
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error finding attachment keys in use", e);
				return new Status(Status.InternalServerError);
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
		}

		/// <summary>DOCUMENT & REV IDS:</summary>
		[InterfaceAudience.Private]
		public static bool IsValidDocumentId(string id)
		{
			// http://wiki.apache.org/couchdb/HTTP_Document_API#Documents
			if (id == null || id.Length == 0)
			{
				return false;
			}
			if (id[0] == '_')
			{
				return (id.StartsWith("_design/"));
			}
			return true;
		}

		// "_local/*" is not a valid document ID. Local docs have their own API and shouldn't get here.
		[InterfaceAudience.Private]
		public static string GenerateDocumentId()
		{
			return Misc.TDCreateUUID();
		}

		[InterfaceAudience.Private]
		public virtual string GenerateNextRevisionID(string revisionId)
		{
			// Revision IDs have a generation count, a hyphen, and a UUID.
			int generation = 0;
			if (revisionId != null)
			{
				generation = RevisionInternal.GenerationFromRevID(revisionId);
				if (generation == 0)
				{
					return null;
				}
			}
			string digest = Misc.TDCreateUUID();
			// TODO: Generate canonical digest of body
			return Sharpen.Extensions.ToString(generation + 1) + "-" + digest;
		}

		[InterfaceAudience.Private]
		public virtual long InsertDocumentID(string docId)
		{
			long rowId = -1;
			try
			{
				ContentValues args = new ContentValues();
				args.Put("docid", docId);
				rowId = database.Insert("docs", null, args);
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error inserting document id", e);
			}
			return rowId;
		}

		[InterfaceAudience.Private]
		public virtual long GetOrInsertDocNumericID(string docId)
		{
			long docNumericId = GetDocNumericID(docId);
			if (docNumericId == 0)
			{
				docNumericId = InsertDocumentID(docId);
			}
			return docNumericId;
		}

		/// <summary>Parses the _revisions dict from a document into an array of revision ID strings
		/// 	</summary>
		[InterfaceAudience.Private]
		public static IList<string> ParseCouchDBRevisionHistory(IDictionary<string, object
			> docProperties)
		{
			IDictionary<string, object> revisions = (IDictionary<string, object>)docProperties
				.Get("_revisions");
			if (revisions == null)
			{
				return null;
			}
			IList<string> revIDs = (IList<string>)revisions.Get("ids");
			int start = (int)revisions.Get("start");
			if (start != null)
			{
				for (int i = 0; i < revIDs.Count; i++)
				{
					string revID = revIDs[i];
					revIDs.Set(i, Sharpen.Extensions.ToString(start--) + "-" + revID);
				}
			}
			return revIDs;
		}

		/// <summary>INSERTION:</summary>
		[InterfaceAudience.Private]
		public virtual byte[] EncodeDocumentJSON(RevisionInternal rev)
		{
			IDictionary<string, object> origProps = rev.GetProperties();
			if (origProps == null)
			{
				return null;
			}
			// Don't allow any "_"-prefixed keys. Known ones we'll ignore, unknown ones are an error.
			IDictionary<string, object> properties = new Dictionary<string, object>(origProps
				.Count);
			foreach (string key in origProps.Keys)
			{
				if (key.StartsWith("_"))
				{
					if (!KnownSpecialKeys.Contains(key))
					{
						Log.E(Tag, "Database: Invalid top-level key '" + key + "' in document to be inserted"
							);
						return null;
					}
				}
				else
				{
					properties.Put(key, origProps.Get(key));
				}
			}
			byte[] json = null;
			try
			{
				json = Manager.GetObjectMapper().WriteValueAsBytes(properties);
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error serializing " + rev + " to JSON", e);
			}
			return json;
		}

		[InterfaceAudience.Private]
		public virtual void NotifyChange(RevisionInternal rev, RevisionInternal winningRev
			, Uri source, bool inConflict)
		{
			// TODO: it is currently sending one change at a time rather than batching them up
			bool isExternalFixMe = false;
			// TODO: fix this to have a real value
			DocumentChange change = new DocumentChange(rev, winningRev, inConflict, source);
			IList<DocumentChange> changes = new AList<DocumentChange>();
			changes.AddItem(change);
			Database.ChangeEvent changeEvent = new Database.ChangeEvent(this, isExternalFixMe
				, changes);
			foreach (Database.ChangeListener changeListener in changeListeners)
			{
				changeListener.Changed(changeEvent);
			}
			// TODO: this is expensive, it should be using a WeakHashMap
			// TODO: instead of loading from the DB.  iOS code below.
			Document document = GetDocument(change.GetDocumentId());
			document.RevisionAdded(change);
		}

		[InterfaceAudience.Private]
		public virtual long InsertRevision(RevisionInternal rev, long docNumericID, long 
			parentSequence, bool current, byte[] data)
		{
			long rowId = 0;
			try
			{
				ContentValues args = new ContentValues();
				args.Put("doc_id", docNumericID);
				args.Put("revid", rev.GetRevId());
				if (parentSequence != 0)
				{
					args.Put("parent", parentSequence);
				}
				args.Put("current", current);
				args.Put("deleted", rev.IsDeleted());
				args.Put("json", data);
				rowId = database.Insert("revs", null, args);
				rev.SetSequence(rowId);
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error inserting revision", e);
			}
			return rowId;
		}

		// TODO: move this to internal API
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual RevisionInternal PutRevision(RevisionInternal rev, string prevRevId
			, Status resultStatus)
		{
			return PutRevision(rev, prevRevId, false, resultStatus);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual RevisionInternal PutRevision(RevisionInternal rev, string prevRevId
			, bool allowConflict)
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
		/// 	</param>
		/// <param name="prevRevId">The ID of the revision to replace (same as the "?rev=" parameter to a PUT), or null if this is a new document.
		/// 	</param>
		/// <param name="allowConflict">If false, an error status 409 will be returned if the insertion would create a conflict, i.e. if the previous revision already has a child.
		/// 	</param>
		/// <param name="resultStatus">On return, an HTTP status code indicating success or failure.
		/// 	</param>
		/// <returns>A new RevisionInternal with the docID, revID and sequence filled in (but no body).
		/// 	</returns>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual RevisionInternal PutRevision(RevisionInternal oldRev, string prevRevId
			, bool allowConflict, Status resultStatus)
		{
			// prevRevId is the rev ID being replaced, or nil if an insert
			string docId = oldRev.GetDocId();
			bool deleted = oldRev.IsDeleted();
			if ((oldRev == null) || ((prevRevId != null) && (docId == null)) || (deleted && (
				docId == null)) || ((docId != null) && !IsValidDocumentId(docId)))
			{
				throw new CouchbaseLiteException(Status.BadRequest);
			}
			BeginTransaction();
			Cursor cursor = null;
			bool inConflict = false;
			RevisionInternal winningRev = null;
			RevisionInternal newRev = null;
			//// PART I: In which are performed lookups and validations prior to the insert...
			long docNumericID = (docId != null) ? GetDocNumericID(docId) : 0;
			long parentSequence = 0;
			string oldWinningRevID = null;
			try
			{
				bool oldWinnerWasDeletion = false;
				bool wasConflicted = false;
				if (docNumericID > 0)
				{
					IList<bool> outIsDeleted = new AList<bool>();
					IList<bool> outIsConflict = new AList<bool>();
					try
					{
						oldWinningRevID = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict);
						if (outIsDeleted.Count > 0)
						{
							oldWinnerWasDeletion = true;
						}
						if (outIsConflict.Count > 0)
						{
							wasConflicted = true;
						}
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
						string msg = string.Format("No existing revision found with doc id: %s", docId);
						throw new CouchbaseLiteException(msg, Status.NotFound);
					}
					string[] args = new string[] { System.Convert.ToString(docNumericID), prevRevId };
					string additionalWhereClause = string.Empty;
					if (!allowConflict)
					{
						additionalWhereClause = "AND current=1";
					}
					cursor = database.RawQuery("SELECT sequence FROM revs WHERE doc_id=? AND revid=? "
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
							string msg = string.Format("Conflicts not allowed and there is already an existing doc with id: %s"
								, docId);
							throw new CouchbaseLiteException(msg, Status.Conflict);
						}
						else
						{
							string msg = string.Format("No existing revision found with doc id: %s", docId);
							throw new CouchbaseLiteException(msg, Status.NotFound);
						}
					}
					if (validations != null && validations.Count > 0)
					{
						// Fetch the previous revision and validate the new one against it:
						RevisionInternal prevRev = new RevisionInternal(docId, prevRevId, false, this);
						ValidateRevision(oldRev, prevRev);
					}
					// Make replaced rev non-current:
					ContentValues updateContent = new ContentValues();
					updateContent.Put("current", 0);
					database.Update("revs", updateContent, "sequence=" + parentSequence, null);
				}
				else
				{
					// Inserting first revision.
					if (deleted && (docId != null))
					{
						// Didn't specify a revision to delete: 404 or a 409, depending
						if (ExistsDocumentWithIDAndRev(docId, null))
						{
							throw new CouchbaseLiteException(Status.Conflict);
						}
						else
						{
							throw new CouchbaseLiteException(Status.NotFound);
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
							if (oldWinnerWasDeletion == true)
							{
								prevRevId = oldWinningRevID;
								parentSequence = GetSequenceOfDocument(docNumericID, prevRevId, false);
							}
							else
							{
								if (oldWinningRevID != null)
								{
									// The current winning revision is not deleted, so this is a conflict
									throw new CouchbaseLiteException(Status.Conflict);
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
				inConflict = wasConflicted || (!deleted && prevRevId != null && oldWinningRevID !=
					 null && !prevRevId.Equals(oldWinningRevID));
				//// PART II: In which insertion occurs...
				// Get the attachments:
				IDictionary<string, AttachmentInternal> attachments = GetAttachmentsFromRevision(
					oldRev);
				// Bump the revID and update the JSON:
				string newRevId = GenerateNextRevisionID(prevRevId);
				byte[] data = null;
				if (!oldRev.IsDeleted())
				{
					data = EncodeDocumentJSON(oldRev);
					if (data == null)
					{
						// bad or missing json
						throw new CouchbaseLiteException(Status.BadRequest);
					}
				}
				newRev = oldRev.CopyWithDocID(docId, newRevId);
				StubOutAttachmentsInRevision(attachments, newRev);
				// Now insert the rev itself:
				long newSequence = InsertRevision(newRev, docNumericID, parentSequence, true, data
					);
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
					resultStatus.SetCode(Status.Ok);
				}
				else
				{
					resultStatus.SetCode(Status.Created);
				}
			}
			catch (SQLException e1)
			{
				Log.E(Database.Tag, "Error putting revision", e1);
				return null;
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
				EndTransaction(resultStatus.IsSuccessful());
			}
			//// EPILOGUE: A change notification is sent...
			NotifyChange(newRev, winningRev, null, inConflict);
			return newRev;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal virtual RevisionInternal Winner(long docNumericID, string oldWinningRevID
			, bool oldWinnerWasDeletion, RevisionInternal newRev)
		{
			if (oldWinningRevID == null)
			{
				return newRev;
			}
			string newRevID = newRev.GetRevId();
			if (!newRev.IsDeleted())
			{
				if (oldWinnerWasDeletion || RevisionInternal.CBLCompareRevIDs(newRevID, oldWinningRevID
					) > 0)
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
					IList<bool> outIsDeleted = new AList<bool>();
					IList<bool> outIsConflict = new AList<bool>();
					string winningRevID = WinningRevIDOfDoc(docNumericID, outIsDeleted, outIsConflict
						);
					if (!winningRevID.Equals(oldWinningRevID))
					{
						if (winningRevID.Equals(newRev.GetRevId()))
						{
							return newRev;
						}
						else
						{
							bool deleted = false;
							RevisionInternal winningRev = new RevisionInternal(newRev.GetDocId(), winningRevID
								, deleted, this);
							return winningRev;
						}
					}
				}
			}
			return null;
		}

		// no change
		[InterfaceAudience.Private]
		private long GetSequenceOfDocument(long docNumericId, string revId, bool onlyCurrent
			)
		{
			long result = -1;
			Cursor cursor = null;
			try
			{
				string extraSql = (onlyCurrent ? "AND current=1" : string.Empty);
				string sql = string.Format("SELECT sequence FROM revs WHERE doc_id=? AND revid=? %s LIMIT 1"
					, extraSql);
				string[] args = new string[] { string.Empty + docNumericId, revId };
				cursor = database.RawQuery(sql, args);
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
				Log.E(Database.Tag, "Error getting getSequenceOfDocument", e);
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

		/// <summary>
		/// Given a revision, read its _attachments dictionary (if any), convert each attachment to a
		/// AttachmentInternal object, and return a dictionary mapping names-&gt;CBL_Attachments.
		/// </summary>
		/// <remarks>
		/// Given a revision, read its _attachments dictionary (if any), convert each attachment to a
		/// AttachmentInternal object, and return a dictionary mapping names-&gt;CBL_Attachments.
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		internal virtual IDictionary<string, AttachmentInternal> GetAttachmentsFromRevision
			(RevisionInternal rev)
		{
			IDictionary<string, object> revAttachments = (IDictionary<string, object>)rev.GetPropertyForKey
				("_attachments");
			if (revAttachments == null || revAttachments.Count == 0 || rev.IsDeleted())
			{
				return new Dictionary<string, AttachmentInternal>();
			}
			IDictionary<string, AttachmentInternal> attachments = new Dictionary<string, AttachmentInternal
				>();
			foreach (string name in revAttachments.Keys)
			{
				IDictionary<string, object> attachInfo = (IDictionary<string, object>)revAttachments
					.Get(name);
				string contentType = (string)attachInfo.Get("content_type");
				AttachmentInternal attachment = new AttachmentInternal(name, contentType);
				string newContentBase64 = (string)attachInfo.Get("data");
				if (newContentBase64 != null)
				{
					// If there's inline attachment data, decode and store it:
					byte[] newContents;
					try
					{
						newContents = Base64.Decode(newContentBase64);
					}
					catch (IOException e)
					{
						throw new CouchbaseLiteException(e, Status.BadEncoding);
					}
					attachment.SetLength(newContents.Length);
					BlobKey outBlobKey = new BlobKey();
					bool storedBlob = GetAttachments().StoreBlob(newContents, outBlobKey);
					attachment.SetBlobKey(outBlobKey);
					if (!storedBlob)
					{
						throw new CouchbaseLiteException(Status.StatusAttachmentError);
					}
				}
				else
				{
					if (attachInfo.ContainsKey("follows") && ((bool)attachInfo.Get("follows")) == true)
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
							throw new CouchbaseLiteException("Expected this attachment to be a stub", Status.
								BadAttachment);
						}
						int revPos = ((int)attachInfo.Get("revpos"));
						if (revPos <= 0)
						{
							throw new CouchbaseLiteException("Invalid revpos: " + revPos, Status.BadAttachment
								);
						}
						continue;
					}
				}
				// Handle encoded attachment:
				string encodingStr = (string)attachInfo.Get("encoding");
				if (encodingStr != null && encodingStr.Length > 0)
				{
					if (Sharpen.Runtime.EqualsIgnoreCase(encodingStr, "gzip"))
					{
						attachment.SetEncoding(AttachmentInternal.AttachmentEncoding.AttachmentEncodingGZIP
							);
					}
					else
					{
						throw new CouchbaseLiteException("Unnkown encoding: " + encodingStr, Status.BadEncoding
							);
					}
					attachment.SetEncodedLength(attachment.GetLength());
					attachment.SetLength((long)attachInfo.Get("length"));
				}
				if (attachInfo.ContainsKey("revpos"))
				{
					attachment.SetRevpos((int)attachInfo.Get("revpos"));
				}
				else
				{
					attachment.SetRevpos(1);
				}
				attachments.Put(name, attachment);
			}
			return attachments;
		}

		/// <summary>Inserts an already-existing revision replicated from a remote sqliteDb.</summary>
		/// <remarks>
		/// Inserts an already-existing revision replicated from a remote sqliteDb.
		/// It must already have a revision ID. This may create a conflict! The revision's history must be given; ancestor revision IDs that don't already exist locally will create phantom revisions with no content.
		/// </remarks>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual void ForceInsert(RevisionInternal rev, IList<string> revHistory, Uri
			 source)
		{
			string docId = rev.GetDocId();
			string revId = rev.GetRevId();
			if (!IsValidDocumentId(docId) || (revId == null))
			{
				throw new CouchbaseLiteException(Status.BadRequest);
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
					throw new CouchbaseLiteException(Status.BadRequest);
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
					throw new CouchbaseLiteException(Status.InternalServerError);
				}
				// Walk through the remote history in chronological order, matching each revision ID to
				// a local revision. When the list diverges, start creating blank local revisions to fill
				// in the local history:
				long sequence = 0;
				long localParentSequence = 0;
				for (int i = revHistory.Count - 1; i >= 0; --i)
				{
					revId = revHistory[i];
					RevisionInternal localRev = localRevs.RevWithDocIdAndRevId(docId, revId);
					if (localRev != null)
					{
						// This revision is known locally. Remember its sequence as the parent of the next one:
						sequence = localRev.GetSequence();
						System.Diagnostics.Debug.Assert((sequence > 0));
						localParentSequence = sequence;
					}
					else
					{
						// This revision isn't known, so add it:
						RevisionInternal newRev;
						byte[] data = null;
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
									throw new CouchbaseLiteException(Status.BadRequest);
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
							throw new CouchbaseLiteException(Status.InternalServerError);
						}
						if (i == 0)
						{
							// Write any changed attachments for the new revision. As the parent sequence use
							// the latest local revision (this is to copy attachments from):
							IDictionary<string, AttachmentInternal> attachments = GetAttachmentsFromRevision(
								rev);
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
					args.Put("current", 0);
					string[] whereArgs = new string[] { System.Convert.ToString(localParentSequence) };
					try
					{
						database.Update("revs", args, "sequence=?", whereArgs);
					}
					catch (SQLException)
					{
						throw new CouchbaseLiteException(Status.InternalServerError);
					}
				}
				success = true;
			}
			catch (SQLException)
			{
				throw new CouchbaseLiteException(Status.InternalServerError);
			}
			finally
			{
				EndTransaction(success);
			}
			// Notify and return:
			bool inConflict = false;
			// TODO: can we be in conflict here?
			RevisionInternal winningRev = null;
			// TODO: what should we do here?
			NotifyChange(rev, winningRev, source, inConflict);
		}

		/// <summary>VALIDATION</summary>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual void ValidateRevision(RevisionInternal newRev, RevisionInternal oldRev
			)
		{
			if (validations == null || validations.Count == 0)
			{
				return;
			}
			ValidationContextImpl context = new ValidationContextImpl(this, oldRev, newRev);
			SavedRevision publicRev = new SavedRevision(this, newRev);
			foreach (string validationName in validations.Keys)
			{
				Validator validation = GetValidation(validationName);
				if (!validation.Validate(publicRev, context))
				{
					throw new CouchbaseLiteException(context.GetRejectMessage(), Status.Forbidden);
				}
			}
		}

		[InterfaceAudience.Private]
		public virtual Replication GetActiveReplicator(Uri remote, bool push)
		{
			if (activeReplicators != null)
			{
				foreach (Replication replicator in activeReplicators)
				{
					if (replicator.GetRemoteUrl().Equals(remote) && replicator.IsPull() == !push && replicator
						.IsRunning())
					{
						return replicator;
					}
				}
			}
			return null;
		}

		[InterfaceAudience.Private]
		public virtual Replication GetReplicator(Uri remote, bool push, bool continuous, 
			ScheduledExecutorService workExecutor)
		{
			Replication replicator = GetReplicator(remote, null, push, continuous, workExecutor
				);
			return replicator;
		}

		[InterfaceAudience.Private]
		public virtual Replication GetReplicator(string sessionId)
		{
			if (activeReplicators != null)
			{
				foreach (Replication replicator in activeReplicators)
				{
					if (replicator.GetSessionID().Equals(sessionId))
					{
						return replicator;
					}
				}
			}
			return null;
		}

		[InterfaceAudience.Private]
		public virtual Replication GetReplicator(Uri remote, HttpClientFactory httpClientFactory
			, bool push, bool continuous, ScheduledExecutorService workExecutor)
		{
			Replication result = GetActiveReplicator(remote, push);
			if (result != null)
			{
				return result;
			}
			result = push ? new Pusher(this, remote, continuous, httpClientFactory, workExecutor
				) : new Puller(this, remote, continuous, httpClientFactory, workExecutor);
			if (activeReplicators == null)
			{
				activeReplicators = new AList<Replication>();
			}
			activeReplicators.AddItem(result);
			return result;
		}

		[InterfaceAudience.Private]
		public virtual string LastSequenceWithRemoteURL(Uri url, bool push)
		{
			Cursor cursor = null;
			string result = null;
			try
			{
				string[] args = new string[] { url.ToExternalForm(), Sharpen.Extensions.ToString(
					push ? 1 : 0) };
				cursor = database.RawQuery("SELECT last_sequence FROM replicators WHERE remote=? AND push=?"
					, args);
				if (cursor.MoveToNext())
				{
					result = cursor.GetString(0);
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting last sequence", e);
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

		[InterfaceAudience.Private]
		public virtual bool SetLastSequence(string lastSequence, Uri url, bool push)
		{
			ContentValues values = new ContentValues();
			values.Put("remote", url.ToExternalForm());
			values.Put("push", push);
			values.Put("last_sequence", lastSequence);
			long newId = database.InsertWithOnConflict("replicators", null, values, SQLiteStorageEngine
				.ConflictReplace);
			return (newId == -1);
		}

		[InterfaceAudience.Private]
		public static string Quote(string @string)
		{
			return @string.Replace("'", "''");
		}

		[InterfaceAudience.Private]
		public static string JoinQuotedObjects(IList<object> objects)
		{
			IList<string> strings = new AList<string>();
			foreach (object @object in objects)
			{
				strings.AddItem(@object != null ? @object.ToString() : null);
			}
			return JoinQuoted(strings);
		}

		[InterfaceAudience.Private]
		public static string JoinQuoted(IList<string> strings)
		{
			if (strings.Count == 0)
			{
				return string.Empty;
			}
			string result = "'";
			bool first = true;
			foreach (string @string in strings)
			{
				if (first)
				{
					first = false;
				}
				else
				{
					result = result + "','";
				}
				result = result + Quote(@string);
			}
			result = result + "'";
			return result;
		}

		[InterfaceAudience.Private]
		public virtual bool FindMissingRevisions(RevisionList touchRevs)
		{
			if (touchRevs.Count == 0)
			{
				return true;
			}
			string quotedDocIds = JoinQuoted(touchRevs.GetAllDocIds());
			string quotedRevIds = JoinQuoted(touchRevs.GetAllRevIds());
			string sql = "SELECT docid, revid FROM revs, docs " + "WHERE docid IN (" + quotedDocIds
				 + ") AND revid in (" + quotedRevIds + ")" + " AND revs.doc_id == docs.doc_id";
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery(sql, null);
				cursor.MoveToNext();
				while (!cursor.IsAfterLast())
				{
					RevisionInternal rev = touchRevs.RevWithDocIdAndRevId(cursor.GetString(0), cursor
						.GetString(1));
					if (rev != null)
					{
						touchRevs.Remove(rev);
					}
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error finding missing revisions", e);
				return false;
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
			return true;
		}

		[InterfaceAudience.Private]
		internal static string MakeLocalDocumentId(string documentId)
		{
			return string.Format("_local/%s", documentId);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual RevisionInternal PutLocalRevision(RevisionInternal revision, string
			 prevRevID)
		{
			string docID = revision.GetDocId();
			if (!docID.StartsWith("_local/"))
			{
				throw new CouchbaseLiteException(Status.BadRequest);
			}
			if (!revision.IsDeleted())
			{
				// PUT:
				byte[] json = EncodeDocumentJSON(revision);
				string newRevID;
				if (prevRevID != null)
				{
					int generation = RevisionInternal.GenerationFromRevID(prevRevID);
					if (generation == 0)
					{
						throw new CouchbaseLiteException(Status.BadRequest);
					}
					newRevID = Sharpen.Extensions.ToString(++generation) + "-local";
					ContentValues values = new ContentValues();
					values.Put("revid", newRevID);
					values.Put("json", json);
					string[] whereArgs = new string[] { docID, prevRevID };
					try
					{
						int rowsUpdated = database.Update("localdocs", values, "docid=? AND revid=?", whereArgs
							);
						if (rowsUpdated == 0)
						{
							throw new CouchbaseLiteException(Status.Conflict);
						}
					}
					catch (SQLException e)
					{
						throw new CouchbaseLiteException(e, Status.InternalServerError);
					}
				}
				else
				{
					newRevID = "1-local";
					ContentValues values = new ContentValues();
					values.Put("docid", docID);
					values.Put("revid", newRevID);
					values.Put("json", json);
					try
					{
						database.InsertWithOnConflict("localdocs", null, values, SQLiteStorageEngine.ConflictIgnore
							);
					}
					catch (SQLException e)
					{
						throw new CouchbaseLiteException(e, Status.InternalServerError);
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

		/// <summary>Creates a one-shot query with the given map function.</summary>
		/// <remarks>
		/// Creates a one-shot query with the given map function. This is equivalent to creating an
		/// anonymous View and then deleting it immediately after querying it. It may be useful during
		/// development, but in general this is inefficient if this map will be used more than once,
		/// because the entire view has to be regenerated from scratch every time.
		/// </remarks>
		[InterfaceAudience.Private]
		public virtual Query SlowQuery(Mapper map)
		{
			return new Query(this, map);
		}

		[InterfaceAudience.Private]
		internal virtual RevisionInternal GetParentRevision(RevisionInternal rev)
		{
			// First get the parent's sequence:
			long seq = rev.GetSequence();
			if (seq > 0)
			{
				seq = LongForQuery("SELECT parent FROM revs WHERE sequence=?", new string[] { System.Convert.ToString
					(seq) });
			}
			else
			{
				long docNumericID = GetDocNumericID(rev.GetDocId());
				if (docNumericID <= 0)
				{
					return null;
				}
				string[] args = new string[] { System.Convert.ToString(docNumericID), rev.GetRevId
					() };
				seq = LongForQuery("SELECT parent FROM revs WHERE doc_id=? and revid=?", args);
			}
			if (seq == 0)
			{
				return null;
			}
			// Now get its revID and deletion status:
			RevisionInternal result = null;
			string[] args_1 = new string[] { System.Convert.ToString(seq) };
			string queryString = "SELECT revid, deleted FROM revs WHERE sequence=?";
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery(queryString, args_1);
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

		/// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
		[InterfaceAudience.Private]
		internal virtual long LongForQuery(string sqlQuery, string[] args)
		{
			Cursor cursor = null;
			long result = 0;
			try
			{
				cursor = database.RawQuery(sqlQuery, args);
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
		/// 	</summary>
		/// <remarks>
		/// Purges specific revisions, which deletes them completely from the local database _without_ adding a "tombstone" revision. It's as though they were never there.
		/// This operation is described here: http://wiki.apache.org/couchdb/Purge_Documents
		/// </remarks>
		/// <param name="docsToRevs">A dictionary mapping document IDs to arrays of revision IDs.
		/// 	</param>
		/// <resultOn>success will point to an NSDictionary with the same form as docsToRev, containing the doc/revision IDs that were actually removed.
		/// 	</resultOn>
		[InterfaceAudience.Private]
		public virtual IDictionary<string, object> PurgeRevisions(IDictionary<string, IList
			<string>> docsToRevs)
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			RunInTransaction(new _TransactionTask_3476(this, docsToRevs, result));
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

		private sealed class _TransactionTask_3476 : TransactionTask
		{
			public _TransactionTask_3476(Database _enclosing, IDictionary<string, IList<string
				>> docsToRevs, IDictionary<string, object> result)
			{
				this._enclosing = _enclosing;
				this.docsToRevs = docsToRevs;
				this.result = result;
			}

			public bool Run()
			{
				foreach (string docID in docsToRevs.Keys)
				{
					long docNumericID = this._enclosing.GetDocNumericID(docID);
					if (docNumericID == -1)
					{
						continue;
					}
					IList<string> revsPurged = new AList<string>();
					IList<string> revIDs = (IList<string>)docsToRevs.Get(docID);
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
									string[] args = new string[] { System.Convert.ToString(docNumericID) };
									this._enclosing.database.ExecSQL("DELETE FROM revs WHERE doc_id=?", args);
								}
								catch (SQLException e)
								{
									Log.E(Database.Tag, "Error deleting revisions", e);
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
									string[] args = new string[] { System.Convert.ToString(docNumericID) };
									string queryString = "SELECT revid, sequence, parent FROM revs WHERE doc_id=? ORDER BY sequence DESC";
									cursor = this._enclosing.database.RawQuery(queryString, args);
									if (!cursor.MoveToNext())
									{
										Log.W(Database.Tag, "No results for query: " + queryString);
										return false;
									}
									ICollection<long> seqsToPurge = new HashSet<long>();
									ICollection<long> seqsToKeep = new HashSet<long>();
									ICollection<string> revsToPurge = new HashSet<string>();
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
									Log.I(Database.Tag, string.Format("Purging doc '%s' revs (%s); asked for (%s)", docID
										, revsToPurge, revIDs));
									if (seqsToPurge.Count > 0)
									{
										string seqsToPurgeList = TextUtils.Join(",", seqsToPurge);
										string sql = string.Format("DELETE FROM revs WHERE sequence in (%s)", seqsToPurgeList
											);
										try
										{
											this._enclosing.database.ExecSQL(sql);
										}
										catch (SQLException e)
										{
											Log.E(Database.Tag, "Error deleting revisions via: " + sql, e);
											return false;
										}
									}
									Sharpen.Collections.AddAll(revsPurged, revsToPurge);
								}
								catch (SQLException e)
								{
									Log.E(Database.Tag, "Error getting revisions", e);
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
					result.Put(docID, revsPurged);
				}
				return true;
			}

			private readonly Database _enclosing;

			private readonly IDictionary<string, IList<string>> docsToRevs;

			private readonly IDictionary<string, object> result;
		}

		[InterfaceAudience.Private]
		protected internal virtual bool ReplaceUUIDs()
		{
			string query = "UPDATE INFO SET value='" + Misc.TDCreateUUID() + "' where key = 'privateUUID';";
			try
			{
				database.ExecSQL(query);
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error updating UUIDs", e);
				return false;
			}
			query = "UPDATE INFO SET value='" + Misc.TDCreateUUID() + "' where key = 'publicUUID';";
			try
			{
				database.ExecSQL(query);
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error updating UUIDs", e);
				return false;
			}
			return true;
		}

		[InterfaceAudience.Private]
		public virtual RevisionInternal GetLocalDocument(string docID, string revID)
		{
			// docID already should contain "_local/" prefix
			RevisionInternal result = null;
			Cursor cursor = null;
			try
			{
				string[] args = new string[] { docID };
				cursor = database.RawQuery("SELECT revid, json FROM localdocs WHERE docid=?", args
					);
				if (cursor.MoveToNext())
				{
					string gotRevID = cursor.GetString(0);
					if (revID != null && (!revID.Equals(gotRevID)))
					{
						return null;
					}
					byte[] json = cursor.GetBlob(1);
					IDictionary<string, object> properties = null;
					try
					{
						properties = Manager.GetObjectMapper().ReadValue<IDictionary>(json);
						properties.Put("_id", docID);
						properties.Put("_rev", gotRevID);
						result = new RevisionInternal(docID, gotRevID, false, this);
						result.SetProperties(properties);
					}
					catch (Exception e)
					{
						Log.W(Database.Tag, "Error parsing local doc JSON", e);
						return null;
					}
				}
				return result;
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error getting local document", e);
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

		[InterfaceAudience.Private]
		public virtual long GetStartTime()
		{
			return this.startTime;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual void DeleteLocalDocument(string docID, string revID)
		{
			if (docID == null)
			{
				throw new CouchbaseLiteException(Status.BadRequest);
			}
			if (revID == null)
			{
				// Didn't specify a revision to delete: 404 or a 409, depending
				if (GetLocalDocument(docID, null) != null)
				{
					throw new CouchbaseLiteException(Status.Conflict);
				}
				else
				{
					throw new CouchbaseLiteException(Status.NotFound);
				}
			}
			string[] whereArgs = new string[] { docID, revID };
			try
			{
				int rowsDeleted = database.Delete("localdocs", "docid=? AND revid=?", whereArgs);
				if (rowsDeleted == 0)
				{
					if (GetLocalDocument(docID, null) != null)
					{
						throw new CouchbaseLiteException(Status.Conflict);
					}
					else
					{
						throw new CouchbaseLiteException(Status.NotFound);
					}
				}
			}
			catch (SQLException e)
			{
				throw new CouchbaseLiteException(e, Status.InternalServerError);
			}
		}

		/// <summary>Set the database's name.</summary>
		/// <remarks>Set the database's name.</remarks>
		[InterfaceAudience.Private]
		public virtual void SetName(string name)
		{
			this.name = name;
		}
	}
}
