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
using Couchbase;
using Couchbase.Storage;
using Couchbase.Util;
using Sharpen;

namespace Couchbase
{
	/// <summary>Represents a view available in a database.</summary>
	/// <remarks>Represents a view available in a database.</remarks>
	public class CBLView
	{
		public const int ReduceBatchSize = 100;

		public enum TDViewCollation
		{
			TDViewCollationUnicode,
			TDViewCollationRaw,
			TDViewCollationASCII
		}

		private CBLDatabase db;

		private string name;

		private int viewId;

		private CBLViewMapBlock mapBlock;

		private CBLViewReduceBlock reduceBlock;

		private CBLView.TDViewCollation collation;

		private static CBLViewCompiler compiler;

		public CBLView(CBLDatabase db, string name)
		{
			this.db = db;
			this.name = name;
			this.viewId = -1;
			// means 'unknown'
			this.collation = CBLView.TDViewCollation.TDViewCollationUnicode;
		}

		public virtual CBLDatabase GetDb()
		{
			return db;
		}

		public virtual string GetName()
		{
			return name;
		}

		public virtual CBLViewMapBlock GetMapBlock()
		{
			return mapBlock;
		}

		public virtual CBLViewReduceBlock GetReduceBlock()
		{
			return reduceBlock;
		}

		public virtual CBLView.TDViewCollation GetCollation()
		{
			return collation;
		}

		public virtual void SetCollation(CBLView.TDViewCollation collation)
		{
			this.collation = collation;
		}

