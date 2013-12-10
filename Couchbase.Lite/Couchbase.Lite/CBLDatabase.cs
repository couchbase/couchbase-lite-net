/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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
using Couchbase;
using Couchbase.Replicator;
using Couchbase.Storage;
using Couchbase.Support;
using Couchbase.Util;
using Sharpen;

namespace Couchbase
{
	/// <summary>A CBLite database.</summary>
	/// <remarks>A CBLite database.</remarks>
	public class CBLDatabase : Observable
	{
		private string path;

		private string name;

		private SQLiteStorageEngine database;

		private bool open = false;

		private int transactionLevel = 0;

		public const string Tag = "CBLDatabase";

		private IDictionary<string, CBLView> views;

		private IDictionary<string, CBLFilterBlock> filters;

		private IDictionary<string, CBLValidationBlock> validations;

		private IDictionary<string, CBLBlobStoreWriter> pendingAttachmentsByDigest;

		private IList<CBLReplicator> activeReplicators;

		private CBLBlobStore attachments;

		private CBLManager manager;

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

		static CBLDatabase()
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

		public static Couchbase.CBLDatabase CreateEmptyDBAtPath(string path, CBLManager manager
			)
		{
			if (!FileDirUtils.RemoveItemIfExists(path))
			{
				return null;
			}
			Couchbase.CBLDatabase result = new Couchbase.CBLDatabase(path, manager);
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

		public CBLDatabase(string path, CBLManager manager)
		{
			System.Diagnostics.Debug.Assert((path.StartsWith("/")));
			//path must be absolute
			this.path = path;
			this.name = FileDirUtils.GetDatabaseNameFromPath(path);
			this.manager = manager;
		}

		public override string ToString()
		{
			return this.GetType().FullName + "[" + path + "]";
		}

		public virtual bool Exists()
		{
			return new FilePath(path).Exists();
		}

		/// <summary>Replaces the database with a copy of another database.</summary>
		/// <remarks>
		/// Replaces the database with a copy of another database.
		/// This is primarily used to install a canned database on first launch of an app, in which case you should first check .exists to avoid replacing the database if it exists already. The canned database would have been copied into your app bundle at build time.
		/// </remarks>
		/// <param name="databasePath">Path of the database file that should replace this one.
		/// 	</param>
		/// <param name="attachmentsPath">Path of the associated attachments directory, or nil if there are no attachments.
		/// 	</param>
		/// <returns>true if the database was copied, IOException if an error occurs</returns>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual bool ReplaceWithDatabase(string databasePath, string attachmentsPath
			)
		{
			string dstAttachmentsPath = this.GetAttachmentStorePath();
			FilePath sourceFile = new FilePath(databasePath);
			FilePath destFile = new FilePath(path);
			FileDirUtils.CopyFile(sourceFile, destFile);
			FilePath attachmentsFile = new FilePath(dstAttachmentsPath);
			FileDirUtils.DeleteRecursive(attachmentsFile);
			attachmentsFile.Mkdirs();
			if (attachmentsPath != null)
			{
				FileDirUtils.CopyFolder(new FilePath(attachmentsPath), attachmentsFile);
			}
			ReplaceUUIDs();
			return true;
		}

