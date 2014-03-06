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
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class ViewsTest : LiteTestCase
	{
		public const string Tag = "Views";

		public virtual void TestQueryDefaultIndexUpdateMode()
		{
			View view = database.GetView("aview");
			Query query = view.CreateQuery();
			NUnit.Framework.Assert.AreEqual(Query.IndexUpdateMode.Before, query.GetIndexUpdateMode
				());
		}

		public virtual void TestViewCreation()
		{
			NUnit.Framework.Assert.IsNull(database.GetExistingView("aview"));
			View view = database.GetView("aview");
			NUnit.Framework.Assert.IsNotNull(view);
			NUnit.Framework.Assert.AreEqual(database, view.GetDatabase());
			NUnit.Framework.Assert.AreEqual("aview", view.GetName());
			NUnit.Framework.Assert.IsNull(view.GetMap());
			NUnit.Framework.Assert.AreEqual(view, database.GetExistingView("aview"));
			bool changed = view.SetMapReduce(new _Mapper_55(), null, "1");
			//no-op
			NUnit.Framework.Assert.IsTrue(changed);
			NUnit.Framework.Assert.AreEqual(1, database.GetAllViews().Count);
			NUnit.Framework.Assert.AreEqual(view, database.GetAllViews()[0]);
			changed = view.SetMapReduce(new _Mapper_67(), null, "1");
			//no-op
			NUnit.Framework.Assert.IsFalse(changed);
			changed = view.SetMapReduce(new _Mapper_77(), null, "2");
			//no-op
			NUnit.Framework.Assert.IsTrue(changed);
		}

		private sealed class _Mapper_55 : Mapper
		{
			public _Mapper_55()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
			}
		}

		private sealed class _Mapper_67 : Mapper
		{
			public _Mapper_67()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
			}
		}

		private sealed class _Mapper_77 : Mapper
		{
			public _Mapper_77()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		private RevisionInternal PutDoc(Database db, IDictionary<string, object> props)
		{
			RevisionInternal rev = new RevisionInternal(props, db);
			Status status = new Status();
			rev = db.PutRevision(rev, null, false, status);
			NUnit.Framework.Assert.IsTrue(status.IsSuccessful());
			return rev;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		private void PutDocViaUntitledDoc(Database db, IDictionary<string, object> props)
		{
			Document document = db.CreateDocument();
			document.PutProperties(props);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual IList<RevisionInternal> PutDocs(Database db)
		{
			IList<RevisionInternal> result = new AList<RevisionInternal>();
			IDictionary<string, object> dict2 = new Dictionary<string, object>();
			dict2.Put("_id", "22222");
			dict2.Put("key", "two");
			result.AddItem(PutDoc(db, dict2));
			IDictionary<string, object> dict4 = new Dictionary<string, object>();
			dict4.Put("_id", "44444");
			dict4.Put("key", "four");
			result.AddItem(PutDoc(db, dict4));
			IDictionary<string, object> dict1 = new Dictionary<string, object>();
			dict1.Put("_id", "11111");
			dict1.Put("key", "one");
			result.AddItem(PutDoc(db, dict1));
			IDictionary<string, object> dict3 = new Dictionary<string, object>();
			dict3.Put("_id", "33333");
			dict3.Put("key", "three");
			result.AddItem(PutDoc(db, dict3));
			IDictionary<string, object> dict5 = new Dictionary<string, object>();
			dict5.Put("_id", "55555");
			dict5.Put("key", "five");
			result.AddItem(PutDoc(db, dict5));
			return result;
		}

		// http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual IList<RevisionInternal> PutLinkedDocs(Database db)
		{
			IList<RevisionInternal> result = new AList<RevisionInternal>();
			IDictionary<string, object> dict1 = new Dictionary<string, object>();
			dict1.Put("_id", "11111");
			result.AddItem(PutDoc(db, dict1));
			IDictionary<string, object> dict2 = new Dictionary<string, object>();
			dict2.Put("_id", "22222");
			dict2.Put("value", "hello");
			dict2.Put("ancestors", new string[] { "11111" });
			result.AddItem(PutDoc(db, dict2));
			IDictionary<string, object> dict3 = new Dictionary<string, object>();
			dict3.Put("_id", "33333");
			dict3.Put("value", "world");
			dict3.Put("ancestors", new string[] { "22222", "11111" });
			result.AddItem(PutDoc(db, dict3));
			return result;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void PutNDocs(Database db, int n)
		{
			for (int i = 0; i < n; i++)
			{
				IDictionary<string, object> doc = new Dictionary<string, object>();
				doc.Put("_id", string.Format("%d", i));
				IList<string> key = new AList<string>();
				for (int j = 0; j < 256; j++)
				{
					key.AddItem("key");
				}
				key.AddItem(string.Format("key-%d", i));
				doc.Put("key", key);
				PutDocViaUntitledDoc(db, doc);
			}
		}

		public static View CreateView(Database db)
		{
			View view = db.GetView("aview");
			view.SetMapReduce(new _Mapper_172(), null, "1");
			return view;
		}

		private sealed class _Mapper_172 : Mapper
		{
			public _Mapper_172()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				NUnit.Framework.Assert.IsNotNull(document.Get("_id"));
				NUnit.Framework.Assert.IsNotNull(document.Get("_rev"));
				if (document.Get("key") != null)
				{
					emitter.Emit(document.Get("key"), null);
				}
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewIndex()
		{
			int numTimesMapFunctionInvoked = 0;
			IDictionary<string, object> dict1 = new Dictionary<string, object>();
			dict1.Put("key", "one");
			IDictionary<string, object> dict2 = new Dictionary<string, object>();
			dict2.Put("key", "two");
			IDictionary<string, object> dict3 = new Dictionary<string, object>();
			dict3.Put("key", "three");
			IDictionary<string, object> dictX = new Dictionary<string, object>();
			dictX.Put("clef", "quatre");
			RevisionInternal rev1 = PutDoc(database, dict1);
			RevisionInternal rev2 = PutDoc(database, dict2);
			RevisionInternal rev3 = PutDoc(database, dict3);
			PutDoc(database, dictX);
			View view = database.GetView("aview");
			_T1975167965 mapBlock = new _T1975167965(this);
			view.SetMap(mapBlock, "1");
			NUnit.Framework.Assert.AreEqual(1, view.GetViewId());
			NUnit.Framework.Assert.IsTrue(view.IsStale());
			view.UpdateIndex();
			IList<IDictionary<string, object>> dumpResult = view.Dump();
			Log.V(Tag, "View dump: " + dumpResult);
			NUnit.Framework.Assert.AreEqual(3, dumpResult.Count);
			NUnit.Framework.Assert.AreEqual("\"one\"", dumpResult[0].Get("key"));
			NUnit.Framework.Assert.AreEqual(1, dumpResult[0].Get("seq"));
			NUnit.Framework.Assert.AreEqual("\"two\"", dumpResult[2].Get("key"));
			NUnit.Framework.Assert.AreEqual(2, dumpResult[2].Get("seq"));
			NUnit.Framework.Assert.AreEqual("\"three\"", dumpResult[1].Get("key"));
			NUnit.Framework.Assert.AreEqual(3, dumpResult[1].Get("seq"));
			//no-op reindex
			NUnit.Framework.Assert.IsFalse(view.IsStale());
			view.UpdateIndex();
			// Now add a doc and update a doc:
			RevisionInternal threeUpdated = new RevisionInternal(rev3.GetDocId(), rev3.GetRevId
				(), false, database);
			numTimesMapFunctionInvoked = mapBlock.GetNumTimesInvoked();
			IDictionary<string, object> newdict3 = new Dictionary<string, object>();
			newdict3.Put("key", "3hree");
			threeUpdated.SetProperties(newdict3);
			Status status = new Status();
			rev3 = database.PutRevision(threeUpdated, rev3.GetRevId(), false, status);
			NUnit.Framework.Assert.IsTrue(status.IsSuccessful());
			// Reindex again:
			NUnit.Framework.Assert.IsTrue(view.IsStale());
			view.UpdateIndex();
			// Make sure the map function was only invoked one more time (for the document that was added)
			NUnit.Framework.Assert.AreEqual(mapBlock.GetNumTimesInvoked(), numTimesMapFunctionInvoked
				 + 1);
			IDictionary<string, object> dict4 = new Dictionary<string, object>();
			dict4.Put("key", "four");
			RevisionInternal rev4 = PutDoc(database, dict4);
			RevisionInternal twoDeleted = new RevisionInternal(rev2.GetDocId(), rev2.GetRevId
				(), true, database);
			database.PutRevision(twoDeleted, rev2.GetRevId(), false, status);
			NUnit.Framework.Assert.IsTrue(status.IsSuccessful());
			// Reindex again:
			NUnit.Framework.Assert.IsTrue(view.IsStale());
			view.UpdateIndex();
			dumpResult = view.Dump();
			Log.V(Tag, "View dump: " + dumpResult);
			NUnit.Framework.Assert.AreEqual(3, dumpResult.Count);
			NUnit.Framework.Assert.AreEqual("\"one\"", dumpResult[2].Get("key"));
			NUnit.Framework.Assert.AreEqual(1, dumpResult[2].Get("seq"));
			NUnit.Framework.Assert.AreEqual("\"3hree\"", dumpResult[0].Get("key"));
			NUnit.Framework.Assert.AreEqual(5, dumpResult[0].Get("seq"));
			NUnit.Framework.Assert.AreEqual("\"four\"", dumpResult[1].Get("key"));
			NUnit.Framework.Assert.AreEqual(6, dumpResult[1].Get("seq"));
			// Now do a real query:
			IList<QueryRow> rows = view.QueryWithOptions(null);
			NUnit.Framework.Assert.AreEqual(3, rows.Count);
			NUnit.Framework.Assert.AreEqual("one", rows[2].GetKey());
			NUnit.Framework.Assert.AreEqual(rev1.GetDocId(), rows[2].GetDocumentId());
			NUnit.Framework.Assert.AreEqual("3hree", rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(rev3.GetDocId(), rows[0].GetDocumentId());
			NUnit.Framework.Assert.AreEqual("four", rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(rev4.GetDocId(), rows[1].GetDocumentId());
			view.DeleteIndex();
		}

		internal class _T1975167965 : Mapper
		{
			internal int numTimesInvoked = 0;

			public virtual void Map(IDictionary<string, object> document, Emitter emitter)
			{
				this.numTimesInvoked += 1;
				NUnit.Framework.Assert.IsNotNull(document.Get("_id"));
				NUnit.Framework.Assert.IsNotNull(document.Get("_rev"));
				if (document.Get("key") != null)
				{
					emitter.Emit(document.Get("key"), null);
				}
			}

			public virtual int GetNumTimesInvoked()
			{
				return this.numTimesInvoked;
			}

			internal _T1975167965(ViewsTest _enclosing)
			{
				this._enclosing = _enclosing;
			}

			private readonly ViewsTest _enclosing;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewQuery()
		{
			PutDocs(database);
			View view = CreateView(database);
			view.UpdateIndex();
			// Query all rows:
			QueryOptions options = new QueryOptions();
			IList<QueryRow> rows = view.QueryWithOptions(options);
			IList<object> expectedRows = new AList<object>();
			IDictionary<string, object> dict5 = new Dictionary<string, object>();
			dict5.Put("id", "55555");
			dict5.Put("key", "five");
			expectedRows.AddItem(dict5);
			IDictionary<string, object> dict4 = new Dictionary<string, object>();
			dict4.Put("id", "44444");
			dict4.Put("key", "four");
			expectedRows.AddItem(dict4);
			IDictionary<string, object> dict1 = new Dictionary<string, object>();
			dict1.Put("id", "11111");
			dict1.Put("key", "one");
			expectedRows.AddItem(dict1);
			IDictionary<string, object> dict3 = new Dictionary<string, object>();
			dict3.Put("id", "33333");
			dict3.Put("key", "three");
			expectedRows.AddItem(dict3);
			IDictionary<string, object> dict2 = new Dictionary<string, object>();
			dict2.Put("id", "22222");
			dict2.Put("key", "two");
			expectedRows.AddItem(dict2);
			NUnit.Framework.Assert.AreEqual(5, rows.Count);
			NUnit.Framework.Assert.AreEqual(dict5.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(dict5.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(dict4.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(dict4.Get("value"), rows[1].GetValue());
			NUnit.Framework.Assert.AreEqual(dict1.Get("key"), rows[2].GetKey());
			NUnit.Framework.Assert.AreEqual(dict1.Get("value"), rows[2].GetValue());
			NUnit.Framework.Assert.AreEqual(dict3.Get("key"), rows[3].GetKey());
			NUnit.Framework.Assert.AreEqual(dict3.Get("value"), rows[3].GetValue());
			NUnit.Framework.Assert.AreEqual(dict2.Get("key"), rows[4].GetKey());
			NUnit.Framework.Assert.AreEqual(dict2.Get("value"), rows[4].GetValue());
			// Start/end key query:
			options = new QueryOptions();
			options.SetStartKey("a");
			options.SetEndKey("one");
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<object>();
			expectedRows.AddItem(dict5);
			expectedRows.AddItem(dict4);
			expectedRows.AddItem(dict1);
			NUnit.Framework.Assert.AreEqual(3, rows.Count);
			NUnit.Framework.Assert.AreEqual(dict5.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(dict5.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(dict4.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(dict4.Get("value"), rows[1].GetValue());
			NUnit.Framework.Assert.AreEqual(dict1.Get("key"), rows[2].GetKey());
			NUnit.Framework.Assert.AreEqual(dict1.Get("value"), rows[2].GetValue());
			// Start/end query without inclusive end:
			options.SetInclusiveEnd(false);
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<object>();
			expectedRows.AddItem(dict5);
			expectedRows.AddItem(dict4);
			NUnit.Framework.Assert.AreEqual(2, rows.Count);
			NUnit.Framework.Assert.AreEqual(dict5.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(dict5.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(dict4.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(dict4.Get("value"), rows[1].GetValue());
			// Reversed:
			options.SetDescending(true);
			options.SetStartKey("o");
			options.SetEndKey("five");
			options.SetInclusiveEnd(true);
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<object>();
			expectedRows.AddItem(dict4);
			expectedRows.AddItem(dict5);
			NUnit.Framework.Assert.AreEqual(2, rows.Count);
			NUnit.Framework.Assert.AreEqual(dict4.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(dict4.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(dict5.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(dict5.Get("value"), rows[1].GetValue());
			// Reversed, no inclusive end:
			options.SetInclusiveEnd(false);
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<object>();
			expectedRows.AddItem(dict4);
			NUnit.Framework.Assert.AreEqual(1, rows.Count);
			NUnit.Framework.Assert.AreEqual(dict4.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(dict4.Get("value"), rows[0].GetValue());
			// Specific keys:
			options = new QueryOptions();
			IList<object> keys = new AList<object>();
			keys.AddItem("two");
			keys.AddItem("four");
			options.SetKeys(keys);
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<object>();
			expectedRows.AddItem(dict4);
			expectedRows.AddItem(dict2);
			NUnit.Framework.Assert.AreEqual(2, rows.Count);
			NUnit.Framework.Assert.AreEqual(dict4.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(dict4.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(dict2.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(dict2.Get("value"), rows[1].GetValue());
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestAllDocsQuery()
		{
			IList<RevisionInternal> docs = PutDocs(database);
			IList<QueryRow> expectedRow = new AList<QueryRow>();
			foreach (RevisionInternal rev in docs)
			{
				IDictionary<string, object> value = new Dictionary<string, object>();
				value.Put("rev", rev.GetRevId());
				value.Put("_conflicts", new AList<string>());
				QueryRow queryRow = new QueryRow(rev.GetDocId(), 0, rev.GetDocId(), value, null);
				queryRow.SetDatabase(database);
				expectedRow.AddItem(queryRow);
			}
			QueryOptions options = new QueryOptions();
			IDictionary<string, object> allDocs = database.GetAllDocs(options);
			IList<QueryRow> expectedRows = new AList<QueryRow>();
			expectedRows.AddItem(expectedRow[2]);
			expectedRows.AddItem(expectedRow[0]);
			expectedRows.AddItem(expectedRow[3]);
			expectedRows.AddItem(expectedRow[1]);
			expectedRows.AddItem(expectedRow[4]);
			IDictionary<string, object> expectedQueryResult = CreateExpectedQueryResult(expectedRows
				, 0);
			NUnit.Framework.Assert.AreEqual(expectedQueryResult, allDocs);
			// Start/end key query:
			options = new QueryOptions();
			options.SetStartKey("2");
			options.SetEndKey("44444");
			allDocs = database.GetAllDocs(options);
			expectedRows = new AList<QueryRow>();
			expectedRows.AddItem(expectedRow[0]);
			expectedRows.AddItem(expectedRow[3]);
			expectedRows.AddItem(expectedRow[1]);
			expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
			NUnit.Framework.Assert.AreEqual(expectedQueryResult, allDocs);
			// Start/end query without inclusive end:
			options.SetInclusiveEnd(false);
			allDocs = database.GetAllDocs(options);
			expectedRows = new AList<QueryRow>();
			expectedRows.AddItem(expectedRow[0]);
			expectedRows.AddItem(expectedRow[3]);
			expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
			NUnit.Framework.Assert.AreEqual(expectedQueryResult, allDocs);
			// Get all documents: with default QueryOptions
			options = new QueryOptions();
			allDocs = database.GetAllDocs(options);
			expectedRows = new AList<QueryRow>();
			expectedRows.AddItem(expectedRow[2]);
			expectedRows.AddItem(expectedRow[0]);
			expectedRows.AddItem(expectedRow[3]);
			expectedRows.AddItem(expectedRow[1]);
			expectedRows.AddItem(expectedRow[4]);
			expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
			NUnit.Framework.Assert.AreEqual(expectedQueryResult, allDocs);
			// Get specific documents:
			options = new QueryOptions();
			IList<object> docIds = new AList<object>();
			QueryRow expected2 = expectedRow[2];
			docIds.AddItem(expected2.GetDocument().GetId());
			options.SetKeys(docIds);
			allDocs = database.GetAllDocs(options);
			expectedRows = new AList<QueryRow>();
			expectedRows.AddItem(expected2);
			expectedQueryResult = CreateExpectedQueryResult(expectedRows, 0);
			NUnit.Framework.Assert.AreEqual(expectedQueryResult, allDocs);
		}

		private IDictionary<string, object> CreateExpectedQueryResult(IList<QueryRow> rows
			, int offset)
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("rows", rows);
			result.Put("total_rows", rows.Count);
			result.Put("offset", offset);
			return result;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewReduce()
		{
			IDictionary<string, object> docProperties1 = new Dictionary<string, object>();
			docProperties1.Put("_id", "CD");
			docProperties1.Put("cost", 8.99);
			PutDoc(database, docProperties1);
			IDictionary<string, object> docProperties2 = new Dictionary<string, object>();
			docProperties2.Put("_id", "App");
			docProperties2.Put("cost", 1.95);
			PutDoc(database, docProperties2);
			IDictionary<string, object> docProperties3 = new Dictionary<string, object>();
			docProperties3.Put("_id", "Dessert");
			docProperties3.Put("cost", 6.50);
			PutDoc(database, docProperties3);
			View view = database.GetView("totaler");
			view.SetMapReduce(new _Mapper_544(), new _Reducer_555(), "1");
			view.UpdateIndex();
			IList<IDictionary<string, object>> dumpResult = view.Dump();
			Log.V(Tag, "View dump: " + dumpResult);
			NUnit.Framework.Assert.AreEqual(3, dumpResult.Count);
			NUnit.Framework.Assert.AreEqual("\"App\"", dumpResult[0].Get("key"));
			NUnit.Framework.Assert.AreEqual("1.95", dumpResult[0].Get("value"));
			NUnit.Framework.Assert.AreEqual(2, dumpResult[0].Get("seq"));
			NUnit.Framework.Assert.AreEqual("\"CD\"", dumpResult[1].Get("key"));
			NUnit.Framework.Assert.AreEqual("8.99", dumpResult[1].Get("value"));
			NUnit.Framework.Assert.AreEqual(1, dumpResult[1].Get("seq"));
			NUnit.Framework.Assert.AreEqual("\"Dessert\"", dumpResult[2].Get("key"));
			NUnit.Framework.Assert.AreEqual("6.5", dumpResult[2].Get("value"));
			NUnit.Framework.Assert.AreEqual(3, dumpResult[2].Get("seq"));
			QueryOptions options = new QueryOptions();
			options.SetReduce(true);
			IList<QueryRow> reduced = view.QueryWithOptions(options);
			NUnit.Framework.Assert.AreEqual(1, reduced.Count);
			object value = reduced[0].GetValue();
			Number numberValue = (Number)value;
			NUnit.Framework.Assert.IsTrue(Math.Abs(numberValue - 17.44) < 0.001);
		}

		private sealed class _Mapper_544 : Mapper
		{
			public _Mapper_544()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				NUnit.Framework.Assert.IsNotNull(document.Get("_id"));
				NUnit.Framework.Assert.IsNotNull(document.Get("_rev"));
				object cost = document.Get("cost");
				if (cost != null)
				{
					emitter.Emit(document.Get("_id"), cost);
				}
			}
		}

		private sealed class _Reducer_555 : Reducer
		{
			public _Reducer_555()
			{
			}

			public object Reduce(IList<object> keys, IList<object> values, bool rereduce)
			{
				return View.TotalValues(values);
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestIndexUpdateMode()
		{
			View view = CreateView(database);
			Query query = view.CreateQuery();
			query.SetIndexUpdateMode(Query.IndexUpdateMode.Before);
			int numRowsBefore = query.Run().GetCount();
			NUnit.Framework.Assert.AreEqual(0, numRowsBefore);
			// do a query and force re-indexing, number of results should be +4
			PutNDocs(database, 1);
			query.SetIndexUpdateMode(Query.IndexUpdateMode.Before);
			NUnit.Framework.Assert.AreEqual(1, query.Run().GetCount());
			// do a query without re-indexing, number of results should be the same
			PutNDocs(database, 4);
			query.SetIndexUpdateMode(Query.IndexUpdateMode.Never);
			NUnit.Framework.Assert.AreEqual(1, query.Run().GetCount());
			// do a query and force re-indexing, number of results should be +4
			query.SetIndexUpdateMode(Query.IndexUpdateMode.Before);
			NUnit.Framework.Assert.AreEqual(5, query.Run().GetCount());
			// do a query which will kick off an async index
			PutNDocs(database, 1);
			query.SetIndexUpdateMode(Query.IndexUpdateMode.After);
			query.Run().GetCount();
			// wait until indexing is (hopefully) done
			try
			{
				Sharpen.Thread.Sleep(1 * 1000);
			}
			catch (Exception e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
			}
			NUnit.Framework.Assert.AreEqual(6, query.Run().GetCount());
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewGrouped()
		{
			IDictionary<string, object> docProperties1 = new Dictionary<string, object>();
			docProperties1.Put("_id", "1");
			docProperties1.Put("artist", "Gang Of Four");
			docProperties1.Put("album", "Entertainment!");
			docProperties1.Put("track", "Ether");
			docProperties1.Put("time", 231);
			PutDoc(database, docProperties1);
			IDictionary<string, object> docProperties2 = new Dictionary<string, object>();
			docProperties2.Put("_id", "2");
			docProperties2.Put("artist", "Gang Of Four");
			docProperties2.Put("album", "Songs Of The Free");
			docProperties2.Put("track", "I Love A Man In Uniform");
			docProperties2.Put("time", 248);
			PutDoc(database, docProperties2);
			IDictionary<string, object> docProperties3 = new Dictionary<string, object>();
			docProperties3.Put("_id", "3");
			docProperties3.Put("artist", "Gang Of Four");
			docProperties3.Put("album", "Entertainment!");
			docProperties3.Put("track", "Natural's Not In It");
			docProperties3.Put("time", 187);
			PutDoc(database, docProperties3);
			IDictionary<string, object> docProperties4 = new Dictionary<string, object>();
			docProperties4.Put("_id", "4");
			docProperties4.Put("artist", "PiL");
			docProperties4.Put("album", "Metal Box");
			docProperties4.Put("track", "Memories");
			docProperties4.Put("time", 309);
			PutDoc(database, docProperties4);
			IDictionary<string, object> docProperties5 = new Dictionary<string, object>();
			docProperties5.Put("_id", "5");
			docProperties5.Put("artist", "Gang Of Four");
			docProperties5.Put("album", "Entertainment!");
			docProperties5.Put("track", "Not Great Men");
			docProperties5.Put("time", 187);
			PutDoc(database, docProperties5);
			View view = database.GetView("grouper");
			view.SetMapReduce(new _Mapper_671(), new _Reducer_681(), "1");
			Status status = new Status();
			view.UpdateIndex();
			QueryOptions options = new QueryOptions();
			options.SetReduce(true);
			IList<QueryRow> rows = view.QueryWithOptions(options);
			IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object
				>>();
			IDictionary<string, object> row1 = new Dictionary<string, object>();
			row1.Put("key", null);
			row1.Put("value", 1162.0);
			expectedRows.AddItem(row1);
			NUnit.Framework.Assert.AreEqual(row1.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(row1.Get("value"), rows[0].GetValue());
			//now group
			options.SetGroup(true);
			status = new Status();
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<IDictionary<string, object>>();
			row1 = new Dictionary<string, object>();
			IList<string> key1 = new AList<string>();
			key1.AddItem("Gang Of Four");
			key1.AddItem("Entertainment!");
			key1.AddItem("Ether");
			row1.Put("key", key1);
			row1.Put("value", 231.0);
			expectedRows.AddItem(row1);
			IDictionary<string, object> row2 = new Dictionary<string, object>();
			IList<string> key2 = new AList<string>();
			key2.AddItem("Gang Of Four");
			key2.AddItem("Entertainment!");
			key2.AddItem("Natural's Not In It");
			row2.Put("key", key2);
			row2.Put("value", 187.0);
			expectedRows.AddItem(row2);
			IDictionary<string, object> row3 = new Dictionary<string, object>();
			IList<string> key3 = new AList<string>();
			key3.AddItem("Gang Of Four");
			key3.AddItem("Entertainment!");
			key3.AddItem("Not Great Men");
			row3.Put("key", key3);
			row3.Put("value", 187.0);
			expectedRows.AddItem(row3);
			IDictionary<string, object> row4 = new Dictionary<string, object>();
			IList<string> key4 = new AList<string>();
			key4.AddItem("Gang Of Four");
			key4.AddItem("Songs Of The Free");
			key4.AddItem("I Love A Man In Uniform");
			row4.Put("key", key4);
			row4.Put("value", 248.0);
			expectedRows.AddItem(row4);
			IDictionary<string, object> row5 = new Dictionary<string, object>();
			IList<string> key5 = new AList<string>();
			key5.AddItem("PiL");
			key5.AddItem("Metal Box");
			key5.AddItem("Memories");
			row5.Put("key", key5);
			row5.Put("value", 309.0);
			expectedRows.AddItem(row5);
			NUnit.Framework.Assert.AreEqual(row1.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(row1.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(row2.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(row2.Get("value"), rows[1].GetValue());
			NUnit.Framework.Assert.AreEqual(row3.Get("key"), rows[2].GetKey());
			NUnit.Framework.Assert.AreEqual(row3.Get("value"), rows[2].GetValue());
			NUnit.Framework.Assert.AreEqual(row4.Get("key"), rows[3].GetKey());
			NUnit.Framework.Assert.AreEqual(row4.Get("value"), rows[3].GetValue());
			NUnit.Framework.Assert.AreEqual(row5.Get("key"), rows[4].GetKey());
			NUnit.Framework.Assert.AreEqual(row5.Get("value"), rows[4].GetValue());
			//group level 1
			options.SetGroupLevel(1);
			status = new Status();
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<IDictionary<string, object>>();
			row1 = new Dictionary<string, object>();
			key1 = new AList<string>();
			key1.AddItem("Gang Of Four");
			row1.Put("key", key1);
			row1.Put("value", 853.0);
			expectedRows.AddItem(row1);
			row2 = new Dictionary<string, object>();
			key2 = new AList<string>();
			key2.AddItem("PiL");
			row2.Put("key", key2);
			row2.Put("value", 309.0);
			expectedRows.AddItem(row2);
			NUnit.Framework.Assert.AreEqual(row1.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(row1.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(row2.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(row2.Get("value"), rows[1].GetValue());
			//group level 2
			options.SetGroupLevel(2);
			status = new Status();
			rows = view.QueryWithOptions(options);
			expectedRows = new AList<IDictionary<string, object>>();
			row1 = new Dictionary<string, object>();
			key1 = new AList<string>();
			key1.AddItem("Gang Of Four");
			key1.AddItem("Entertainment!");
			row1.Put("key", key1);
			row1.Put("value", 605.0);
			expectedRows.AddItem(row1);
			row2 = new Dictionary<string, object>();
			key2 = new AList<string>();
			key2.AddItem("Gang Of Four");
			key2.AddItem("Songs Of The Free");
			row2.Put("key", key2);
			row2.Put("value", 248.0);
			expectedRows.AddItem(row2);
			row3 = new Dictionary<string, object>();
			key3 = new AList<string>();
			key3.AddItem("PiL");
			key3.AddItem("Metal Box");
			row3.Put("key", key3);
			row3.Put("value", 309.0);
			expectedRows.AddItem(row3);
			NUnit.Framework.Assert.AreEqual(row1.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(row1.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(row2.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(row2.Get("value"), rows[1].GetValue());
			NUnit.Framework.Assert.AreEqual(row3.Get("key"), rows[2].GetKey());
			NUnit.Framework.Assert.AreEqual(row3.Get("value"), rows[2].GetValue());
		}

		private sealed class _Mapper_671 : Mapper
		{
			public _Mapper_671()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				IList<object> key = new AList<object>();
				key.AddItem(document.Get("artist"));
				key.AddItem(document.Get("album"));
				key.AddItem(document.Get("track"));
				emitter.Emit(key, document.Get("time"));
			}
		}

		private sealed class _Reducer_681 : Reducer
		{
			public _Reducer_681()
			{
			}

			public object Reduce(IList<object> keys, IList<object> values, bool rereduce)
			{
				return View.TotalValues(values);
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewGroupedStrings()
		{
			IDictionary<string, object> docProperties1 = new Dictionary<string, object>();
			docProperties1.Put("name", "Alice");
			PutDoc(database, docProperties1);
			IDictionary<string, object> docProperties2 = new Dictionary<string, object>();
			docProperties2.Put("name", "Albert");
			PutDoc(database, docProperties2);
			IDictionary<string, object> docProperties3 = new Dictionary<string, object>();
			docProperties3.Put("name", "Naomi");
			PutDoc(database, docProperties3);
			IDictionary<string, object> docProperties4 = new Dictionary<string, object>();
			docProperties4.Put("name", "Jens");
			PutDoc(database, docProperties4);
			IDictionary<string, object> docProperties5 = new Dictionary<string, object>();
			docProperties5.Put("name", "Jed");
			PutDoc(database, docProperties5);
			View view = database.GetView("default/names");
			view.SetMapReduce(new _Mapper_859(), new _Reducer_869(), "1.0");
			view.UpdateIndex();
			QueryOptions options = new QueryOptions();
			options.SetGroupLevel(1);
			IList<QueryRow> rows = view.QueryWithOptions(options);
			IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object
				>>();
			IDictionary<string, object> row1 = new Dictionary<string, object>();
			row1.Put("key", "A");
			row1.Put("value", 2);
			expectedRows.AddItem(row1);
			IDictionary<string, object> row2 = new Dictionary<string, object>();
			row2.Put("key", "J");
			row2.Put("value", 2);
			expectedRows.AddItem(row2);
			IDictionary<string, object> row3 = new Dictionary<string, object>();
			row3.Put("key", "N");
			row3.Put("value", 1);
			expectedRows.AddItem(row3);
			NUnit.Framework.Assert.AreEqual(row1.Get("key"), rows[0].GetKey());
			NUnit.Framework.Assert.AreEqual(row1.Get("value"), rows[0].GetValue());
			NUnit.Framework.Assert.AreEqual(row2.Get("key"), rows[1].GetKey());
			NUnit.Framework.Assert.AreEqual(row2.Get("value"), rows[1].GetValue());
			NUnit.Framework.Assert.AreEqual(row3.Get("key"), rows[2].GetKey());
			NUnit.Framework.Assert.AreEqual(row3.Get("value"), rows[2].GetValue());
		}

		private sealed class _Mapper_859 : Mapper
		{
			public _Mapper_859()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				string name = (string)document.Get("name");
				if (name != null)
				{
					emitter.Emit(Sharpen.Runtime.Substring(name, 0, 1), 1);
				}
			}
		}

		private sealed class _Reducer_869 : Reducer
		{
			public _Reducer_869()
			{
			}

			public object Reduce(IList<object> keys, IList<object> values, bool rereduce)
			{
				return values.Count;
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewCollation()
		{
			IList<object> list1 = new AList<object>();
			list1.AddItem("a");
			IList<object> list2 = new AList<object>();
			list2.AddItem("b");
			IList<object> list3 = new AList<object>();
			list3.AddItem("b");
			list3.AddItem("c");
			IList<object> list4 = new AList<object>();
			list4.AddItem("b");
			list4.AddItem("c");
			list4.AddItem("a");
			IList<object> list5 = new AList<object>();
			list5.AddItem("b");
			list5.AddItem("d");
			IList<object> list6 = new AList<object>();
			list6.AddItem("b");
			list6.AddItem("d");
			list6.AddItem("e");
			// Based on CouchDB's "view_collation.js" test
			IList<object> testKeys = new AList<object>();
			testKeys.AddItem(null);
			testKeys.AddItem(false);
			testKeys.AddItem(true);
			testKeys.AddItem(0);
			testKeys.AddItem(2.5);
			testKeys.AddItem(10);
			testKeys.AddItem(" ");
			testKeys.AddItem("_");
			testKeys.AddItem("~");
			testKeys.AddItem("a");
			testKeys.AddItem("A");
			testKeys.AddItem("aa");
			testKeys.AddItem("b");
			testKeys.AddItem("B");
			testKeys.AddItem("ba");
			testKeys.AddItem("bb");
			testKeys.AddItem(list1);
			testKeys.AddItem(list2);
			testKeys.AddItem(list3);
			testKeys.AddItem(list4);
			testKeys.AddItem(list5);
			testKeys.AddItem(list6);
			int i = 0;
			foreach (object key in testKeys)
			{
				IDictionary<string, object> docProperties = new Dictionary<string, object>();
				docProperties.Put("_id", Sharpen.Extensions.ToString(i++));
				docProperties.Put("name", key);
				PutDoc(database, docProperties);
			}
			View view = database.GetView("default/names");
			view.SetMapReduce(new _Mapper_970(), null, "1.0");
			QueryOptions options = new QueryOptions();
			IList<QueryRow> rows = view.QueryWithOptions(options);
			i = 0;
			foreach (QueryRow row in rows)
			{
				NUnit.Framework.Assert.AreEqual(testKeys[i++], row.GetKey());
			}
		}

		private sealed class _Mapper_970 : Mapper
		{
			public _Mapper_970()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("name"), null);
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewCollationRaw()
		{
			IList<object> list1 = new AList<object>();
			list1.AddItem("a");
			IList<object> list2 = new AList<object>();
			list2.AddItem("b");
			IList<object> list3 = new AList<object>();
			list3.AddItem("b");
			list3.AddItem("c");
			IList<object> list4 = new AList<object>();
			list4.AddItem("b");
			list4.AddItem("c");
			list4.AddItem("a");
			IList<object> list5 = new AList<object>();
			list5.AddItem("b");
			list5.AddItem("d");
			IList<object> list6 = new AList<object>();
			list6.AddItem("b");
			list6.AddItem("d");
			list6.AddItem("e");
			// Based on CouchDB's "view_collation.js" test
			IList<object> testKeys = new AList<object>();
			testKeys.AddItem(0);
			testKeys.AddItem(2.5);
			testKeys.AddItem(10);
			testKeys.AddItem(false);
			testKeys.AddItem(null);
			testKeys.AddItem(true);
			testKeys.AddItem(list1);
			testKeys.AddItem(list2);
			testKeys.AddItem(list3);
			testKeys.AddItem(list4);
			testKeys.AddItem(list5);
			testKeys.AddItem(list6);
			testKeys.AddItem(" ");
			testKeys.AddItem("A");
			testKeys.AddItem("B");
			testKeys.AddItem("_");
			testKeys.AddItem("a");
			testKeys.AddItem("aa");
			testKeys.AddItem("b");
			testKeys.AddItem("ba");
			testKeys.AddItem("bb");
			testKeys.AddItem("~");
			int i = 0;
			foreach (object key in testKeys)
			{
				IDictionary<string, object> docProperties = new Dictionary<string, object>();
				docProperties.Put("_id", Sharpen.Extensions.ToString(i++));
				docProperties.Put("name", key);
				PutDoc(database, docProperties);
			}
			View view = database.GetView("default/names");
			view.SetMapReduce(new _Mapper_1048(), null, "1.0");
			view.SetCollation(View.TDViewCollation.TDViewCollationRaw);
			QueryOptions options = new QueryOptions();
			IList<QueryRow> rows = view.QueryWithOptions(options);
			i = 0;
			foreach (QueryRow row in rows)
			{
				NUnit.Framework.Assert.AreEqual(testKeys[i++], row.GetKey());
			}
			database.Close();
		}

		private sealed class _Mapper_1048 : Mapper
		{
			public _Mapper_1048()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("name"), null);
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestLargerViewQuery()
		{
			PutNDocs(database, 4);
			View view = CreateView(database);
			view.UpdateIndex();
			// Query all rows:
			QueryOptions options = new QueryOptions();
			Status status = new Status();
			IList<QueryRow> rows = view.QueryWithOptions(options);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestViewLinkedDocs()
		{
			PutLinkedDocs(database);
			View view = database.GetView("linked");
			view.SetMapReduce(new _Mapper_1086(), null, "1");
			view.UpdateIndex();
			QueryOptions options = new QueryOptions();
			options.SetIncludeDocs(true);
			// required for linked documents
			IList<QueryRow> rows = view.QueryWithOptions(options);
			NUnit.Framework.Assert.IsNotNull(rows);
			NUnit.Framework.Assert.AreEqual(5, rows.Count);
			object[][] expected = new object[][] { new object[] { "22222", "hello", 0, null, 
				"22222" }, new object[] { "22222", "hello", 1, "11111", "11111" }, new object[] 
				{ "33333", "world", 0, null, "33333" }, new object[] { "33333", "world", 1, "22222"
				, "22222" }, new object[] { "33333", "world", 2, "11111", "11111" } };
			for (int i = 0; i < rows.Count; i++)
			{
				QueryRow row = rows[i];
				IDictionary<string, object> rowAsJson = row.AsJSONDictionary();
				Log.D(Tag, string.Empty + rowAsJson);
				IList<object> key = (IList<object>)rowAsJson.Get("key");
				IDictionary<string, object> doc = (IDictionary<string, object>)rowAsJson.Get("doc"
					);
				string id = (string)rowAsJson.Get("id");
				NUnit.Framework.Assert.AreEqual(expected[i][0], id);
				NUnit.Framework.Assert.AreEqual(2, key.Count);
				NUnit.Framework.Assert.AreEqual(expected[i][1], key[0]);
				NUnit.Framework.Assert.AreEqual(expected[i][2], key[1]);
				if (expected[i][3] == null)
				{
					NUnit.Framework.Assert.IsNull(row.GetValue());
				}
				else
				{
					NUnit.Framework.Assert.AreEqual(expected[i][3], ((IDictionary<string, object>)row
						.GetValue()).Get("_id"));
				}
				NUnit.Framework.Assert.AreEqual(expected[i][4], doc.Get("_id"));
			}
		}

		private sealed class _Mapper_1086 : Mapper
		{
			public _Mapper_1086()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				if (document.ContainsKey("value"))
				{
					emitter.Emit(new object[] { document.Get("value"), 0 }, null);
				}
				if (document.ContainsKey("ancestors"))
				{
					IList<object> ancestors = (IList<object>)document.Get("ancestors");
					for (int i = 0; i < ancestors.Count; i++)
					{
						IDictionary<string, object> value = new Dictionary<string, object>();
						value.Put("_id", ancestors[i]);
						emitter.Emit(new object[] { document.Get("value"), i + 1 }, value);
					}
				}
			}
		}
	}
}