		/// <summary>Is the view's index currently out of date?</summary>
		public virtual bool IsStale()
		{
			return (GetLastSequenceIndexed() < db.GetLastSequence());
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
					cursor = db.GetDatabase().RawQuery(sql, args);
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
					Log.E(CBLDatabase.Tag, "Error getting view id", e);
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

		public virtual long GetLastSequenceIndexed()
		{
			string sql = "SELECT lastSequence FROM views WHERE name=?";
			string[] args = new string[] { name };
			Cursor cursor = null;
			long result = -1;
			try
			{
				cursor = db.GetDatabase().RawQuery(sql, args);
				if (cursor.MoveToNext())
				{
					result = cursor.GetLong(0);
				}
			}
			catch (Exception)
			{
				Log.E(CBLDatabase.Tag, "Error getting last sequence indexed");
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

		public virtual bool SetMapReduceBlocks(CBLViewMapBlock mapBlock, CBLViewReduceBlock
			 reduceBlock, string version)
		{
			System.Diagnostics.Debug.Assert((mapBlock != null));
			System.Diagnostics.Debug.Assert((version != null));
			this.mapBlock = mapBlock;
			this.reduceBlock = reduceBlock;
			if (!db.Open())
			{
				return false;
			}
			// Update the version column in the db. This is a little weird looking
			// because we want to
			// avoid modifying the db if the version didn't change, and because the
			// row might not exist yet.
			SQLiteStorageEngine database = db.GetDatabase();
			// Older Android doesnt have reliable insert or ignore, will to 2 step
			// FIXME review need for change to execSQL, manual call to changes()
			string sql = "SELECT name, version FROM views WHERE name=?";
			string[] args = new string[] { name };
			Cursor cursor = null;
			try
			{
				cursor = db.GetDatabase().RawQuery(sql, args);
				if (!cursor.MoveToNext())
				{
					// no such record, so insert
					ContentValues insertValues = new ContentValues();
					insertValues.Put("name", name);
					insertValues.Put("version", version);
					database.Insert("views", null, insertValues);
					return true;
				}
				ContentValues updateValues = new ContentValues();
				updateValues.Put("version", version);
				updateValues.Put("lastSequence", 0);
				string[] whereArgs = new string[] { name, version };
				int rowsAffected = database.Update("views", updateValues, "name=? AND version!=?"
					, whereArgs);
				return (rowsAffected > 0);
			}
			catch (SQLException e)
			{
				Log.E(CBLDatabase.Tag, "Error setting map block", e);
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

		public virtual void RemoveIndex()
		{
			if (GetViewId() < 0)
			{
				return;
			}
			bool success = false;
			try
			{
				db.BeginTransaction();
				string[] whereArgs = new string[] { Sharpen.Extensions.ToString(GetViewId()) };
				db.GetDatabase().Delete("maps", "view_id=?", whereArgs);
				ContentValues updateValues = new ContentValues();
				updateValues.Put("lastSequence", 0);
				db.GetDatabase().Update("views", updateValues, "view_id=?", whereArgs);
				success = true;
			}
			catch (SQLException e)
			{
				Log.E(CBLDatabase.Tag, "Error removing index", e);
			}
			finally
			{
				db.EndTransaction(success);
			}
		}

		public virtual void DeleteView()
		{
			db.DeleteViewNamed(name);
			viewId = 0;
		}

		public virtual void DatabaseClosing()
		{
			db = null;
			viewId = 0;
		}

		/// <summary>Indexing</summary>
		public virtual string ToJSONString(object @object)
		{
			if (@object == null)
			{
				return null;
			}
			string result = null;
			try
			{
				result = CBLServer.GetObjectMapper().WriteValueAsString(@object);
			}
			catch (Exception)
			{
			}
			// ignore
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
				result = CBLServer.GetObjectMapper().ReadValue<object>(json);
			}
			catch (Exception)
			{
			}
			// ignore
			return result;
		}

		/// <summary>Updates the view's index (incrementally) if necessary.</summary>
		/// <remarks>Updates the view's index (incrementally) if necessary.</remarks>
		/// <returns>200 if updated, 304 if already up-to-date, else an error code</returns>
		public virtual CBLStatus UpdateIndex()
		{
			Log.V(CBLDatabase.Tag, "Re-indexing view " + name + " ...");
			System.Diagnostics.Debug.Assert((mapBlock != null));
			if (GetViewId() < 0)
			{
				return new CBLStatus(CBLStatus.NotFound);
			}
			db.BeginTransaction();
			CBLStatus result = new CBLStatus(CBLStatus.InternalServerError);
			Cursor cursor = null;
			try
			{
				long lastSequence = GetLastSequenceIndexed();
				long dbMaxSequence = db.GetLastSequence();
				if (lastSequence == dbMaxSequence)
				{
					result.SetCode(CBLStatus.NotModified);
					return result;
				}
				// First remove obsolete emitted results from the 'maps' table:
				long sequence = lastSequence;
				if (lastSequence < 0)
				{
					return result;
				}
				if (lastSequence == 0)
				{
					// If the lastSequence has been reset to 0, make sure to remove
					// any leftover rows:
					string[] whereArgs = new string[] { Sharpen.Extensions.ToString(GetViewId()) };
					db.GetDatabase().Delete("maps", "view_id=?", whereArgs);
				}
				else
				{
					// Delete all obsolete map results (ones from since-replaced
					// revisions):
					string[] args = new string[] { Sharpen.Extensions.ToString(GetViewId()), System.Convert.ToString
						(lastSequence), System.Convert.ToString(lastSequence) };
					db.GetDatabase().ExecSQL("DELETE FROM maps WHERE view_id=? AND sequence IN (" + "SELECT parent FROM revs WHERE sequence>? "
						 + "AND parent>0 AND parent<=?)", args);
				}
				int deleted = 0;
				cursor = db.GetDatabase().RawQuery("SELECT changes()", null);
				cursor.MoveToNext();
				deleted = cursor.GetInt(0);
				cursor.Close();
				// This is the emit() block, which gets called from within the
				// user-defined map() block
				// that's called down below.
				AbstractTouchMapEmitBlock emitBlock = new _AbstractTouchMapEmitBlock_312(this);
				// find a better way to propogate this back
				// Now scan every revision added since the last time the view was
				// indexed:
				string[] selectArgs = new string[] { System.Convert.ToString(lastSequence) };
				cursor = db.GetDatabase().RawQuery("SELECT revs.doc_id, sequence, docid, revid, json FROM revs, docs "
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
						IDictionary<string, object> properties = db.DocumentPropertiesFromJSON(json, docId
							, revId, sequence, EnumSet.NoneOf<CBLDatabase.TDContentOptions>());
						if (properties != null)
						{
							// Call the user-defined map() to emit new key/value
							// pairs from this revision:
							Log.V(CBLDatabase.Tag, "  call map for sequence=" + System.Convert.ToString(sequence
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
				db.GetDatabase().Update("views", updateValues, "view_id=?", whereArgs_1);
				// FIXME actually count number added :)
				Log.V(CBLDatabase.Tag, "...Finished re-indexing view " + name + " up to sequence "
					 + System.Convert.ToString(dbMaxSequence) + " (deleted " + deleted + " added " +
					 "?" + ")");
				result.SetCode(CBLStatus.Ok);
			}
			catch (SQLException)
			{
				return result;
			}
			finally
			{
				if (cursor != null)
				{
					cursor.Close();
				}
				if (!result.IsSuccessful())
				{
					Log.W(CBLDatabase.Tag, "Failed to rebuild view " + name + ": " + result.GetCode()
						);
				}
				if (db != null)
				{
					db.EndTransaction(result.IsSuccessful());
				}
			}
			return result;
		}

		private sealed class _AbstractTouchMapEmitBlock_312 : AbstractTouchMapEmitBlock
		{
			public _AbstractTouchMapEmitBlock_312(CBLView _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public override void Emit(object key, object value)
			{
				try
				{
					string keyJson = CBLServer.GetObjectMapper().WriteValueAsString(key);
					string valueJson = CBLServer.GetObjectMapper().WriteValueAsString(value);
					Log.V(CBLDatabase.Tag, "    emit(" + keyJson + ", " + valueJson + ")");
					ContentValues insertValues = new ContentValues();
					insertValues.Put("view_id", this._enclosing.GetViewId());
					insertValues.Put("sequence", this.sequence);
					insertValues.Put("key", keyJson);
					insertValues.Put("value", valueJson);
					this._enclosing.db.GetDatabase().Insert("maps", null, insertValues);
				}
				catch (Exception e)
				{
					Log.E(CBLDatabase.Tag, "Error emitting", e);
				}
			}

			private readonly CBLView _enclosing;
		}

		public virtual Cursor ResultSetWithOptions(CBLQueryOptions options, CBLStatus status
			)
		{
			if (options == null)
			{
				options = new CBLQueryOptions();
			}
			// OPT: It would be faster to use separate tables for raw-or ascii-collated views so that
			// they could be indexed with the right collation, instead of having to specify it here.
			string collationStr = string.Empty;
			if (collation == CBLView.TDViewCollation.TDViewCollationASCII)
			{
				collationStr += " COLLATE JSON_ASCII";
			}
			else
			{
				if (collation == CBLView.TDViewCollation.TDViewCollationRaw)
				{
					collationStr += " COLLATE JSON_RAW";
				}
			}
			string sql = "SELECT key, value, docid";
			if (options.IsIncludeDocs())
			{
				sql = sql + ", revid, json, revs.sequence";
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
			Log.V(CBLDatabase.Tag, "Query " + name + ": " + sql);
			Cursor cursor = db.GetDatabase().RawQuery(sql, Sharpen.Collections.ToArray(argsList
				, new string[argsList.Count]));
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
				cursor = db.GetDatabase().RawQuery("SELECT sequence, key, value FROM maps WHERE view_id=? ORDER BY key"
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
				Log.E(CBLDatabase.Tag, "Error dumping view", e);
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

		/// <summary>Queries the view.</summary>
		/// <remarks>Queries the view. Does NOT first update the index.</remarks>
		/// <param name="options">The options to use.</param>
		/// <param name="status">An array of result rows -- each is a dictionary with "key" and "value" keys, and possibly "id" and "doc".
		/// 	</param>
		public virtual IList<IDictionary<string, object>> QueryWithOptions(CBLQueryOptions
			 options, CBLStatus status)
		{
			if (options == null)
			{
				options = new CBLQueryOptions();
			}
			Cursor cursor = null;
			IList<IDictionary<string, object>> rows = new AList<IDictionary<string, object>>(
				);
			try
			{
				cursor = ResultSetWithOptions(options, status);
				int groupLevel = options.GetGroupLevel();
				bool group = options.IsGroup() || (groupLevel > 0);
				bool reduce = options.IsReduce() || group;
				if (reduce && (reduceBlock == null) && !group)
				{
					Log.W(CBLDatabase.Tag, "Cannot use reduce option in view " + name + " which has no reduce block defined"
						);
					status.SetCode(CBLStatus.BadRequest);
					return null;
				}
				IList<object> keysToReduce = null;
				IList<object> valuesToReduce = null;
				object lastKey = null;
				if (reduce)
				{
					keysToReduce = new AList<object>(ReduceBatchSize);
					valuesToReduce = new AList<object>(ReduceBatchSize);
				}
				cursor.MoveToNext();
				while (!cursor.IsAfterLast())
				{
					object key = FromJSON(cursor.GetBlob(0));
					object value = FromJSON(cursor.GetBlob(1));
					System.Diagnostics.Debug.Assert((key != null));
					if (reduce)
					{
						// Reduced or grouped query:
						if (group && !GroupTogether(key, lastKey, groupLevel) && (lastKey != null))
						{
							// This pair starts a new group, so reduce & record the last one:
							object reduced = (reduceBlock != null) ? reduceBlock.Reduce(keysToReduce, valuesToReduce
								, false) : null;
							IDictionary<string, object> row = new Dictionary<string, object>();
							row.Put("key", GroupKey(lastKey, groupLevel));
							if (reduced != null)
							{
								row.Put("value", reduced);
							}
							rows.AddItem(row);
							keysToReduce.Clear();
							valuesToReduce.Clear();
						}
						keysToReduce.AddItem(key);
						valuesToReduce.AddItem(value);
						lastKey = key;
					}
					else
					{
						// Regular query:
						IDictionary<string, object> row = new Dictionary<string, object>();
						string docId = cursor.GetString(2);
						IDictionary<string, object> docContents = null;
						if (options.IsIncludeDocs())
						{
							// http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
							if (value is IDictionary && ((IDictionary)value).ContainsKey("_id"))
							{
								string linkedDocId = (string)((IDictionary)value).Get("_id");
								CBLRevision linkedDoc = db.GetDocumentWithIDAndRev(linkedDocId, null, EnumSet.NoneOf
									<CBLDatabase.TDContentOptions>());
								docContents = linkedDoc.GetProperties();
							}
							else
							{
								docContents = db.DocumentPropertiesFromJSON(cursor.GetBlob(4), docId, cursor.GetString
									(3), cursor.GetLong(5), options.GetContentOptions());
							}
						}
						if (docContents != null)
						{
							row.Put("doc", docContents);
						}
						if (value != null)
						{
							row.Put("value", value);
						}
						row.Put("id", docId);
						row.Put("key", key);
						rows.AddItem(row);
					}
					cursor.MoveToNext();
				}
				if (reduce)
				{
					if (keysToReduce.Count > 0)
					{
						// Finish the last group (or the entire list, if no grouping):
						object key = group ? GroupKey(lastKey, groupLevel) : null;
						object reduced = (reduceBlock != null) ? reduceBlock.Reduce(keysToReduce, valuesToReduce
							, false) : null;
						IDictionary<string, object> row = new Dictionary<string, object>();
						row.Put("key", key);
						if (reduced != null)
						{
							row.Put("value", reduced);
						}
						rows.AddItem(row);
					}
					keysToReduce.Clear();
					valuesToReduce.Clear();
				}
				status.SetCode(CBLStatus.Ok);
			}
			catch (SQLException e)
			{
				Log.E(CBLDatabase.Tag, "Error querying view", e);
				return null;
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
			foreach (object @object in values)
			{
				if (@object is Number)
				{
					Number number = (Number)@object;
					total += number;
				}
				else
				{
					Log.W(CBLDatabase.Tag, "Warning non-numeric value found in totalValues: " + @object
						);
				}
			}
			return total;
		}

		public static CBLViewCompiler GetCompiler()
		{
			return compiler;
		}

		public static void SetCompiler(CBLViewCompiler compiler)
		{
			Couchbase.CBLView.compiler = compiler;
		}
	}

	internal abstract class AbstractTouchMapEmitBlock : CBLViewMapEmitBlock
	{
		protected internal long sequence = 0;

		internal virtual void SetSequence(long sequence)
		{
			this.sequence = sequence;
		}

		public abstract void Emit(object arg1, object arg2);
	}
}