		public virtual bool ReplaceUUIDs()
		{
			string query = "UPDATE INFO SET value='" + CBLMisc.TDCreateUUID() + "' where key = 'privateUUID';";
			try
			{
				database.ExecSQL(query);
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error updating UUIDs", e);
				return false;
			}
			query = "UPDATE INFO SET value='" + CBLMisc.TDCreateUUID() + "' where key = 'publicUUID';";
			try
			{
				database.ExecSQL(query);
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error updating UUIDs", e);
				return false;
			}
			return true;
		}

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
			// Stuff we need to initialize every time the database opens:
			if (!Initialize("PRAGMA foreign_keys = ON;"))
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error turning on foreign keys");
				return false;
			}
			// Check the user_version number we last stored in the database:
			int dbVersion = database.GetVersion();
			// Incompatible version changes increment the hundreds' place:
			if (dbVersion >= 100)
			{
				Log.W(Couchbase.CBLDatabase.Tag, "CBLDatabase: Database version (" + dbVersion + 
					") is newer than I know how to work with");
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
					 + "INSERT INTO INFO (key, value) VALUES ('privateUUID', '" + CBLMisc.TDCreateUUID
					() + "'); " + "INSERT INTO INFO (key, value) VALUES ('publicUUID',  '" + CBLMisc
					.TDCreateUUID() + "'); " + "PRAGMA user_version = 4";
				if (!Initialize(upgradeSql))
				{
					database.Close();
					return false;
				}
			}
			try
			{
				attachments = new CBLBlobStore(GetAttachmentStorePath());
			}
			catch (ArgumentException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Could not initialize attachment store", e);
				database.Close();
				return false;
			}
			open = true;
			return true;
		}

		public virtual bool Close()
		{
			if (!open)
			{
				return false;
			}
			if (views != null)
			{
				foreach (CBLView view in views.Values)
				{
					view.DatabaseClosing();
				}
			}
			views = null;
			if (activeReplicators != null)
			{
				foreach (CBLReplicator replicator in activeReplicators)
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

		public virtual bool DeleteDatabase()
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

		public virtual string GetPath()
		{
			return path;
		}

		public virtual string GetName()
		{
			return name;
		}

		public virtual void SetName(string name)
		{
			this.name = name;
		}

		// Leave this package protected, so it can only be used
		// CBLView uses this accessor
		internal virtual SQLiteStorageEngine GetDatabase()
		{
			return database;
		}

		public virtual CBLBlobStore GetAttachments()
		{
			return attachments;
		}

		public virtual CBLBlobStoreWriter GetAttachmentWriter()
		{
			return new CBLBlobStoreWriter(GetAttachments());
		}

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
		public virtual bool BeginTransaction()
		{
			try
			{
				database.BeginTransaction();
				++transactionLevel;
			}
			catch (SQLException)
			{
				//Log.v(TAG, "Begin transaction (level " + Integer.toString(transactionLevel) + ")...");
				return false;
			}
			return true;
		}

		/// <summary>Commits or aborts (rolls back) a transaction.</summary>
		/// <remarks>Commits or aborts (rolls back) a transaction.</remarks>
		/// <param name="commit">If true, commits; if false, aborts and rolls back, undoing all changes made since the matching -beginTransaction call, *including* any committed nested transactions.
		/// 	</param>
		public virtual bool EndTransaction(bool commit)
		{
			System.Diagnostics.Debug.Assert((transactionLevel > 0));
			if (commit)
			{
				//Log.v(TAG, "Committing transaction (level " + Integer.toString(transactionLevel) + ")...");
				database.SetTransactionSuccessful();
				database.EndTransaction();
			}
			else
			{
				Log.V(Tag, "CANCEL transaction (level " + Sharpen.Extensions.ToString(transactionLevel
					) + ")...");
				try
				{
					database.EndTransaction();
				}
				catch (SQLException)
				{
					return false;
				}
			}
			--transactionLevel;
			return true;
		}

		/// <summary>Compacts the database storage by removing the bodies and attachments of obsolete revisions.
		/// 	</summary>
		/// <remarks>Compacts the database storage by removing the bodies and attachments of obsolete revisions.
		/// 	</remarks>
		public virtual CBLStatus Compact()
		{
			// Can't delete any rows because that would lose revision tree history.
			// But we can remove the JSON of non-current revisions, which is most of the space.
			try
			{
				Log.V(Couchbase.CBLDatabase.Tag, "Deleting JSON of old revisions...");
				ContentValues args = new ContentValues();
				args.Put("json", (string)null);
				database.Update("revs", args, "current=0", null);
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error compacting", e);
				return new CBLStatus(CBLStatus.InternalServerError);
			}
			Log.V(Couchbase.CBLDatabase.Tag, "Deleting old attachments...");
			CBLStatus result = GarbageCollectAttachments();
			Log.V(Couchbase.CBLDatabase.Tag, "Vacuuming SQLite database...");
			try
			{
				database.ExecSQL("VACUUM");
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error vacuuming database", e);
				return new CBLStatus(CBLStatus.InternalServerError);
			}
			return result;
		}

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

		/// <summary>GETTING DOCUMENTS:</summary>
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting document count", e);
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

		public virtual long GetLastSequence()
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting last sequence", e);
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
				extraJSON = CBLServer.GetObjectMapper().WriteValueAsBytes(dict);
			}
			catch (Exception e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error convert extra JSON to bytes", e);
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
		public virtual IDictionary<string, object> ExtraPropertiesForRevision(CBLRevision
			 rev, EnumSet<CBLDatabase.TDContentOptions> contentOptions)
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
			if (contentOptions.Contains(CBLDatabase.TDContentOptions.TDIncludeLocalSeq))
			{
				localSeq = sequenceNumber;
			}
			IDictionary<string, object> revHistory = null;
			if (contentOptions.Contains(CBLDatabase.TDContentOptions.TDIncludeRevs))
			{
				revHistory = GetRevisionHistoryDict(rev);
			}
			IList<object> revsInfo = null;
			if (contentOptions.Contains(CBLDatabase.TDContentOptions.TDIncludeRevsInfo))
			{
				revsInfo = new AList<object>();
				IList<CBLRevision> revHistoryFull = GetRevisionHistory(rev);
				foreach (CBLRevision historicalRev in revHistoryFull)
				{
					IDictionary<string, object> revHistoryItem = new Dictionary<string, object>();
					string status = "available";
					if (historicalRev.IsDeleted())
					{
						status = "deleted";
					}
					// TODO: Detect missing revisions, set status="missing"
					revHistoryItem.Put("rev", historicalRev.GetRevId());
					revHistoryItem.Put("status", status);
					revsInfo.AddItem(revHistoryItem);
				}
			}
			IList<string> conflicts = null;
			if (contentOptions.Contains(CBLDatabase.TDContentOptions.TDIncludeConflicts))
			{
				CBLRevisionList revs = GetAllRevisionsOfDocumentID(docId, true);
				if (revs.Count > 1)
				{
					conflicts = new AList<string>();
					foreach (CBLRevision historicalRev in revs)
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
		public virtual void ExpandStoredJSONIntoRevisionWithAttachments(byte[] json, CBLRevision
			 rev, EnumSet<CBLDatabase.TDContentOptions> contentOptions)
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

		public virtual IDictionary<string, object> DocumentPropertiesFromJSON(byte[] json
			, string docId, string revId, long sequence, EnumSet<CBLDatabase.TDContentOptions
			> contentOptions)
		{
			CBLRevision rev = new CBLRevision(docId, revId, false, this);
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
				docProperties = CBLServer.GetObjectMapper().ReadValue<IDictionary>(json);
				docProperties.PutAll(extra);
				return docProperties;
			}
			catch (Exception e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error serializing properties to JSON", e);
			}
			return docProperties;
		}

		public virtual CBLRevision GetDocumentWithIDAndRev(string id, string rev, EnumSet
			<CBLDatabase.TDContentOptions> contentOptions)
		{
			CBLRevision result = null;
			string sql;
			Cursor cursor = null;
			try
			{
				cursor = null;
				string cols = "revid, deleted, sequence";
				if (!contentOptions.Contains(CBLDatabase.TDContentOptions.TDNoBody))
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
					result = new CBLRevision(id, rev, deleted, this);
					result.SetSequence(cursor.GetLong(2));
					if (!contentOptions.Equals(EnumSet.Of(CBLDatabase.TDContentOptions.TDNoBody)))
					{
						byte[] json = null;
						if (!contentOptions.Contains(CBLDatabase.TDContentOptions.TDNoBody))
						{
							json = cursor.GetBlob(3);
						}
						ExpandStoredJSONIntoRevisionWithAttachments(json, result, contentOptions);
					}
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting document with id and rev", e);
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

		public virtual bool ExistsDocumentWithIDAndRev(string docId, string revId)
		{
			return GetDocumentWithIDAndRev(docId, revId, EnumSet.Of(CBLDatabase.TDContentOptions
				.TDNoBody)) != null;
		}

		public virtual CBLStatus LoadRevisionBody(CBLRevision rev, EnumSet<CBLDatabase.TDContentOptions
			> contentOptions)
		{
			if (rev.GetBody() != null)
			{
				return new CBLStatus(CBLStatus.Ok);
			}
			System.Diagnostics.Debug.Assert(((rev.GetDocId() != null) && (rev.GetRevId() != null
				)));
			Cursor cursor = null;
			CBLStatus result = new CBLStatus(CBLStatus.NotFound);
			try
			{
				string sql = "SELECT sequence, json FROM revs, docs WHERE revid=? AND docs.docid=? AND revs.doc_id=docs.doc_id LIMIT 1";
				string[] args = new string[] { rev.GetRevId(), rev.GetDocId() };
				cursor = database.RawQuery(sql, args);
				if (cursor.MoveToNext())
				{
					result.SetCode(CBLStatus.Ok);
					rev.SetSequence(cursor.GetLong(0));
					ExpandStoredJSONIntoRevisionWithAttachments(cursor.GetBlob(1), rev, contentOptions
						);
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error loading revision body", e);
				return new CBLStatus(CBLStatus.InternalServerError);
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting doc numeric id", e);
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
		public virtual CBLRevisionList GetAllRevisionsOfDocumentID(string docId, long docNumericID
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
			CBLRevisionList result;
			try
			{
				cursor.MoveToNext();
				result = new CBLRevisionList();
				while (!cursor.IsAfterLast())
				{
					CBLRevision rev = new CBLRevision(docId, cursor.GetString(1), (cursor.GetInt(2) >
						 0), this);
					rev.SetSequence(cursor.GetLong(0));
					result.AddItem(rev);
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting all revisions of document", e);
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

		public virtual CBLRevisionList GetAllRevisionsOfDocumentID(string docId, bool onlyCurrent
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
					return new CBLRevisionList();
				}
				else
				{
					return GetAllRevisionsOfDocumentID(docId, docNumericId, onlyCurrent);
				}
			}
		}

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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting all revisions of document", e);
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

		public virtual string FindCommonAncestorOf(CBLRevision rev, IList<string> revIDs)
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting all revisions of document", e);
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
		public virtual IList<CBLRevision> GetRevisionHistory(CBLRevision rev)
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
					return new AList<CBLRevision>();
				}
			}
			string sql = "SELECT sequence, parent, revid, deleted FROM revs " + "WHERE doc_id=? ORDER BY sequence DESC";
			string[] args = new string[] { System.Convert.ToString(docNumericId) };
			Cursor cursor = null;
			IList<CBLRevision> result;
			try
			{
				cursor = database.RawQuery(sql, args);
				cursor.MoveToNext();
				long lastSequence = 0;
				result = new AList<CBLRevision>();
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
						CBLRevision aRev = new CBLRevision(docId, revId, deleted, this);
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting revision history", e);
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

		public static IDictionary<string, object> MakeRevisionHistoryDict(IList<CBLRevision
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
			foreach (CBLRevision rev in history)
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
				foreach (CBLRevision rev_1 in history)
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
		public virtual IDictionary<string, object> GetRevisionHistoryDict(CBLRevision rev
			)
		{
			return MakeRevisionHistoryDict(GetRevisionHistory(rev));
		}

		public virtual CBLRevisionList ChangesSince(long lastSeq, CBLChangesOptions options
			, CBLFilterBlock filter)
		{
			// http://wiki.apache.org/couchdb/HTTP_database_API#Changes
			if (options == null)
			{
				options = new CBLChangesOptions();
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
			CBLRevisionList changes = null;
			try
			{
				cursor = database.RawQuery(sql, args);
				cursor.MoveToNext();
				changes = new CBLRevisionList();
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
					CBLRevision rev = new CBLRevision(cursor.GetString(2), cursor.GetString(3), (cursor
						.GetInt(4) > 0), this);
					rev.SetSequence(cursor.GetLong(0));
					if (includeDocs)
					{
						ExpandStoredJSONIntoRevisionWithAttachments(cursor.GetBlob(5), rev, options.GetContentOptions
							());
					}
					if ((filter == null) || (filter.Filter(rev)))
					{
						changes.AddItem(rev);
					}
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error looking for changes", e);
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

		/// <summary>Define or clear a named filter function.</summary>
		/// <remarks>
		/// Define or clear a named filter function.
		/// These aren't used directly by CBLDatabase, but they're looked up by CBLRouter when a _changes request has a ?filter parameter.
		/// </remarks>
		public virtual void DefineFilter(string filterName, CBLFilterBlock filter)
		{
			if (filters == null)
			{
				filters = new Dictionary<string, CBLFilterBlock>();
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

		public virtual CBLFilterBlock GetFilterNamed(string filterName)
		{
			CBLFilterBlock result = null;
			if (filters != null)
			{
				result = filters.Get(filterName);
			}
			return result;
		}

		/// <summary>VIEWS:</summary>
		public virtual CBLView RegisterView(CBLView view)
		{
			if (view == null)
			{
				return null;
			}
			if (views == null)
			{
				views = new Dictionary<string, CBLView>();
			}
			views.Put(view.GetName(), view);
			return view;
		}

		public virtual CBLView GetViewNamed(string name)
		{
			CBLView view = null;
			if (views != null)
			{
				view = views.Get(name);
			}
			if (view != null)
			{
				return view;
			}
			return RegisterView(new CBLView(this, name));
		}

		public virtual CBLView GetExistingViewNamed(string name)
		{
			CBLView view = null;
			if (views != null)
			{
				view = views.Get(name);
			}
			if (view != null)
			{
				return view;
			}
			view = new CBLView(this, name);
			if (view.GetViewId() == 0)
			{
				return null;
			}
			return RegisterView(view);
		}

		public virtual IList<CBLView> GetAllViews()
		{
			Cursor cursor = null;
			IList<CBLView> result = null;
			try
			{
				cursor = database.RawQuery("SELECT name FROM views", null);
				cursor.MoveToNext();
				result = new AList<CBLView>();
				while (!cursor.IsAfterLast())
				{
					result.AddItem(GetViewNamed(cursor.GetString(0)));
					cursor.MoveToNext();
				}
			}
			catch (Exception e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting all views", e);
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

		public virtual CBLStatus DeleteViewNamed(string name)
		{
			CBLStatus result = new CBLStatus(CBLStatus.InternalServerError);
			try
			{
				string[] whereArgs = new string[] { name };
				int rowsAffected = database.Delete("views", "name=?", whereArgs);
				if (rowsAffected > 0)
				{
					result.SetCode(CBLStatus.Ok);
				}
				else
				{
					result.SetCode(CBLStatus.NotFound);
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error deleting view", e);
			}
			return result;
		}

		//FIX: This has a lot of code in common with -[CBLView queryWithOptions:status:]. Unify the two!
		public virtual IDictionary<string, object> GetDocsWithIDs(IList<string> docIDs, CBLQueryOptions
			 options)
		{
			if (options == null)
			{
				options = new CBLQueryOptions();
			}
			long updateSeq = 0;
			if (options.IsUpdateSeq())
			{
				updateSeq = GetLastSequence();
			}
			// TODO: needs to be atomic with the following SELECT
			// Generate the SELECT statement, based on the options:
			string additionalCols = string.Empty;
			if (options.IsIncludeDocs())
			{
				additionalCols = ", json, sequence";
			}
			string sql = "SELECT revs.doc_id, docid, revid, deleted" + additionalCols + " FROM revs, docs WHERE";
			if (docIDs != null)
			{
				sql += " docid IN (" + JoinQuoted(docIDs) + ")";
			}
			else
			{
				sql += " deleted=0";
			}
			sql += " AND current=1 AND docs.doc_id = revs.doc_id";
			IList<string> argsList = new AList<string>();
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
				if (inclusiveMin)
				{
					sql += " AND docid >= ?";
				}
				else
				{
					sql += " AND docid > ?";
				}
				argsList.AddItem((string)minKey);
			}
			if (maxKey != null)
			{
				System.Diagnostics.Debug.Assert((maxKey is string));
				if (inclusiveMax)
				{
					sql += " AND docid <= ?";
				}
				else
				{
					sql += " AND docid < ?";
				}
				argsList.AddItem((string)maxKey);
			}
			string order = "ASC";
			if (options.IsDescending())
			{
				order = "DESC";
			}
			sql += " ORDER BY docid " + order + ", revid DESC LIMIT ? OFFSET ?";
			argsList.AddItem(Sharpen.Extensions.ToString(options.GetLimit()));
			argsList.AddItem(Sharpen.Extensions.ToString(options.GetSkip()));
			Cursor cursor = null;
			int totalRows = 0;
			long lastDocID = 0;
			IList<IDictionary<string, object>> rows = null;
			try
			{
				cursor = database.RawQuery(sql, Sharpen.Collections.ToArray(argsList, new string[
					argsList.Count]));
				cursor.MoveToNext();
				rows = new AList<IDictionary<string, object>>();
				while (!cursor.IsAfterLast())
				{
					totalRows++;
					long docNumericID = cursor.GetLong(0);
					if (docNumericID == lastDocID)
					{
						cursor.MoveToNext();
						continue;
					}
					lastDocID = docNumericID;
					string docId = cursor.GetString(1);
					string revId = cursor.GetString(2);
					IDictionary<string, object> docContents = null;
					bool deleted = cursor.GetInt(3) > 0;
					if (options.IsIncludeDocs() && !deleted)
					{
						byte[] json = cursor.GetBlob(4);
						long sequence = cursor.GetLong(5);
						docContents = DocumentPropertiesFromJSON(json, docId, revId, sequence, options.GetContentOptions
							());
					}
					IDictionary<string, object> valueMap = new Dictionary<string, object>();
					valueMap.Put("rev", revId);
					IDictionary<string, object> change = new Dictionary<string, object>();
					change.Put("id", docId);
					change.Put("key", docId);
					change.Put("value", valueMap);
					if (docContents != null)
					{
						change.Put("doc", docContents);
					}
					if (deleted)
					{
						change.Put("deleted", true);
					}
					rows.AddItem(change);
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting all docs", e);
				return null;
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("rows", rows);
			result.Put("total_rows", totalRows);
			result.Put("offset", options.GetSkip());
			if (updateSeq != 0)
			{
				result.Put("update_seq", updateSeq);
			}
			return result;
		}

		public virtual IDictionary<string, object> GetAllDocs(CBLQueryOptions options)
		{
			return GetDocsWithIDs(null, options);
		}

		public virtual CBLStatus InsertAttachmentForSequenceWithNameAndType(InputStream contentStream
			, long sequence, string name, string contentType, int revpos)
		{
			System.Diagnostics.Debug.Assert((sequence > 0));
			System.Diagnostics.Debug.Assert((name != null));
			CBLBlobKey key = new CBLBlobKey();
			if (!attachments.StoreBlobStream(contentStream, key))
			{
				return new CBLStatus(CBLStatus.InternalServerError);
			}
			return InsertAttachmentForSequenceWithNameAndType(sequence, name, contentType, revpos
				, key);
		}

		public virtual CBLStatus InsertAttachmentForSequenceWithNameAndType(long sequence
			, string name, string contentType, int revpos, CBLBlobKey key)
		{
			try
			{
				ContentValues args = new ContentValues();
				args.Put("sequence", sequence);
				args.Put("filename", name);
				args.Put("key", key.GetBytes());
				args.Put("type", contentType);
				args.Put("length", attachments.GetSizeOfBlob(key));
				args.Put("revpos", revpos);
				database.Insert("attachments", null, args);
				return new CBLStatus(CBLStatus.Created);
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error inserting attachment", e);
				return new CBLStatus(CBLStatus.InternalServerError);
			}
		}

		/// <summary>Move pending (temporary) attachments into their permanent location.</summary>
		/// <remarks>Move pending (temporary) attachments into their permanent location.</remarks>
		public virtual CBLStatus InstallPendingAttachment(IDictionary<string, object> attachment
			)
		{
			string digest = (string)attachment.Get("digest");
			if (digest == null)
			{
				return new CBLStatus(CBLStatus.BadAttachment);
			}
			if (pendingAttachmentsByDigest != null && pendingAttachmentsByDigest.ContainsKey(
				digest))
			{
				object writer = pendingAttachmentsByDigest.Get(digest);
				if (writer is CBLBlobStoreWriter)
				{
					try
					{
						((CBLBlobStoreWriter)writer).Install();
						// this is a temporary hack.  rather than doing what it does in the ios putRevision()
						// method, and creating CBLAttachment objects and passing it down into this method,
						// just set the digest in this map to be sha1 (the one we want).
						attachment.Put("digest", ((CBLBlobStoreWriter)writer).SHA1DigestString());
					}
					catch (Exception e)
					{
						string msg = string.Format("Unable to install pending attachment: %s", digest);
						Log.E(Couchbase.CBLDatabase.Tag, msg, e);
						return new CBLStatus(CBLStatus.StatusAttachmentError);
					}
					return new CBLStatus(CBLStatus.Ok);
				}
				else
				{
					// TODO: deal with case where its a byte[] rather than a blob store writer, see ios
					return new CBLStatus(CBLStatus.BadAttachment);
				}
			}
			return new CBLStatus(CBLStatus.Ok);
		}

		public virtual CBLStatus CopyAttachmentNamedFromSequenceToSequence(string name, long
			 fromSeq, long toSeq)
		{
			System.Diagnostics.Debug.Assert((name != null));
			System.Diagnostics.Debug.Assert((toSeq > 0));
			if (fromSeq < 0)
			{
				return new CBLStatus(CBLStatus.NotFound);
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
					Log.W(Couchbase.CBLDatabase.Tag, "Can't find inherited attachment " + name + " from seq# "
						 + System.Convert.ToString(fromSeq) + " to copy to " + System.Convert.ToString(toSeq
						));
					return new CBLStatus(CBLStatus.NotFound);
				}
				else
				{
					return new CBLStatus(CBLStatus.Ok);
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error copying attachment", e);
				return new CBLStatus(CBLStatus.InternalServerError);
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
		public virtual CBLAttachment GetAttachmentForSequence(long sequence, string filename
			, CBLStatus status)
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
					status.SetCode(CBLStatus.NotFound);
					return null;
				}
				byte[] keyData = cursor.GetBlob(0);
				//TODO add checks on key here? (ios version)
				CBLBlobKey key = new CBLBlobKey(keyData);
				InputStream contentStream = attachments.BlobStreamForKey(key);
				if (contentStream == null)
				{
					Log.E(Couchbase.CBLDatabase.Tag, "Failed to load attachment");
					status.SetCode(CBLStatus.InternalServerError);
					return null;
				}
				else
				{
					status.SetCode(CBLStatus.Ok);
					CBLAttachment result = new CBLAttachment();
					result.SetContentStream(contentStream);
					result.SetContentType(cursor.GetString(1));
					result.SetGZipped(attachments.IsGZipped(key));
					return result;
				}
			}
			catch (SQLException)
			{
				status.SetCode(CBLStatus.InternalServerError);
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

		/// <summary>Constructs an "_attachments" dictionary for a revision, to be inserted in its JSON body.
		/// 	</summary>
		/// <remarks>Constructs an "_attachments" dictionary for a revision, to be inserted in its JSON body.
		/// 	</remarks>
		public virtual IDictionary<string, object> GetAttachmentsDictForSequenceWithContent
			(long sequence, EnumSet<CBLDatabase.TDContentOptions> contentOptions)
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
					CBLBlobKey key = new CBLBlobKey(keyData);
					string digestString = "sha1-" + Base64.EncodeBytes(keyData);
					string dataBase64 = null;
					if (contentOptions.Contains(CBLDatabase.TDContentOptions.TDIncludeAttachments))
					{
						if (contentOptions.Contains(CBLDatabase.TDContentOptions.TDBigAttachmentsFollow) 
							&& length >= Couchbase.CBLDatabase.kBigAttachmentLength)
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
								Log.W(Couchbase.CBLDatabase.Tag, "Error loading attachment");
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting attachments for sequence", e);
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

		/// <summary>Modifies a CBLRevision's body by changing all attachments with revpos &lt; minRevPos into stubs.
		/// 	</summary>
		/// <remarks>Modifies a CBLRevision's body by changing all attachments with revpos &lt; minRevPos into stubs.
		/// 	</remarks>
		/// <param name="rev"></param>
		/// <param name="minRevPos"></param>
		public virtual void StubOutAttachmentsIn(CBLRevision rev, int minRevPos)
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
					Log.D(Couchbase.CBLDatabase.Tag, "Stubbed out attachment" + rev + " " + name + ": revpos"
						 + revPos + " " + minRevPos);
				}
			}
			if (editedProperties != null)
			{
				rev.SetProperties(editedProperties);
			}
		}

		/// <summary>Given a newly-added revision, adds the necessary attachment rows to the database and stores inline attachments into the blob store.
		/// 	</summary>
		/// <remarks>Given a newly-added revision, adds the necessary attachment rows to the database and stores inline attachments into the blob store.
		/// 	</remarks>
		public virtual CBLStatus ProcessAttachmentsForRevision(CBLRevision rev, long parentSequence
			)
		{
			System.Diagnostics.Debug.Assert((rev != null));
			long newSequence = rev.GetSequence();
			System.Diagnostics.Debug.Assert((newSequence > parentSequence));
			// If there are no attachments in the new rev, there's nothing to do:
			IDictionary<string, object> newAttachments = null;
			IDictionary<string, object> properties = (IDictionary<string, object>)rev.GetProperties
				();
			if (properties != null)
			{
				newAttachments = (IDictionary<string, object>)properties.Get("_attachments");
			}
			if (newAttachments == null || newAttachments.Count == 0 || rev.IsDeleted())
			{
				return new CBLStatus(CBLStatus.Ok);
			}
			foreach (string name in newAttachments.Keys)
			{
				CBLStatus status = new CBLStatus();
				IDictionary<string, object> newAttach = (IDictionary<string, object>)newAttachments
					.Get(name);
				string newContentBase64 = (string)newAttach.Get("data");
				if (newContentBase64 != null)
				{
					// New item contains data, so insert it. First decode the data:
					byte[] newContents;
					try
					{
						newContents = Base64.Decode(newContentBase64);
					}
					catch (IOException e)
					{
						Log.E(Couchbase.CBLDatabase.Tag, "IOExeption parsing base64", e);
						return new CBLStatus(CBLStatus.BadRequest);
					}
					if (newContents == null)
					{
						return new CBLStatus(CBLStatus.BadRequest);
					}
					// Now determine the revpos, i.e. generation # this was added in. Usually this is
					// implicit, but a rev being pulled in replication will have it set already.
					int generation = rev.GetGeneration();
					System.Diagnostics.Debug.Assert((generation > 0));
					object revposObj = newAttach.Get("revpos");
					int revpos = generation;
					if (revposObj != null && revposObj is int)
					{
						revpos = ((int)revposObj);
					}
					if (revpos > generation)
					{
						return new CBLStatus(CBLStatus.BadRequest);
					}
					// Finally insert the attachment:
					// workaround for issue #80 - it was looking at the "content_type" field instead of "content-type".
					// fix is backwards compatible in case any code is using content_type.
					string contentType = null;
					if (newAttach.ContainsKey("content_type"))
					{
						contentType = (string)newAttach.Get("content_type");
						Log.W(Tag, "Found attachment that uses content_type field name instead of content-type: "
							 + newAttach);
					}
					else
					{
						if (newAttach.ContainsKey("content-type"))
						{
							contentType = (string)newAttach.Get("content-type");
						}
					}
					status = InsertAttachmentForSequenceWithNameAndType(new ByteArrayInputStream(newContents
						), newSequence, name, contentType, revpos);
				}
				else
				{
					if (newAttach.ContainsKey("follows") && ((bool)newAttach.Get("follows")) == true)
					{
						// Now determine the revpos, i.e. generation # this was added in. Usually this is
						// implicit, but a rev being pulled in replication will have it set already.
						int generation = rev.GetGeneration();
						System.Diagnostics.Debug.Assert((generation > 0));
						object revposObj = newAttach.Get("revpos");
						int revpos = generation;
						if (revposObj != null && revposObj is int)
						{
							revpos = ((int)revposObj);
						}
						if (revpos > generation)
						{
							return new CBLStatus(CBLStatus.BadRequest);
						}
						// Finally insert the attachment:
						Encoding utf8 = Sharpen.Extensions.GetEncoding("UTF-8");
						string sha1DigestKey = (string)newAttach.Get("digest");
						CBLBlobKey key = new CBLBlobKey(sha1DigestKey);
						status = InsertAttachmentForSequenceWithNameAndType(newSequence, name, (string)newAttach
							.Get("content_type"), revpos, key);
					}
					else
					{
						// It's just a stub, so copy the previous revision's attachment entry:
						//? Should I enforce that the type and digest (if any) match?
						status = CopyAttachmentNamedFromSequenceToSequence(name, parentSequence, newSequence
							);
					}
				}
				if (!status.IsSuccessful())
				{
					return status;
				}
			}
			return new CBLStatus(CBLStatus.Ok);
		}

		/// <summary>Updates or deletes an attachment, creating a new document revision in the process.
		/// 	</summary>
		/// <remarks>
		/// Updates or deletes an attachment, creating a new document revision in the process.
		/// Used by the PUT / DELETE methods called on attachment URLs.
		/// </remarks>
		public virtual CBLRevision UpdateAttachment(string filename, InputStream contentStream
			, string contentType, string docID, string oldRevID, CBLStatus status)
		{
			status.SetCode(CBLStatus.BadRequest);
			if (filename == null || filename.Length == 0 || (contentStream != null && contentType
				 == null) || (oldRevID != null && docID == null) || (contentStream != null && docID
				 == null))
			{
				return null;
			}
			BeginTransaction();
			try
			{
				CBLRevision oldRev = new CBLRevision(docID, oldRevID, false, this);
				if (oldRevID != null)
				{
					// Load existing revision if this is a replacement:
					CBLStatus loadStatus = LoadRevisionBody(oldRev, EnumSet.NoneOf<CBLDatabase.TDContentOptions
						>());
					status.SetCode(loadStatus.GetCode());
					if (!status.IsSuccessful())
					{
						if (status.GetCode() == CBLStatus.NotFound && ExistsDocumentWithIDAndRev(docID, null
							))
						{
							status.SetCode(CBLStatus.Conflict);
						}
						// if some other revision exists, it's a conflict
						return null;
					}
					IDictionary<string, object> attachments = (IDictionary<string, object>)oldRev.GetProperties
						().Get("_attachments");
					if (contentStream == null && attachments != null && !attachments.ContainsKey(filename
						))
					{
						status.SetCode(CBLStatus.NotFound);
						return null;
					}
					// Remove the _attachments stubs so putRevision: doesn't copy the rows for me
					// OPT: Would be better if I could tell loadRevisionBody: not to add it
					if (attachments != null)
					{
						IDictionary<string, object> properties = new Dictionary<string, object>(oldRev.GetProperties
							());
						Sharpen.Collections.Remove(properties, "_attachments");
						oldRev.SetBody(new CBLBody(properties));
					}
				}
				else
				{
					// If this creates a new doc, it needs a body:
					oldRev.SetBody(new CBLBody(new Dictionary<string, object>()));
				}
				// Create a new revision:
				CBLRevision newRev = PutRevision(oldRev, oldRevID, false, status);
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
					CBLStatus insertStatus = InsertAttachmentForSequenceWithNameAndType(contentStream
						, newRev.GetSequence(), filename, contentType, newRev.GetGeneration());
					status.SetCode(insertStatus.GetCode());
					if (!status.IsSuccessful())
					{
						return null;
					}
				}
				status.SetCode((contentStream != null) ? CBLStatus.Created : CBLStatus.Ok);
				return newRev;
			}
			catch (SQLException e)
			{
				Log.E(Tag, "Error uploading attachment", e);
				status.SetCode(CBLStatus.InternalServerError);
				return null;
			}
			finally
			{
				EndTransaction(status.IsSuccessful());
			}
		}

		public virtual void RememberAttachmentWritersForDigests(IDictionary<string, CBLBlobStoreWriter
			> blobsByDigest)
		{
			if (pendingAttachmentsByDigest == null)
			{
				pendingAttachmentsByDigest = new Dictionary<string, CBLBlobStoreWriter>();
			}
			pendingAttachmentsByDigest.PutAll(blobsByDigest);
		}

		/// <summary>Deletes obsolete attachments from the database and blob store.</summary>
		/// <remarks>Deletes obsolete attachments from the database and blob store.</remarks>
		public virtual CBLStatus GarbageCollectAttachments()
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error deleting attachments", e);
			}
			// Now collect all remaining attachment IDs and tell the store to delete all but these:
			Cursor cursor = null;
			try
			{
				cursor = database.RawQuery("SELECT DISTINCT key FROM attachments", null);
				cursor.MoveToNext();
				IList<CBLBlobKey> allKeys = new AList<CBLBlobKey>();
				while (!cursor.IsAfterLast())
				{
					CBLBlobKey key = new CBLBlobKey(cursor.GetBlob(0));
					allKeys.AddItem(key);
					cursor.MoveToNext();
				}
				int numDeleted = attachments.DeleteBlobsExceptWithKeys(allKeys);
				if (numDeleted < 0)
				{
					return new CBLStatus(CBLStatus.InternalServerError);
				}
				Log.V(Couchbase.CBLDatabase.Tag, "Deleted " + numDeleted + " attachments");
				return new CBLStatus(CBLStatus.Ok);
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error finding attachment keys in use", e);
				return new CBLStatus(CBLStatus.InternalServerError);
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
		public static string GenerateDocumentId()
		{
			return CBLMisc.TDCreateUUID();
		}

		public virtual string GenerateNextRevisionID(string revisionId)
		{
			// Revision IDs have a generation count, a hyphen, and a UUID.
			int generation = 0;
			if (revisionId != null)
			{
				generation = CBLRevision.GenerationFromRevID(revisionId);
				if (generation == 0)
				{
					return null;
				}
			}
			string digest = CBLMisc.TDCreateUUID();
			//TODO: Generate canonical digest of body
			return Sharpen.Extensions.ToString(generation + 1) + "-" + digest;
		}

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
				Log.E(Couchbase.CBLDatabase.Tag, "Error inserting document id", e);
			}
			return rowId;
		}

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
		public virtual byte[] EncodeDocumentJSON(CBLRevision rev)
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
						Log.E(Tag, "CBLDatabase: Invalid top-level key '" + key + "' in document to be inserted"
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
				json = CBLServer.GetObjectMapper().WriteValueAsBytes(properties);
			}
			catch (Exception e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error serializing " + rev + " to JSON", e);
			}
			return json;
		}

		public virtual void NotifyChange(CBLRevision rev, Uri source)
		{
			IDictionary<string, object> changeNotification = new Dictionary<string, object>();
			changeNotification.Put("rev", rev);
			changeNotification.Put("seq", rev.GetSequence());
			if (source != null)
			{
				changeNotification.Put("source", source);
			}
			SetChanged();
			NotifyObservers(changeNotification);
		}

		public virtual long InsertRevision(CBLRevision rev, long docNumericID, long parentSequence
			, bool current, byte[] data)
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error inserting revision", e);
			}
			return rowId;
		}

		private CBLRevision PutRevision(CBLRevision rev, string prevRevId, CBLStatus resultStatus
			)
		{
			return PutRevision(rev, prevRevId, false, resultStatus);
		}

		/// <summary>Stores a new (or initial) revision of a document.</summary>
		/// <remarks>
		/// Stores a new (or initial) revision of a document.
		/// This is what's invoked by a PUT or POST. As with those, the previous revision ID must be supplied when necessary and the call will fail if it doesn't match.
		/// </remarks>
		/// <param name="rev">The revision to add. If the docID is null, a new UUID will be assigned. Its revID must be null. It must have a JSON body.
		/// 	</param>
		/// <param name="prevRevId">The ID of the revision to replace (same as the "?rev=" parameter to a PUT), or null if this is a new document.
		/// 	</param>
		/// <param name="allowConflict">If false, an error status 409 will be returned if the insertion would create a conflict, i.e. if the previous revision already has a child.
		/// 	</param>
		/// <param name="resultStatus">On return, an HTTP status code indicating success or failure.
		/// 	</param>
		/// <returns>A new CBLRevision with the docID, revID and sequence filled in (but no body).
		/// 	</returns>
		public virtual CBLRevision PutRevision(CBLRevision rev, string prevRevId, bool allowConflict
			, CBLStatus resultStatus)
		{
			// prevRevId is the rev ID being replaced, or nil if an insert
			string docId = rev.GetDocId();
			bool deleted = rev.IsDeleted();
			if ((rev == null) || ((prevRevId != null) && (docId == null)) || (deleted && (docId
				 == null)) || ((docId != null) && !IsValidDocumentId(docId)))
			{
				resultStatus.SetCode(CBLStatus.BadRequest);
				return null;
			}
			resultStatus.SetCode(CBLStatus.InternalServerError);
			BeginTransaction();
			Cursor cursor = null;
			//// PART I: In which are performed lookups and validations prior to the insert...
			long docNumericID = (docId != null) ? GetDocNumericID(docId) : 0;
			long parentSequence = 0;
			try
			{
				if (prevRevId != null)
				{
					// Replacing: make sure given prevRevID is current & find its sequence number:
					if (docNumericID <= 0)
					{
						resultStatus.SetCode(CBLStatus.NotFound);
						return null;
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
							resultStatus.SetCode(CBLStatus.Conflict);
							return null;
						}
						else
						{
							resultStatus.SetCode(CBLStatus.NotFound);
							return null;
						}
					}
					if (validations != null && validations.Count > 0)
					{
						// Fetch the previous revision and validate the new one against it:
						CBLRevision prevRev = new CBLRevision(docId, prevRevId, false, this);
						CBLStatus status = ValidateRevision(rev, prevRev);
						if (!status.IsSuccessful())
						{
							resultStatus.SetCode(status.GetCode());
							return null;
						}
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
							resultStatus.SetCode(CBLStatus.Conflict);
							return null;
						}
						else
						{
							resultStatus.SetCode(CBLStatus.NotFound);
							return null;
						}
					}
					// Validate:
					CBLStatus status = ValidateRevision(rev, null);
					if (!status.IsSuccessful())
					{
						resultStatus.SetCode(status.GetCode());
						return null;
					}
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
							// Doc exists; check whether current winning revision is deleted:
							string[] args = new string[] { System.Convert.ToString(docNumericID) };
							cursor = database.RawQuery("SELECT sequence, deleted FROM revs WHERE doc_id=? and current=1 ORDER BY revid DESC LIMIT 1"
								, args);
							if (cursor.MoveToNext())
							{
								bool wasAlreadyDeleted = (cursor.GetInt(1) > 0);
								if (wasAlreadyDeleted)
								{
									// Make the deleted revision no longer current:
									ContentValues updateContent = new ContentValues();
									updateContent.Put("current", 0);
									database.Update("revs", updateContent, "sequence=" + cursor.GetLong(0), null);
								}
								else
								{
									if (!allowConflict)
									{
										// docId already exists, current not deleted, conflict
										resultStatus.SetCode(CBLStatus.Conflict);
										return null;
									}
								}
							}
						}
					}
					else
					{
						// Inserting first revision, with no docID given (POST): generate a unique docID:
						docId = Couchbase.CBLDatabase.GenerateDocumentId();
						docNumericID = InsertDocumentID(docId);
						if (docNumericID <= 0)
						{
							return null;
						}
					}
				}
				//// PART II: In which insertion occurs...
				// Bump the revID and update the JSON:
				string newRevId = GenerateNextRevisionID(prevRevId);
				byte[] data = null;
				if (!rev.IsDeleted())
				{
					data = EncodeDocumentJSON(rev);
					if (data == null)
					{
						// bad or missing json
						resultStatus.SetCode(CBLStatus.BadRequest);
						return null;
					}
				}
				rev = rev.CopyWithDocID(docId, newRevId);
				// Now insert the rev itself:
				long newSequence = InsertRevision(rev, docNumericID, parentSequence, true, data);
				if (newSequence == 0)
				{
					return null;
				}
				// Store any attachments:
				if (attachments != null)
				{
					CBLStatus status = ProcessAttachmentsForRevision(rev, parentSequence);
					if (!status.IsSuccessful())
					{
						resultStatus.SetCode(status.GetCode());
						return null;
					}
				}
				// Success!
				if (deleted)
				{
					resultStatus.SetCode(CBLStatus.Ok);
				}
				else
				{
					resultStatus.SetCode(CBLStatus.Created);
				}
			}
			catch (SQLException e1)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error putting revision", e1);
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
			NotifyChange(rev, null);
			return rev;
		}

		/// <summary>Inserts an already-existing revision replicated from a remote database.</summary>
		/// <remarks>
		/// Inserts an already-existing revision replicated from a remote database.
		/// It must already have a revision ID. This may create a conflict! The revision's history must be given; ancestor revision IDs that don't already exist locally will create phantom revisions with no content.
		/// </remarks>
		public virtual CBLStatus ForceInsert(CBLRevision rev, IList<string> revHistory, Uri
			 source)
		{
			string docId = rev.GetDocId();
			string revId = rev.GetRevId();
			if (!IsValidDocumentId(docId) || (revId == null))
			{
				return new CBLStatus(CBLStatus.BadRequest);
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
					return new CBLStatus(CBLStatus.BadRequest);
				}
			}
			bool success = false;
			BeginTransaction();
			try
			{
				// First look up all locally-known revisions of this document:
				long docNumericID = GetOrInsertDocNumericID(docId);
				CBLRevisionList localRevs = GetAllRevisionsOfDocumentID(docId, docNumericID, false
					);
				if (localRevs == null)
				{
					return new CBLStatus(CBLStatus.InternalServerError);
				}
				// Walk through the remote history in chronological order, matching each revision ID to
				// a local revision. When the list diverges, start creating blank local revisions to fill
				// in the local history:
				long sequence = 0;
				long localParentSequence = 0;
				for (int i = revHistory.Count - 1; i >= 0; --i)
				{
					revId = revHistory[i];
					CBLRevision localRev = localRevs.RevWithDocIdAndRevId(docId, revId);
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
						CBLRevision newRev;
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
									return new CBLStatus(CBLStatus.BadRequest);
								}
							}
							current = true;
						}
						else
						{
							// It's an intermediate parent, so insert a stub:
							newRev = new CBLRevision(docId, revId, false, this);
						}
						// Insert it:
						sequence = InsertRevision(newRev, docNumericID, sequence, current, data);
						if (sequence <= 0)
						{
							return new CBLStatus(CBLStatus.InternalServerError);
						}
						if (i == 0)
						{
							// Write any changed attachments for the new revision:
							CBLStatus status = ProcessAttachmentsForRevision(rev, localParentSequence);
							if (!status.IsSuccessful())
							{
								return status;
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
						return new CBLStatus(CBLStatus.InternalServerError);
					}
				}
				success = true;
			}
			catch (SQLException)
			{
				EndTransaction(success);
				return new CBLStatus(CBLStatus.InternalServerError);
			}
			finally
			{
				EndTransaction(success);
			}
			// Notify and return:
			NotifyChange(rev, source);
			return new CBLStatus(CBLStatus.Created);
		}

		/// <summary>Define or clear a named document validation function.</summary>
		/// <remarks>Define or clear a named document validation function.</remarks>
		public virtual void DefineValidation(string name, CBLValidationBlock validationBlock
			)
		{
			if (validations == null)
			{
				validations = new Dictionary<string, CBLValidationBlock>();
			}
			if (validationBlock != null)
			{
				validations.Put(name, validationBlock);
			}
			else
			{
				Sharpen.Collections.Remove(validations, name);
			}
		}

		public virtual CBLValidationBlock GetValidationNamed(string name)
		{
			CBLValidationBlock result = null;
			if (validations != null)
			{
				result = validations.Get(name);
			}
			return result;
		}

		public virtual CBLStatus ValidateRevision(CBLRevision newRev, CBLRevision oldRev)
		{
			CBLStatus result = new CBLStatus(CBLStatus.Ok);
			if (validations == null || validations.Count == 0)
			{
				return result;
			}
			TDValidationContextImpl context = new TDValidationContextImpl(this, oldRev);
			foreach (string validationName in validations.Keys)
			{
				CBLValidationBlock validation = GetValidationNamed(validationName);
				if (!validation.Validate(newRev, context))
				{
					result.SetCode(context.GetErrorType().GetCode());
					break;
				}
			}
			return result;
		}

		public virtual IList<CBLReplicator> GetActiveReplicators()
		{
			//TODO implement missing replication methods
			return activeReplicators;
		}

		public virtual CBLReplicator GetActiveReplicator(Uri remote, bool push)
		{
			if (activeReplicators != null)
			{
				foreach (CBLReplicator replicator in activeReplicators)
				{
					if (replicator.GetRemote().Equals(remote) && replicator.IsPush() == push && replicator
						.IsRunning())
					{
						return replicator;
					}
				}
			}
			return null;
		}

		public virtual CBLReplicator GetReplicator(Uri remote, bool push, bool continuous
			, ScheduledExecutorService workExecutor)
		{
			CBLReplicator replicator = GetReplicator(remote, null, push, continuous, workExecutor
				);
			return replicator;
		}

		public virtual CBLReplicator GetReplicator(string sessionId)
		{
			if (activeReplicators != null)
			{
				foreach (CBLReplicator replicator in activeReplicators)
				{
					if (replicator.GetSessionID().Equals(sessionId))
					{
						return replicator;
					}
				}
			}
			return null;
		}

		public virtual CBLReplicator GetReplicator(Uri remote, HttpClientFactory httpClientFactory
			, bool push, bool continuous, ScheduledExecutorService workExecutor)
		{
			CBLReplicator result = GetActiveReplicator(remote, push);
			if (result != null)
			{
				return result;
			}
			result = push ? new CBLPusher(this, remote, continuous, httpClientFactory, workExecutor
				) : new CBLPuller(this, remote, continuous, httpClientFactory, workExecutor);
			if (activeReplicators == null)
			{
				activeReplicators = new AList<CBLReplicator>();
			}
			activeReplicators.AddItem(result);
			return result;
		}

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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting last sequence", e);
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

		public static string Quote(string @string)
		{
			return @string.Replace("'", "''");
		}

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

		public virtual bool FindMissingRevisions(CBLRevisionList touchRevs)
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
					CBLRevision rev = touchRevs.RevWithDocIdAndRevId(cursor.GetString(0), cursor.GetString
						(1));
					if (rev != null)
					{
						touchRevs.Remove(rev);
					}
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Couchbase.CBLDatabase.Tag, "Error finding missing revisions", e);
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

		public virtual CBLRevision GetLocalDocument(string docID, string revID)
		{
			CBLRevision result = null;
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
						properties = CBLServer.GetObjectMapper().ReadValue<IDictionary>(json);
						properties.Put("_id", docID);
						properties.Put("_rev", gotRevID);
						result = new CBLRevision(docID, gotRevID, false, this);
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
				Log.E(Couchbase.CBLDatabase.Tag, "Error getting local document", e);
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

		public virtual CBLRevision PutLocalRevision(CBLRevision revision, string prevRevID
			, CBLStatus status)
		{
			string docID = revision.GetDocId();
			if (!docID.StartsWith("_local/"))
			{
				status.SetCode(CBLStatus.BadRequest);
				return null;
			}
			if (!revision.IsDeleted())
			{
				// PUT:
				byte[] json = EncodeDocumentJSON(revision);
				string newRevID;
				if (prevRevID != null)
				{
					int generation = CBLRevision.GenerationFromRevID(prevRevID);
					if (generation == 0)
					{
						status.SetCode(CBLStatus.BadRequest);
						return null;
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
							status.SetCode(CBLStatus.Conflict);
							return null;
						}
					}
					catch (SQLException)
					{
						status.SetCode(CBLStatus.InternalServerError);
						return null;
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
					catch (SQLException)
					{
						status.SetCode(CBLStatus.InternalServerError);
						return null;
					}
				}
				status.SetCode(CBLStatus.Created);
				return revision.CopyWithDocID(docID, newRevID);
			}
			else
			{
				// DELETE:
				CBLStatus deleteStatus = DeleteLocalDocument(docID, prevRevID);
				status.SetCode(deleteStatus.GetCode());
				return (status.IsSuccessful()) ? revision : null;
			}
		}

		public virtual CBLStatus DeleteLocalDocument(string docID, string revID)
		{
			if (docID == null)
			{
				return new CBLStatus(CBLStatus.BadRequest);
			}
			if (revID == null)
			{
				// Didn't specify a revision to delete: 404 or a 409, depending
				return (GetLocalDocument(docID, null) != null) ? new CBLStatus(CBLStatus.Conflict
					) : new CBLStatus(CBLStatus.NotFound);
			}
			string[] whereArgs = new string[] { docID, revID };
			try
			{
				int rowsDeleted = database.Delete("localdocs", "docid=? AND revid=?", whereArgs);
				if (rowsDeleted == 0)
				{
					return (GetLocalDocument(docID, null) != null) ? new CBLStatus(CBLStatus.Conflict
						) : new CBLStatus(CBLStatus.NotFound);
				}
				return new CBLStatus(CBLStatus.Ok);
			}
			catch (SQLException)
			{
				return new CBLStatus(CBLStatus.InternalServerError);
			}
		}
	}

	internal class TDValidationContextImpl : CBLValidationContext
	{
		private CBLDatabase database;

		private CBLRevision currentRevision;

		private CBLStatus errorType;

		private string errorMessage;

		public TDValidationContextImpl(CBLDatabase database, CBLRevision currentRevision)
		{
			this.database = database;
			this.currentRevision = currentRevision;
			this.errorType = new CBLStatus(CBLStatus.Forbidden);
			this.errorMessage = "invalid document";
		}

		public virtual CBLRevision GetCurrentRevision()
		{
			if (currentRevision != null)
			{
				database.LoadRevisionBody(currentRevision, EnumSet.NoneOf<CBLDatabase.TDContentOptions
					>());
			}
			return currentRevision;
		}

		public virtual CBLStatus GetErrorType()
		{
			return errorType;
		}

		public virtual void SetErrorType(CBLStatus status)
		{
			this.errorType = status;
		}

		public virtual string GetErrorMessage()
		{
			return errorMessage;
		}

		public virtual void SetErrorMessage(string message)
		{
			this.errorMessage = message;
		}
	}
}
