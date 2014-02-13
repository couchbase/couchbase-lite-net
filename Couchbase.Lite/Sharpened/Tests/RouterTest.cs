//
// RouterTest.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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

using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Router;
using Couchbase.Lite.Util;
using Org.Apache.Commons.IO;
using Sharpen;

namespace Couchbase.Lite
{
	public class RouterTest : LiteTestCase
	{
		public const string Tag = "Router";

		public virtual void TestServer()
		{
			IDictionary<string, object> responseBody = new Dictionary<string, object>();
			responseBody.Put("CBLite", "Welcome");
			responseBody.Put("couchdb", "Welcome");
			responseBody.Put("version", Couchbase.Lite.Router.Router.GetVersionString());
			Send("GET", "/", Status.Ok, responseBody);
			IDictionary<string, object> session = new Dictionary<string, object>();
			IDictionary<string, object> userCtx = new Dictionary<string, object>();
			IList<string> roles = new AList<string>();
			roles.AddItem("_admin");
			session.Put("ok", true);
			userCtx.Put("name", null);
			userCtx.Put("roles", roles);
			session.Put("userCtx", userCtx);
			Send("GET", "/_session", Status.Ok, session);
			IList<string> allDbs = new AList<string>();
			allDbs.AddItem("cblite-test");
			Send("GET", "/_all_dbs", Status.Ok, allDbs);
			Send("GET", "/non-existant", Status.NotFound, null);
			Send("GET", "/BadName", Status.BadRequest, null);
			Send("PUT", "/", Status.BadRequest, null);
			Send("POST", "/", Status.BadRequest, null);
		}

