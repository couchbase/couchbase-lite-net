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
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Represents a view available in a database.</summary>
	/// <remarks>Represents a view available in a database.</remarks>
	public class View
	{
		public const int ReduceBatchSize = 100;

		public enum TDViewCollation
		{
			TDViewCollationUnicode,
			TDViewCollationRaw,
			TDViewCollationASCII
		}

		private Database database;

		private string name;

		private int viewId;

		private Mapper mapBlock;

		private Reducer reduceBlock;

		private View.TDViewCollation collation;

		private static ViewCompiler compiler;

		/// <summary>The registered object, if any, that can compile map/reduce functions from source code.
		/// 	</summary>
		/// <remarks>The registered object, if any, that can compile map/reduce functions from source code.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public static ViewCompiler GetCompiler()
		{
			return compiler;
		}

		/// <summary>Registers an object that can compile map/reduce functions from source code.
		/// 	</summary>
		/// <remarks>Registers an object that can compile map/reduce functions from source code.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public static void SetCompiler(ViewCompiler compiler)
		{
			Couchbase.Lite.View.compiler = compiler;
		}

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal View(Database database, string name)
		{
			this.database = database;
			this.name = name;
			this.viewId = -1;
			// means 'unknown'
			this.collation = View.TDViewCollation.TDViewCollationUnicode;
		}

		/// <summary>Get the database that owns this view.</summary>
		/// <remarks>Get the database that owns this view.</remarks>
		[InterfaceAudience.Public]
		public virtual Database GetDatabase()
		{
			return database;
		}

		/// <summary>Get the name of the view.</summary>
		/// <remarks>Get the name of the view.</remarks>
		[InterfaceAudience.Public]
		public virtual string GetName()
		{
			return name;
		}

		/// <summary>The map function that controls how index rows are created from documents.
		/// 	</summary>
		/// <remarks>The map function that controls how index rows are created from documents.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual Mapper GetMap()
		{
			return mapBlock;
		}

		/// <summary>The optional reduce function, which aggregates together multiple rows.</summary>
		/// <remarks>The optional reduce function, which aggregates together multiple rows.</remarks>
		[InterfaceAudience.Public]
		public virtual Reducer GetReduce()
		{
			return reduceBlock;
		}

		/// <summary>Is the view's index currently out of date?</summary>
		[InterfaceAudience.Public]
		public virtual bool IsStale()
		{
			return (GetLastSequenceIndexed() < database.GetLastSequenceNumber());
		}

		/// <summary>Get the last sequence number indexed so far.</summary>
		/// <remarks>Get the last sequence number indexed so far.</remarks>
		[InterfaceAudience.Public]
		public virtual long GetLastSequenceIndexed()
		{
			string sql = "SELECT lastSequence FROM views WHERE name=?";
			string[] args = new string[] { name };
			Cursor cursor = null;
			long result = -1;
			try
			{
				Log.D(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " start running query: "
					 + sql);
				cursor = database.GetDatabase().RawQuery(sql, args);
				Log.D(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " finish running query: "
					 + sql);
				if (cursor.MoveToNext())
				{
					result = cursor.GetLong(0);
				}
			}
			catch (Exception)
			{
				Log.E(Database.Tag, "Error getting last sequence indexed");
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

		/// <summary>Defines a view that has no reduce function.</summary>
		/// <remarks>
		/// Defines a view that has no reduce function.
		/// See setMapAndReduce() for more information.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual bool SetMap(Mapper mapBlock, string version)
		{
			return SetMapAndReduce(mapBlock, null, version);
		}

		/// <summary>Defines a view's functions.</summary>
		/// <remarks>
		/// Defines a view's functions.
		/// The view's definition is given as a class that conforms to the Mapper or
		/// Reducer interface (or null to delete the view). The body of the block
		/// should call the 'emit' object (passed in as a paramter) for every key/value pair
		/// it wants to write to the view.
		/// Since the function itself is obviously not stored in the database (only a unique
		/// string idenfitying it), you must re-define the view on every launch of the app!
		/// If the database needs to rebuild the view but the function hasn't been defined yet,
		/// it will fail and the view will be empty, causing weird problems later on.
		/// It is very important that this block be a law-abiding map function! As in other
		/// languages, it must be a "pure" function, with no side effects, that always emits
		/// the same values given the same input document. That means that it should not access
		/// or change any external state; be careful, since callbacks make that so easy that you
		/// might do it inadvertently!  The callback may be called on any thread, or on
		/// multiple threads simultaneously. This won't be a problem if the code is "pure" as
		/// described above, since it will as a consequence also be thread-safe.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual bool SetMapAndReduce(Mapper mapBlock, Reducer reduceBlock, string 
			version)
		{
			System.Diagnostics.Debug.Assert((mapBlock != null));
			System.Diagnostics.Debug.Assert((version != null));
			this.mapBlock = mapBlock;
			this.reduceBlock = reduceBlock;
			if (!database.Open())
			{
				return false;
			}
			// Update the version column in the database. This is a little weird looking
			// because we want to
			// avoid modifying the database if the version didn't change, and because the
			// row might not exist yet.
			SQLiteStorageEngine storageEngine = this.database.GetDatabase();
			// Older Android doesnt have reliable insert or ignore, will to 2 step
			// FIXME review need for change to execSQL, manual call to changes()
			string sql = "SELECT name, version FROM views WHERE name=?";
			string[] args = new string[] { name };
			Cursor cursor = null;
			try
			{
				cursor = storageEngine.RawQuery(sql, args);
				if (!cursor.MoveToNext())
				{
					// no such record, so insert
					ContentValues insertValues = new ContentValues();
					insertValues.Put("name", name);
					insertValues.Put("version", version);
					storageEngine.Insert("views", null, insertValues);
					return true;
				}
				ContentValues updateValues = new ContentValues();
				updateValues.Put("version", version);
				updateValues.Put("lastSequence", 0);
				string[] whereArgs = new string[] { name, version };
				int rowsAffected = storageEngine.Update("views", updateValues, "name=? AND version!=?"
					, whereArgs);
				return (rowsAffected > 0);
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error setting map block", e);
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

		/// <summary>Deletes the view's persistent index.</summary>
		/// <remarks>Deletes the view's persistent index. It will be regenerated on the next query.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual void DeleteIndex()
		{
			if (GetViewId() < 0)
			{
				return;
			}
			bool success = false;
			try
			{
				database.BeginTransaction();
				string[] whereArgs = new string[] { Sharpen.Extensions.ToString(GetViewId()) };
				database.GetDatabase().Delete("maps", "view_id=?", whereArgs);
				ContentValues updateValues = new ContentValues();
				updateValues.Put("lastSequence", 0);
				database.GetDatabase().Update("views", updateValues, "view_id=?", whereArgs);
				success = true;
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error removing index", e);
			}
			finally
			{
				database.EndTransaction(success);
			}
		}

		/// <summary>Deletes the view, persistently.</summary>
		/// <remarks>Deletes the view, persistently.</remarks>
		[InterfaceAudience.Public]
		public virtual void Delete()
		{
			database.DeleteViewNamed(name);
			viewId = 0;
		}

		/// <summary>Creates a new query object for this view.</summary>
		/// <remarks>Creates a new query object for this view. The query can be customized and then executed.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual Query CreateQuery()
		{
			return new Query(GetDatabase(), this);
		}

		public virtual int GetViewId()
		{
			if (viewId < 0)
			{
				string sql = "SELECT view_id FROM views WHERE name=?";
				string[] args = new string[] { name };
				Cursor cursor = null;
				try
				{
					cursor = database.GetDatabase().RawQuery(sql, args);
					if (cursor.MoveToNext())
					{
						viewId = cursor.GetInt(0);
					}
					else
					{
						viewId = 0;
					}
				}
				catch (SQLException e)
				{
					Log.E(Database.Tag, "Error getting view id", e);
					viewId = 0;
				}
				finally
				{
					if (cursor != null)
					{
						cursor.Close();
					}
				}
			}
			return viewId;
		}

		public virtual void DatabaseClosing()
		{
			database = null;
			viewId = 0;
		}

		/// <summary>Indexing</summary>
		public virtual string ToJSONString(object obj)
		{
			if (obj == null)
			{
				return null;
			}
			string result = null;
			try
			{
				result = Manager.GetObjectMapper().WriteValueAsString(obj);
			}
			catch (Exception e)
			{
				Log.W(Database.Tag, "Exception serializing object to json: " + obj, e);
			}
			return result;
		}

		public virtual object FromJSON(byte[] json)
		{
			if (json == null)
			{
				return null;
			}
			object result = null;
			try
			{
				result = Manager.GetObjectMapper().ReadValue<object>(json);
			}
			catch (Exception e)
			{
				Log.W(Database.Tag, "Exception parsing json", e);
			}
			return result;
		}

		public virtual View.TDViewCollation GetCollation()
		{
			return collation;
		}

		public virtual void SetCollation(View.TDViewCollation collation)
		{
			this.collation = collation;
		}

		/// <summary>Updates the view's index (incrementally) if necessary.</summary>
		/// <remarks>Updates the view's index (incrementally) if necessary.</remarks>
		/// <returns>200 if updated, 304 if already up-to-date, else an error code</returns>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void UpdateIndex()
		{
			Log.V(Database.Tag, "Re-indexing view " + name + " ...");
			System.Diagnostics.Debug.Assert((mapBlock != null));
			if (GetViewId() < 0)
			{
				string msg = string.Format("getViewId() < 0");
				throw new CouchbaseLiteException(msg, new Status(Status.NotFound));
			}
			database.BeginTransaction();
			Status result = new Status(Status.InternalServerError);
			Cursor cursor = null;
			try
			{
				long lastSequence = GetLastSequenceIndexed();
				long dbMaxSequence = database.GetLastSequenceNumber();
				if (lastSequence == dbMaxSequence)
				{
					// nothing to do (eg,  kCBLStatusNotModified)
					string msg = string.Format("lastSequence (%d) == dbMaxSequence (%d), nothing to do"
						, lastSequence, dbMaxSequence);
					Log.D(Database.Tag, msg);
					return;
				}
				// First remove obsolete emitted results from the 'maps' table:
				long sequence = lastSequence;
				if (lastSequence < 0)
				{
					string msg = string.Format("lastSequence < 0 (%s)", lastSequence);
					throw new CouchbaseLiteException(msg, new Status(Status.InternalServerError));
				}
				if (lastSequence == 0)
				{
					// If the lastSequence has been reset to 0, make sure to remove
					// any leftover rows:
					string[] whereArgs = new string[] { Sharpen.Extensions.ToString(GetViewId()) };
					database.GetDatabase().Delete("maps", "view_id=?", whereArgs);
				}
				else
				{
					// Delete all obsolete map results (ones from since-replaced
					// revisions):
					string[] args = new string[] { Sharpen.Extensions.ToString(GetViewId()), System.Convert.ToString
						(lastSequence), System.Convert.ToString(lastSequence) };
					database.GetDatabase().ExecSQL("DELETE FROM maps WHERE view_id=? AND sequence IN ("
						 + "SELECT parent FROM revs WHERE sequence>? " + "AND parent>0 AND parent<=?)", 
						args);
				}
				int deleted = 0;
				cursor = database.GetDatabase().RawQuery("SELECT changes()", null);
				cursor.MoveToNext();
				deleted = cursor.GetInt(0);
				cursor.Close();
				// This is the emit() block, which gets called from within the
				// user-defined map() block
				// that's called down below.
				AbstractTouchMapEmitBlock emitBlock = new _AbstractTouchMapEmitBlock_417(this);
				// find a better way to propagate this back
				// Now scan every revision added since the last time the view was
				// indexed:
				string[] selectArgs = new string[] { System.Convert.ToString(lastSequence) };
				cursor = database.GetDatabase().RawQuery("SELECT revs.doc_id, sequence, docid, revid, json FROM revs, docs "
					 + "WHERE sequence>? AND current!=0 AND deleted=0 " + "AND revs.doc_id = docs.doc_id "
					 + "ORDER BY revs.doc_id, revid DESC", selectArgs);
				cursor.MoveToNext();
				long lastDocID = 0;
				while (!cursor.IsAfterLast())
				{
					long docID = cursor.GetLong(0);
					if (docID != lastDocID)
					{
						// Only look at the first-iterated revision of any document,
						// because this is the
						// one with the highest revid, hence the "winning" revision
						// of a conflict.
						lastDocID = docID;
						// Reconstitute the document as a dictionary:
						sequence = cursor.GetLong(1);
						string docId = cursor.GetString(2);
						if (docId.StartsWith("_design/"))
						{
							// design docs don't get indexed!
							cursor.MoveToNext();
							continue;
						}
						string revId = cursor.GetString(3);
						byte[] json = cursor.GetBlob(4);
						IDictionary<string, object> properties = database.DocumentPropertiesFromJSON(json
							, docId, revId, false, sequence, EnumSet.NoneOf<TDContentOptions>());
						if (properties != null)
						{
							// Call the user-defined map() to emit new key/value
							// pairs from this revision:
							Log.V(Database.Tag, "  call map for sequence=" + System.Convert.ToString(sequence
								));
							emitBlock.SetSequence(sequence);
							mapBlock.Map(properties, emitBlock);
						}
					}
					cursor.MoveToNext();
				}
				// Finally, record the last revision sequence number that was
				// indexed:
				ContentValues updateValues = new ContentValues();
				updateValues.Put("lastSequence", dbMaxSequence);
				string[] whereArgs_1 = new string[] { Sharpen.Extensions.ToString(GetViewId()) };
				database.GetDatabase().Update("views", updateValues, "view_id=?", whereArgs_1);
				// FIXME actually count number added :)
				Log.V(Database.Tag, "...Finished re-indexing view " + name + " up to sequence " +
					 System.Convert.ToString(dbMaxSequence) + " (deleted " + deleted + " added " + "?"
					 + ")");
				result.SetCode(Status.Ok);
			}
			catch (SQLException e)
			{
				throw new CouchbaseLiteException(e, new Status(Status.DbError));
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
				if (!result.IsSuccessful())
				{
					Log.W(Database.Tag, "Failed to rebuild view " + name + ": " + result.GetCode());
				}
				if (database != null)
				{
					database.EndTransaction(result.IsSuccessful());
				}
			}
		}

		private sealed class _AbstractTouchMapEmitBlock_417 : AbstractTouchMapEmitBlock
		{
			public _AbstractTouchMapEmitBlock_417(View _enclosing)
			{
				this._enclosing = _enclosing;
			}

            internal void DefaultEmit(object key, object value)
			{
				try
				{
					string keyJson = Manager.GetObjectMapper().WriteValueAsString(key);
					string valueJson = Manager.GetObjectMapper().WriteValueAsString(value);
					Log.V(Database.Tag, "    emit(" + keyJson + ", " + valueJson + ")");
					ContentValues insertValues = new ContentValues();
					insertValues.Put("view_id", this._enclosing.GetViewId());
					insertValues.Put("sequence", this.sequence);
					insertValues.Put("key", keyJson);
					insertValues.Put("value", valueJson);
					this._enclosing.database.GetDatabase().Insert("maps", null, insertValues);
				}
				catch (Exception e)
				{
					Log.E(Database.Tag, "Error emitting", e);
				}
			}

			private readonly View _enclosing;
		}

		public virtual Cursor ResultSetWithOptions(QueryOptions options)
		{
			if (options == null)
			{
				options = new QueryOptions();
			}
			// OPT: It would be faster to use separate tables for raw-or ascii-collated views so that
			// they could be indexed with the right collation, instead of having to specify it here.
			string collationStr = string.Empty;
			if (collation == View.TDViewCollation.TDViewCollationASCII)
			{
				collationStr += " COLLATE JSON_ASCII";
			}
			else
			{
				if (collation == View.TDViewCollation.TDViewCollationRaw)
				{
					collationStr += " COLLATE JSON_RAW";
				}
			}
			string sql = "SELECT key, value, docid, revs.sequence";
			if (options.IsIncludeDocs())
			{
				sql = sql + ", revid, json";
			}
			sql = sql + " FROM maps, revs, docs WHERE maps.view_id=?";
			IList<string> argsList = new AList<string>();
			argsList.AddItem(Sharpen.Extensions.ToString(GetViewId()));
			if (options.GetKeys() != null)
			{
				sql += " AND key in (";
				string item = "?";
				foreach (object key in options.GetKeys())
				{
					sql += item;
					item = ", ?";
					argsList.AddItem(ToJSONString(key));
				}
				sql += ")";
			}
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
					sql += " AND key >= ?";
				}
				else
				{
					sql += " AND key > ?";
				}
				sql += collationStr;
				argsList.AddItem(ToJSONString(minKey));
			}
			if (maxKey != null)
			{
				System.Diagnostics.Debug.Assert((maxKey is string));
				if (inclusiveMax)
				{
					sql += " AND key <= ?";
				}
				else
				{
					sql += " AND key < ?";
				}
				sql += collationStr;
				argsList.AddItem(ToJSONString(maxKey));
			}
			sql = sql + " AND revs.sequence = maps.sequence AND docs.doc_id = revs.doc_id ORDER BY key";
			sql += collationStr;
			if (options.IsDescending())
			{
				sql = sql + " DESC";
			}
			sql = sql + " LIMIT ? OFFSET ?";
			argsList.AddItem(Sharpen.Extensions.ToString(options.GetLimit()));
			argsList.AddItem(Sharpen.Extensions.ToString(options.GetSkip()));
			Log.V(Database.Tag, "Query " + name + ": " + sql);
			Cursor cursor = database.GetDatabase().RawQuery(sql, Sharpen.Collections.ToArray(
				argsList, new string[argsList.Count]));
			return cursor;
		}

		// Are key1 and key2 grouped together at this groupLevel?
		public static bool GroupTogether(object key1, object key2, int groupLevel)
		{
			if (groupLevel == 0 || !(key1 is IList) || !(key2 is IList))
			{
				return key1.Equals(key2);
			}
			IList<object> key1List = (IList<object>)key1;
			IList<object> key2List = (IList<object>)key2;
			int end = Math.Min(groupLevel, Math.Min(key1List.Count, key2List.Count));
			for (int i = 0; i < end; ++i)
			{
				if (!key1List[i].Equals(key2List[i]))
				{
					return false;
				}
			}
			return true;
		}

		// Returns the prefix of the key to use in the result row, at this groupLevel
		public static object GroupKey(object key, int groupLevel)
		{
			if (groupLevel > 0 && (key is IList) && (((IList<object>)key).Count > groupLevel))
			{
				return ((IList<object>)key).SubList(0, groupLevel);
			}
			else
			{
				return key;
			}
		}

		/// <summary>Querying</summary>
		public virtual IList<IDictionary<string, object>> Dump()
		{
			if (GetViewId() < 0)
			{
				return null;
			}
			string[] selectArgs = new string[] { Sharpen.Extensions.ToString(GetViewId()) };
			Cursor cursor = null;
			IList<IDictionary<string, object>> result = null;
			try
			{
				cursor = database.GetDatabase().RawQuery("SELECT sequence, key, value FROM maps WHERE view_id=? ORDER BY key"
					, selectArgs);
				cursor.MoveToNext();
				result = new AList<IDictionary<string, object>>();
				while (!cursor.IsAfterLast())
				{
					IDictionary<string, object> row = new Dictionary<string, object>();
					row.Put("seq", cursor.GetInt(0));
					row.Put("key", cursor.GetString(1));
					row.Put("value", cursor.GetString(2));
					result.AddItem(row);
					cursor.MoveToNext();
				}
			}
			catch (SQLException e)
			{
				Log.E(Database.Tag, "Error dumping view", e);
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

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		internal virtual IList<QueryRow> ReducedQuery(Cursor cursor, bool group, int groupLevel)
		{
			IList<object> keysToReduce = null;
			IList<object> valuesToReduce = null;
			object lastKey = null;
			if (GetReduce() != null)
			{
				keysToReduce = new AList<object>(ReduceBatchSize);
				valuesToReduce = new AList<object>(ReduceBatchSize);
			}
			IList<QueryRow> rows = new AList<QueryRow>();
			cursor.MoveToNext();
			while (!cursor.IsAfterLast())
			{
				object keyData = FromJSON(cursor.GetBlob(0));
				object value = FromJSON(cursor.GetBlob(1));
				System.Diagnostics.Debug.Assert((keyData != null));
				if (group && !GroupTogether(keyData, lastKey, groupLevel))
				{
					if (lastKey != null)
					{
						// This pair starts a new group, so reduce & record the last one:
						object reduced = (reduceBlock != null) ? reduceBlock.Reduce(keysToReduce, valuesToReduce
							, false) : null;
						object key = GroupKey(lastKey, groupLevel);
						QueryRow row = new QueryRow(null, 0, key, reduced, null);
						row.SetDatabase(database);
						rows.AddItem(row);
						keysToReduce.Clear();
						valuesToReduce.Clear();
					}
					lastKey = keyData;
				}
				keysToReduce.AddItem(keyData);
				valuesToReduce.AddItem(value);
				cursor.MoveToNext();
			}
			if (keysToReduce.Count > 0)
			{
				// Finish the last group (or the entire list, if no grouping):
				object key = group ? GroupKey(lastKey, groupLevel) : null;
				object reduced = (reduceBlock != null) ? reduceBlock.Reduce(keysToReduce, valuesToReduce
					, false) : null;
				QueryRow row = new QueryRow(null, 0, key, reduced, null);
				row.SetDatabase(database);
				rows.AddItem(row);
			}
			return rows;
		}

		/// <summary>Queries the view.</summary>
		/// <remarks>Queries the view. Does NOT first update the index.</remarks>
		/// <param name="options">The options to use.</param>
		/// <returns>An array of QueryRow objects.</returns>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual IList<QueryRow> QueryWithOptions(QueryOptions options)
		{
			if (options == null)
			{
				options = new QueryOptions();
			}
			Cursor cursor = null;
			IList<QueryRow> rows = new AList<QueryRow>();
			try
			{
				cursor = ResultSetWithOptions(options);
				int groupLevel = options.GetGroupLevel();
				bool group = options.IsGroup() || (groupLevel > 0);
				bool reduce = options.IsReduce() || group;
				if (reduce && (reduceBlock == null) && !group)
				{
					string msg = "Cannot use reduce option in view " + name + " which has no reduce block defined";
					Log.W(Database.Tag, msg);
					throw new CouchbaseLiteException(new Status(Status.BadRequest));
				}
				if (reduce || group)
				{
					// Reduced or grouped query:
					rows = ReducedQuery(cursor, group, groupLevel);
				}
				else
				{
					// regular query
					cursor.MoveToNext();
					while (!cursor.IsAfterLast())
					{
						object keyData = FromJSON(cursor.GetBlob(0));
						// TODO: delay parsing this for increased efficiency
						object value = FromJSON(cursor.GetBlob(1));
						// TODO: ditto
						string docId = cursor.GetString(2);
						int sequence = Sharpen.Extensions.ValueOf(cursor.GetString(3));
						IDictionary<string, object> docContents = null;
						if (options.IsIncludeDocs())
						{
							// http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
							if (value is IDictionary && ((IDictionary)value).ContainsKey("_id"))
							{
								string linkedDocId = (string)((IDictionary)value).Get("_id");
								RevisionInternal linkedDoc = database.GetDocumentWithIDAndRev(linkedDocId, null, 
									EnumSet.NoneOf<TDContentOptions>());
								docContents = linkedDoc.GetProperties();
							}
							else
							{
								docContents = database.DocumentPropertiesFromJSON(cursor.GetBlob(5), docId, cursor
									.GetString(4), false, cursor.GetLong(3), options.GetContentOptions());
							}
						}
						QueryRow row = new QueryRow(docId, sequence, keyData, value, docContents);
						row.SetDatabase(database);
						rows.AddItem(row);
						cursor.MoveToNext();
					}
				}
			}
			catch (SQLException e)
			{
				string errMsg = string.Format("Error querying view: %s", this);
				Log.E(Database.Tag, errMsg, e);
				throw new CouchbaseLiteException(errMsg, e, new Status(Status.DbError));
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
			}
			return rows;
		}

		/// <summary>Utility function to use in reduce blocks.</summary>
		/// <remarks>Utility function to use in reduce blocks. Totals an array of Numbers.</remarks>
		public static double TotalValues(IList<object> values)
		{
			double total = 0;
			foreach (object obj in values)
			{
				if (obj is Number)
				{
					Number number = (Number)obj;
					total += number;
				}
				else
				{
					Log.W(Database.Tag, "Warning non-numeric value found in totalValues: " + obj);
				}
			}
			return total;
		}
	}

	internal abstract class AbstractTouchMapEmitBlock : Emitter
	{
		protected internal long sequence = 0;

		internal virtual void SetSequence(long sequence)
		{
			this.sequence = sequence;
		}

		public abstract void Emit(object arg1, object arg2);
	}
}
