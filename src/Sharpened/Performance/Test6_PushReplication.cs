// 
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
//using System;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Org.Apache.Commons.IO;
using Org.Apache.Commons.IO.Output;
using Sharpen;

namespace Couchbase.Lite.Performance
{
	public class Test6_PushReplication : LiteTestCase
	{
		public const string Tag = "PushReplicationPerformance";

		/// <exception cref="System.Exception"></exception>
		protected override void SetUp()
		{
			Log.V(Tag, "DeleteDBPerformance setUp");
			base.SetUp();
			string docIdTimestamp = System.Convert.ToString(Runtime.CurrentTimeMillis());
			for (int i = 0; i < GetNumberOfDocuments(); i++)
			{
				string docId = string.Format("doc%d-%s", i, docIdTimestamp);
				try
				{
					AddDocWithId(docId, "attachment.png", false);
				}
				catch (IOException ioex)
				{
					//addDocWithId(docId, null, false);
					Log.E(Tag, "Add document directly to sync gateway failed", ioex);
					Fail();
				}
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestPushReplicationPerformance()
		{
			long startMillis = Runtime.CurrentTimeMillis();
			Uri remote = GetReplicationURL();
			Replication repl = database.CreatePushReplication(remote);
			repl.SetContinuous(false);
			if (!IsSyncGateway(remote))
			{
				repl.SetCreateTarget(true);
				NUnit.Framework.Assert.IsTrue(repl.ShouldCreateTarget());
			}
			RunReplication(repl);
			Log.D(Tag, "testPusher() finished");
			Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
				() - startMillis).ToString() + "," + GetNumberOfDocuments());
		}

		private bool IsSyncGateway(Uri remote)
		{
			return (remote.Port == 4984 || remote.Port == 4984);
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		private void AddDocWithId(string docId, string attachmentName, bool gzipped)
		{
			string docJson;
			IDictionary<string, object> documentProperties = new Dictionary<string, object>();
			if (attachmentName == null)
			{
				documentProperties.Put("foo", 1);
				documentProperties.Put("bar", false);
				Document doc = database.GetDocument(docId);
				doc.PutProperties(documentProperties);
			}
			else
			{
				// add attachment to document
				InputStream attachmentStream = GetAsset(attachmentName);
				ByteArrayOutputStream baos = new ByteArrayOutputStream();
				IOUtils.Copy(attachmentStream, baos);
				if (gzipped == false)
				{
					string attachmentBase64 = Base64.EncodeBytes(baos.ToByteArray());
					documentProperties.Put("foo", 1);
					documentProperties.Put("bar", false);
					IDictionary<string, object> attachment = new Dictionary<string, object>();
					attachment.Put("content_type", "image/png");
					attachment.Put("data", attachmentBase64);
					IDictionary<string, object> attachments = new Dictionary<string, object>();
					attachments.Put(attachmentName, attachment);
					documentProperties.Put("_attachments", attachments);
					Document doc = database.GetDocument(docId);
					doc.PutProperties(documentProperties);
				}
				else
				{
					byte[] bytes = baos.ToByteArray();
					string attachmentBase64 = Base64.EncodeBytes(bytes, Base64.Gzip);
					documentProperties.Put("foo", 1);
					documentProperties.Put("bar", false);
					IDictionary<string, object> attachment = new Dictionary<string, object>();
					attachment.Put("content_type", "image/png");
					attachment.Put("data", attachmentBase64);
					attachment.Put("encoding", "gzip");
					attachment.Put("length", bytes.Length);
					IDictionary<string, object> attachments = new Dictionary<string, object>();
					attachments.Put(attachmentName, attachment);
					documentProperties.Put("_attachments", attachments);
					Document doc = database.GetDocument(docId);
					doc.PutProperties(documentProperties);
				}
			}
		}

		private int GetNumberOfDocuments()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test6_numberOfDocuments"));
		}
	}
}