		public virtual void TestDatabase()
		{
			Send("PUT", "/database", Status.Created, null);
			IDictionary<string, object> dbInfo = (IDictionary<string, object>)Send("GET", "/database"
				, Status.Ok, null);
			NUnit.Framework.Assert.AreEqual(0, dbInfo.Get("doc_count"));
			NUnit.Framework.Assert.AreEqual(0, dbInfo.Get("update_seq"));
			NUnit.Framework.Assert.IsTrue((int)dbInfo.Get("disk_size") > 8000);
			Send("PUT", "/database", Status.PreconditionFailed, null);
			Send("PUT", "/database2", Status.Created, null);
			IList<string> allDbs = new AList<string>();
			allDbs.AddItem("cblite-test");
			allDbs.AddItem("database");
			allDbs.AddItem("database2");
			Send("GET", "/_all_dbs", Status.Ok, allDbs);
			dbInfo = (IDictionary<string, object>)Send("GET", "/database2", Status.Ok, null);
			NUnit.Framework.Assert.AreEqual("database2", dbInfo.Get("db_name"));
			Send("DELETE", "/database2", Status.Ok, null);
			allDbs.Remove("database2");
			Send("GET", "/_all_dbs", Status.Ok, allDbs);
			Send("PUT", "/database%2Fwith%2Fslashes", Status.Created, null);
			dbInfo = (IDictionary<string, object>)Send("GET", "/database%2Fwith%2Fslashes", Status
				.Ok, null);
			NUnit.Framework.Assert.AreEqual("database/with/slashes", dbInfo.Get("db_name"));
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestDocWithAttachment()
		{
			string inlineTextString = "Inline text string created by cblite functional test";
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> attachment = new Dictionary<string, object>();
			attachment.Put("content_type", "text/plain");
			attachment.Put("data", "SW5saW5lIHRleHQgc3RyaW5nIGNyZWF0ZWQgYnkgY2JsaXRlIGZ1bmN0aW9uYWwgdGVzdA=="
				);
			IDictionary<string, object> attachments = new Dictionary<string, object>();
			attachments.Put("inline.txt", attachment);
			IDictionary<string, object> docWithAttachment = new Dictionary<string, object>();
			docWithAttachment.Put("_id", "docWithAttachment");
			docWithAttachment.Put("text", inlineTextString);
			docWithAttachment.Put("_attachments", attachments);
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("PUT", 
				"/db/docWithAttachment", docWithAttachment, Status.Created, null);
			result = (IDictionary<string, object>)Send("GET", "/db/docWithAttachment", Status
				.Ok, null);
			IDictionary<string, object> attachmentsResult = (IDictionary<string, object>)result
				.Get("_attachments");
			IDictionary<string, object> attachmentResult = (IDictionary<string, object>)attachmentsResult
				.Get("inline.txt");
			// there should be either a content_type or content-type field.
			//https://github.com/couchbase/couchbase-lite-android-core/issues/12
			//content_type becomes null for attachments in responses, should be as set in Content-Type
			string contentTypeField = (string)attachmentResult.Get("content_type");
			NUnit.Framework.Assert.IsTrue(attachmentResult.ContainsKey("content_type"));
			NUnit.Framework.Assert.IsNotNull(contentTypeField);
			URLConnection conn = SendRequest("GET", "/db/docWithAttachment/inline.txt", null, 
				null);
			string contentType = conn.GetHeaderField("Content-Type");
			NUnit.Framework.Assert.IsNotNull(contentType);
			NUnit.Framework.Assert.IsTrue(contentType.Contains("text/plain"));
			StringWriter writer = new StringWriter();
			IOUtils.Copy(conn.GetInputStream(), writer, "UTF-8");
			string responseString = writer.ToString();
			NUnit.Framework.Assert.IsTrue(responseString.Contains(inlineTextString));
		}

		private IDictionary<string, object> ValueMapWithRev(string revId)
		{
			IDictionary<string, object> value = ValueMapWithRevNoConflictArray(revId);
			value.Put("_conflicts", new AList<string>());
			return value;
		}

		private IDictionary<string, object> ValueMapWithRevNoConflictArray(string revId)
		{
			IDictionary<string, object> value = new Dictionary<string, object>();
			value.Put("rev", revId);
			return value;
		}

		public virtual void TestDocs()
		{
			Send("PUT", "/db", Status.Created, null);
			// PUT:
			IDictionary<string, object> doc1 = new Dictionary<string, object>();
			doc1.Put("message", "hello");
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("PUT", 
				"/db/doc1", doc1, Status.Created, null);
			string revID = (string)result.Get("rev");
			NUnit.Framework.Assert.IsTrue(revID.StartsWith("1-"));
			// PUT to update:
			doc1.Put("message", "goodbye");
			doc1.Put("_rev", revID);
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc1", doc1, Status.Created
				, null);
			Log.V(Tag, string.Format("PUT returned %s", result));
			revID = (string)result.Get("rev");
			NUnit.Framework.Assert.IsTrue(revID.StartsWith("2-"));
			doc1.Put("_id", "doc1");
			doc1.Put("_rev", revID);
			result = (IDictionary<string, object>)Send("GET", "/db/doc1", Status.Ok, doc1);
			// Add more docs:
			IDictionary<string, object> docX = new Dictionary<string, object>();
			docX.Put("message", "hello");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc3", docX, Status.Created
				, null);
			string revID3 = (string)result.Get("rev");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc2", docX, Status.Created
				, null);
			string revID2 = (string)result.Get("rev");
			// _all_docs:
			result = (IDictionary<string, object>)Send("GET", "/db/_all_docs", Status.Ok, null
				);
			NUnit.Framework.Assert.AreEqual(3, result.Get("total_rows"));
			NUnit.Framework.Assert.AreEqual(0, result.Get("offset"));
			IDictionary<string, object> value1 = ValueMapWithRev(revID);
			IDictionary<string, object> value2 = ValueMapWithRev(revID2);
			IDictionary<string, object> value3 = ValueMapWithRev(revID3);
			IDictionary<string, object> row1 = new Dictionary<string, object>();
			row1.Put("id", "doc1");
			row1.Put("key", "doc1");
			row1.Put("value", value1);
			IDictionary<string, object> row2 = new Dictionary<string, object>();
			row2.Put("id", "doc2");
			row2.Put("key", "doc2");
			row2.Put("value", value2);
			IDictionary<string, object> row3 = new Dictionary<string, object>();
			row3.Put("id", "doc3");
			row3.Put("key", "doc3");
			row3.Put("value", value3);
			IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object
				>>();
			expectedRows.AddItem(row1);
			expectedRows.AddItem(row2);
			expectedRows.AddItem(row3);
			IList<IDictionary<string, object>> rows = (IList<IDictionary<string, object>>)result
				.Get("rows");
			NUnit.Framework.Assert.AreEqual(expectedRows, rows);
			// DELETE:
			result = (IDictionary<string, object>)Send("DELETE", string.Format("/db/doc1?rev=%s"
				, revID), Status.Ok, null);
			revID = (string)result.Get("rev");
			NUnit.Framework.Assert.IsTrue(revID.StartsWith("3-"));
			Send("GET", "/db/doc1", Status.NotFound, null);
			// _changes:
			IList<object> changes1 = new AList<object>();
			changes1.AddItem(ValueMapWithRevNoConflictArray(revID));
			IList<object> changes2 = new AList<object>();
			changes2.AddItem(ValueMapWithRevNoConflictArray(revID2));
			IList<object> changes3 = new AList<object>();
			changes3.AddItem(ValueMapWithRevNoConflictArray(revID3));
			IDictionary<string, object> result1 = new Dictionary<string, object>();
			result1.Put("id", "doc1");
			result1.Put("seq", 5);
			result1.Put("deleted", true);
			result1.Put("changes", changes1);
			IDictionary<string, object> result2 = new Dictionary<string, object>();
			result2.Put("id", "doc2");
			result2.Put("seq", 4);
			result2.Put("changes", changes2);
			IDictionary<string, object> result3 = new Dictionary<string, object>();
			result3.Put("id", "doc3");
			result3.Put("seq", 3);
			result3.Put("changes", changes3);
			IList<object> results = new AList<object>();
			results.AddItem(result3);
			results.AddItem(result2);
			results.AddItem(result1);
			IDictionary<string, object> expectedChanges = new Dictionary<string, object>();
			expectedChanges.Put("last_seq", 5);
			expectedChanges.Put("results", results);
			Send("GET", "/db/_changes", Status.Ok, expectedChanges);
			// _changes with ?since:
			results.Remove(result3);
			results.Remove(result2);
			expectedChanges.Put("results", results);
			Send("GET", "/db/_changes?since=4", Status.Ok, expectedChanges);
			results.Remove(result1);
			expectedChanges.Put("results", results);
			Send("GET", "/db/_changes?since=5", Status.Ok, expectedChanges);
			// Put with _deleted to delete a doc:
			Log.D(Tag, "Put with _deleted to delete a doc");
			Send("GET", "/db/doc5", Status.NotFound, null);
			IDictionary<string, object> doc5 = new Dictionary<string, object>();
			doc5.Put("message", "hello5");
			IDictionary<string, object> resultDoc5 = (IDictionary<string, object>)SendBody("PUT"
				, "/db/doc5", doc5, Status.Created, null);
			string revIdDoc5 = (string)resultDoc5.Get("rev");
			NUnit.Framework.Assert.IsTrue(revIdDoc5.StartsWith("1-"));
			doc5.Put("_deleted", true);
			doc5.Put("_rev", revIdDoc5);
			doc5.Put("_id", "doc5");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc5", doc5, Status.Ok
				, null);
			Send("GET", "/db/doc5", Status.NotFound, null);
			Log.D(Tag, "Finished put with _deleted to delete a doc");
		}

		public virtual void TestLocalDocs()
		{
			Send("PUT", "/db", Status.Created, null);
			// PUT a local doc:
			IDictionary<string, object> doc1 = new Dictionary<string, object>();
			doc1.Put("message", "hello");
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("PUT", 
				"/db/_local/doc1", doc1, Status.Created, null);
			string revID = (string)result.Get("rev");
			NUnit.Framework.Assert.IsTrue(revID.StartsWith("1-"));
			// GET it:
			doc1.Put("_id", "_local/doc1");
			doc1.Put("_rev", revID);
			result = (IDictionary<string, object>)Send("GET", "/db/_local/doc1", Status.Ok, doc1
				);
			// Local doc should not appear in _changes feed:
			IDictionary<string, object> expectedChanges = new Dictionary<string, object>();
			expectedChanges.Put("last_seq", 0);
			expectedChanges.Put("results", new AList<object>());
			Send("GET", "/db/_changes", Status.Ok, expectedChanges);
		}

		public virtual void TestAllDocs()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> result;
			IDictionary<string, object> doc1 = new Dictionary<string, object>();
			doc1.Put("message", "hello");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc1", doc1, Status.Created
				, null);
			string revID = (string)result.Get("rev");
			IDictionary<string, object> doc3 = new Dictionary<string, object>();
			doc3.Put("message", "bonjour");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc3", doc3, Status.Created
				, null);
			string revID3 = (string)result.Get("rev");
			IDictionary<string, object> doc2 = new Dictionary<string, object>();
			doc2.Put("message", "guten tag");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc2", doc2, Status.Created
				, null);
			string revID2 = (string)result.Get("rev");
			// _all_docs:
			result = (IDictionary<string, object>)Send("GET", "/db/_all_docs", Status.Ok, null
				);
			NUnit.Framework.Assert.AreEqual(3, result.Get("total_rows"));
			NUnit.Framework.Assert.AreEqual(0, result.Get("offset"));
			IDictionary<string, object> value1 = ValueMapWithRev(revID);
			IDictionary<string, object> value2 = ValueMapWithRev(revID2);
			IDictionary<string, object> value3 = ValueMapWithRev(revID3);
			IDictionary<string, object> row1 = new Dictionary<string, object>();
			row1.Put("id", "doc1");
			row1.Put("key", "doc1");
			row1.Put("value", value1);
			IDictionary<string, object> row2 = new Dictionary<string, object>();
			row2.Put("id", "doc2");
			row2.Put("key", "doc2");
			row2.Put("value", value2);
			IDictionary<string, object> row3 = new Dictionary<string, object>();
			row3.Put("id", "doc3");
			row3.Put("key", "doc3");
			row3.Put("value", value3);
			IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object
				>>();
			expectedRows.AddItem(row1);
			expectedRows.AddItem(row2);
			expectedRows.AddItem(row3);
			IList<IDictionary<string, object>> rows = (IList<IDictionary<string, object>>)result
				.Get("rows");
			NUnit.Framework.Assert.AreEqual(expectedRows, rows);
			// ?include_docs:
			result = (IDictionary<string, object>)Send("GET", "/db/_all_docs?include_docs=true"
				, Status.Ok, null);
			NUnit.Framework.Assert.AreEqual(3, result.Get("total_rows"));
			NUnit.Framework.Assert.AreEqual(0, result.Get("offset"));
			doc1.Put("_id", "doc1");
			doc1.Put("_rev", revID);
			row1.Put("doc", doc1);
			doc2.Put("_id", "doc2");
			doc2.Put("_rev", revID2);
			row2.Put("doc", doc2);
			doc3.Put("_id", "doc3");
			doc3.Put("_rev", revID3);
			row3.Put("doc", doc3);
			IList<IDictionary<string, object>> expectedRowsWithDocs = new AList<IDictionary<string
				, object>>();
			expectedRowsWithDocs.AddItem(row1);
			expectedRowsWithDocs.AddItem(row2);
			expectedRowsWithDocs.AddItem(row3);
			rows = (IList<IDictionary<string, object>>)result.Get("rows");
			NUnit.Framework.Assert.AreEqual(expectedRowsWithDocs, rows);
		}

		public virtual void TestViews()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> result;
			IDictionary<string, object> doc1 = new Dictionary<string, object>();
			doc1.Put("message", "hello");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc1", doc1, Status.Created
				, null);
			string revID = (string)result.Get("rev");
			IDictionary<string, object> doc3 = new Dictionary<string, object>();
			doc3.Put("message", "bonjour");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc3", doc3, Status.Created
				, null);
			string revID3 = (string)result.Get("rev");
			IDictionary<string, object> doc2 = new Dictionary<string, object>();
			doc2.Put("message", "guten tag");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc2", doc2, Status.Created
				, null);
			string revID2 = (string)result.Get("rev");
			Database db = manager.GetDatabase("db");
			View view = db.GetView("design/view");
			view.SetMapAndReduce(new _Mapper_372(), null, "1");
			// Build up our expected result
			IDictionary<string, object> row1 = new Dictionary<string, object>();
			row1.Put("id", "doc1");
			row1.Put("key", "hello");
			IDictionary<string, object> row2 = new Dictionary<string, object>();
			row2.Put("id", "doc2");
			row2.Put("key", "guten tag");
			IDictionary<string, object> row3 = new Dictionary<string, object>();
			row3.Put("id", "doc3");
			row3.Put("key", "bonjour");
			IList<IDictionary<string, object>> expectedRows = new AList<IDictionary<string, object
				>>();
			expectedRows.AddItem(row3);
			expectedRows.AddItem(row2);
			expectedRows.AddItem(row1);
			IDictionary<string, object> expectedResult = new Dictionary<string, object>();
			expectedResult.Put("offset", 0);
			expectedResult.Put("total_rows", 3);
			expectedResult.Put("rows", expectedRows);
			// Query the view and check the result:
			Send("GET", "/db/_design/design/_view/view", Status.Ok, expectedResult);
			// Check the ETag:
			URLConnection conn = SendRequest("GET", "/db/_design/design/_view/view", null, null
				);
			string etag = conn.GetHeaderField("Etag");
			NUnit.Framework.Assert.AreEqual(string.Format("\"%d\"", view.GetLastSequenceIndexed
				()), etag);
			// Try a conditional GET:
			IDictionary<string, string> headers = new Dictionary<string, string>();
			headers.Put("If-None-Match", etag);
			conn = SendRequest("GET", "/db/_design/design/_view/view", headers, null);
			NUnit.Framework.Assert.AreEqual(Status.NotModified, conn.GetResponseCode());
			// Update the database:
			IDictionary<string, object> doc4 = new Dictionary<string, object>();
			doc4.Put("message", "aloha");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/doc4", doc4, Status.Created
				, null);
			// Try a conditional GET:
			conn = SendRequest("GET", "/db/_design/design/_view/view", headers, null);
			NUnit.Framework.Assert.AreEqual(Status.Ok, conn.GetResponseCode());
			result = (IDictionary<string, object>)ParseJSONResponse(conn);
			NUnit.Framework.Assert.AreEqual(4, result.Get("total_rows"));
		}

		private sealed class _Mapper_372 : Mapper
		{
			public _Mapper_372()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("message"), null);
			}
		}

		public virtual void TestPostBulkDocs()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> bulk_doc1 = new Dictionary<string, object>();
			bulk_doc1.Put("_id", "bulk_message1");
			bulk_doc1.Put("baz", "hello");
			IDictionary<string, object> bulk_doc2 = new Dictionary<string, object>();
			bulk_doc2.Put("_id", "bulk_message2");
			bulk_doc2.Put("baz", "hi");
			IList<IDictionary<string, object>> list = new AList<IDictionary<string, object>>(
				);
			list.AddItem(bulk_doc1);
			list.AddItem(bulk_doc2);
			IDictionary<string, object> bodyObj = new Dictionary<string, object>();
			bodyObj.Put("docs", list);
			IList<IDictionary<string, object>> bulk_result = (AList<IDictionary<string, object
				>>)SendBody("POST", "/db/_bulk_docs", bodyObj, Status.Created, null);
			NUnit.Framework.Assert.AreEqual(2, bulk_result.Count);
			NUnit.Framework.Assert.AreEqual(bulk_result[0].Get("id"), bulk_doc1.Get("_id"));
			NUnit.Framework.Assert.IsNotNull(bulk_result[0].Get("rev"));
			NUnit.Framework.Assert.AreEqual(bulk_result[1].Get("id"), bulk_doc2.Get("_id"));
			NUnit.Framework.Assert.IsNotNull(bulk_result[1].Get("rev"));
		}

		public virtual void TestPostKeysView()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> result;
			Database db = manager.GetDatabase("db");
			View view = db.GetView("design/view");
			view.SetMapAndReduce(new _Mapper_463(), null, "1");
			IDictionary<string, object> key_doc1 = new Dictionary<string, object>();
			key_doc1.Put("parentId", "12345");
			result = (IDictionary<string, object>)SendBody("PUT", "/db/key_doc1", key_doc1, Status
				.Created, null);
			view = db.GetView("design/view");
			view.SetMapAndReduce(new _Mapper_475(), null, "1");
			IList<object> keys = new AList<object>();
			keys.AddItem("12345");
			IDictionary<string, object> bodyObj = new Dictionary<string, object>();
			bodyObj.Put("keys", keys);
			URLConnection conn = SendRequest("POST", "/db/_design/design/_view/view", null, bodyObj
				);
			result = (IDictionary<string, object>)ParseJSONResponse(conn);
			NUnit.Framework.Assert.AreEqual(1, result.Get("total_rows"));
		}

		private sealed class _Mapper_463 : Mapper
		{
			public _Mapper_463()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				emitter.Emit(document.Get("message"), null);
			}
		}

		private sealed class _Mapper_475 : Mapper
		{
			public _Mapper_475()
			{
			}

			public void Map(IDictionary<string, object> document, Emitter emitter)
			{
				if (document.Get("parentId").Equals("12345"))
				{
					emitter.Emit(document.Get("parentId"), document);
				}
			}
		}

		public virtual void TestRevsDiff()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> doc = new Dictionary<string, object>();
			IDictionary<string, object> doc1r1 = (IDictionary<string, object>)SendBody("PUT", 
				"/db/11111", doc, Status.Created, null);
			string doc1r1ID = (string)doc1r1.Get("rev");
			IDictionary<string, object> doc2r1 = (IDictionary<string, object>)SendBody("PUT", 
				"/db/22222", doc, Status.Created, null);
			string doc2r1ID = (string)doc2r1.Get("rev");
			IDictionary<string, object> doc3r1 = (IDictionary<string, object>)SendBody("PUT", 
				"/db/33333", doc, Status.Created, null);
			string doc3r1ID = (string)doc3r1.Get("rev");
			IDictionary<string, object> doc1v2 = new Dictionary<string, object>();
			doc1v2.Put("_rev", doc1r1ID);
			IDictionary<string, object> doc1r2 = (IDictionary<string, object>)SendBody("PUT", 
				"/db/11111", doc1v2, Status.Created, null);
			string doc1r2ID = (string)doc1r2.Get("rev");
			IDictionary<string, object> doc2v2 = new Dictionary<string, object>();
			doc2v2.Put("_rev", doc2r1ID);
			SendBody("PUT", "/db/22222", doc2v2, Status.Created, null);
			IDictionary<string, object> doc1v3 = new Dictionary<string, object>();
			doc1v3.Put("_rev", doc1r2ID);
			IDictionary<string, object> doc1r3 = (IDictionary<string, object>)SendBody("PUT", 
				"/db/11111", doc1v3, Status.Created, null);
			string doc1r3ID = (string)doc1r1.Get("rev");
			//now build up the request
			IList<string> doc1Revs = new AList<string>();
			doc1Revs.AddItem(doc1r2ID);
			doc1Revs.AddItem("3-foo");
			IList<string> doc2Revs = new AList<string>();
			doc2Revs.AddItem(doc2r1ID);
			IList<string> doc3Revs = new AList<string>();
			doc3Revs.AddItem("10-bar");
			IList<string> doc9Revs = new AList<string>();
			doc9Revs.AddItem("6-six");
			IDictionary<string, object> revsDiffRequest = new Dictionary<string, object>();
			revsDiffRequest.Put("11111", doc1Revs);
			revsDiffRequest.Put("22222", doc2Revs);
			revsDiffRequest.Put("33333", doc3Revs);
			revsDiffRequest.Put("99999", doc9Revs);
			//now build up the expected response
			IList<string> doc1missing = new AList<string>();
			doc1missing.AddItem("3-foo");
			IList<string> doc3missing = new AList<string>();
			doc3missing.AddItem("10-bar");
			IList<string> doc9missing = new AList<string>();
			doc9missing.AddItem("6-six");
			IDictionary<string, object> doc1missingMap = new Dictionary<string, object>();
			doc1missingMap.Put("missing", doc1missing);
			IDictionary<string, object> doc3missingMap = new Dictionary<string, object>();
			doc3missingMap.Put("missing", doc3missing);
			IDictionary<string, object> doc9missingMap = new Dictionary<string, object>();
			doc9missingMap.Put("missing", doc9missing);
			IDictionary<string, object> revsDiffResponse = new Dictionary<string, object>();
			revsDiffResponse.Put("11111", doc1missingMap);
			revsDiffResponse.Put("33333", doc3missingMap);
			revsDiffResponse.Put("99999", doc9missingMap);
			SendBody("POST", "/db/_revs_diff", revsDiffRequest, Status.Ok, revsDiffResponse);
		}

		public virtual void TestFacebookToken()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> doc1 = new Dictionary<string, object>();
			doc1.Put("email", "foo@bar.com");
			doc1.Put("remote_url", GetReplicationURL().ToExternalForm());
			doc1.Put("access_token", "fake_access_token");
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("POST"
				, "/_facebook_token", doc1, Status.Ok, null);
			Log.V(Tag, string.Format("result %s", result));
		}

		public virtual void TestPersonaAssertion()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> doc1 = new Dictionary<string, object>();
			string sampleAssertion = "eyJhbGciOiJSUzI1NiJ9.eyJwdWJsaWMta2V5Ijp7ImFsZ29yaXRobSI6IkRTIiwieSI6ImNhNWJiYTYzZmI4MDQ2OGE0MjFjZjgxYTIzN2VlMDcwYTJlOTM4NTY0ODhiYTYzNTM0ZTU4NzJjZjllMGUwMDk0ZWQ2NDBlOGNhYmEwMjNkYjc5ODU3YjkxMzBlZGNmZGZiNmJiNTUwMWNjNTk3MTI1Y2NiMWQ1ZWQzOTVjZTMyNThlYjEwN2FjZTM1ODRiOWIwN2I4MWU5MDQ4NzhhYzBhMjFlOWZkYmRjYzNhNzNjOTg3MDAwYjk4YWUwMmZmMDQ4ODFiZDNiOTBmNzllYzVlNDU1YzliZjM3NzFkYjEzMTcxYjNkMTA2ZjM1ZDQyZmZmZjQ2ZWZiZDcwNjgyNWQiLCJwIjoiZmY2MDA0ODNkYjZhYmZjNWI0NWVhYjc4NTk0YjM1MzNkNTUwZDlmMWJmMmE5OTJhN2E4ZGFhNmRjMzRmODA0NWFkNGU2ZTBjNDI5ZDMzNGVlZWFhZWZkN2UyM2Q0ODEwYmUwMGU0Y2MxNDkyY2JhMzI1YmE4MWZmMmQ1YTViMzA1YThkMTdlYjNiZjRhMDZhMzQ5ZDM5MmUwMGQzMjk3NDRhNTE3OTM4MDM0NGU4MmExOGM0NzkzMzQzOGY4OTFlMjJhZWVmODEyZDY5YzhmNzVlMzI2Y2I3MGVhMDAwYzNmNzc2ZGZkYmQ2MDQ2MzhjMmVmNzE3ZmMyNmQwMmUxNyIsInEiOiJlMjFlMDRmOTExZDFlZDc5OTEwMDhlY2FhYjNiZjc3NTk4NDMwOWMzIiwiZyI6ImM1MmE0YTBmZjNiN2U2MWZkZjE4NjdjZTg0MTM4MzY5YTYxNTRmNGFmYTkyOTY2ZTNjODI3ZTI1Y2ZhNmNmNTA4YjkwZTVkZTQxOWUxMzM3ZTA3YTJlOWUyYTNjZDVkZWE3MDRkMTc1ZjhlYmY2YWYzOTdkNjllMTEwYjk2YWZiMTdjN2EwMzI1OTMyOWU0ODI5YjBkMDNiYmM3ODk2YjE1YjRhZGU1M2UxMzA4NThjYzM0ZDk2MjY5YWE4OTA0MWY0MDkxMzZjNzI0MmEzODg5NWM5ZDViY2NhZDRmMzg5YWYxZDdhNGJkMTM5OGJkMDcyZGZmYTg5NjIzMzM5N2EifSwicHJpbmNpcGFsIjp7ImVtYWlsIjoiamVuc0Btb29zZXlhcmQuY29tIn0sImlhdCI6MTM1ODI5NjIzNzU3NywiZXhwIjoxMzU4MzgyNjM3NTc3LCJpc3MiOiJsb2dpbi5wZXJzb25hLm9yZyJ9.RnDK118nqL2wzpLCVRzw1MI4IThgeWpul9jPl6ypyyxRMMTurlJbjFfs-BXoPaOem878G8-4D2eGWS6wd307k7xlPysevYPogfFWxK_eDHwkTq3Ts91qEDqrdV_JtgULC8c1LvX65E0TwW_GL_TM94g3CvqoQnGVxxoaMVye4ggvR7eOZjimWMzUuu4Lo9Z-VBHBj7XM0UMBie57CpGwH4_Wkv0V_LHZRRHKdnl9ISp_aGwfBObTcHG9v0P3BW9vRrCjihIn0SqOJQ9obl52rMf84GD4Lcy9NIktzfyka70xR9Sh7ALotW7rWywsTzMTu3t8AzMz2MJgGjvQmx49QA~eyJhbGciOiJEUzEyOCJ9.eyJleHAiOjEzNTgyOTY0Mzg0OTUsImF1ZCI6Imh0dHA6Ly9sb2NhbGhvc3Q6NDk4NC8ifQ.4FV2TrUQffDya0MOxOQlzJQbDNvCPF2sfTIJN7KOLvvlSFPknuIo5g";
			doc1.Put("assertion", sampleAssertion);
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("POST"
				, "/_persona_assertion", doc1, Status.Ok, null);
			Log.V(Tag, string.Format("result %s", result));
			string email = (string)result.Get("email");
			NUnit.Framework.Assert.AreEqual(email, "jens@mooseyard.com");
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPushReplicate()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> replicateJsonMap = GetPushReplicationParsedJson();
			Log.V(Tag, "map: " + replicateJsonMap);
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("POST"
				, "/_replicate", replicateJsonMap, Status.Ok, null);
			Log.V(Tag, "result: " + result);
			NUnit.Framework.Assert.IsNotNull(result.Get("session_id"));
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestPullReplicate()
		{
			Send("PUT", "/db", Status.Created, null);
			IDictionary<string, object> replicateJsonMap = GetPullReplicationParsedJson();
			Log.V(Tag, "map: " + replicateJsonMap);
			IDictionary<string, object> result = (IDictionary<string, object>)SendBody("POST"
				, "/_replicate", replicateJsonMap, Status.Ok, null);
			Log.V(Tag, "result: " + result);
			NUnit.Framework.Assert.IsNotNull(result.Get("session_id"));
		}
	}
}
