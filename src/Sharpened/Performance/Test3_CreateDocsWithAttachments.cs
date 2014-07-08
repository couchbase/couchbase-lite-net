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
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite.Performance
{
	public class Test3_CreateDocsWithAttachments : LiteTestCase
	{
		public const string Tag = "CreateDocsWithAttachmentsPerformance";

		private const string _testAttachmentName = "test_attachment";

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestCreateDocsWithAttachmentsPerformance()
		{
			long startMillis = Runtime.CurrentTimeMillis();
			bool success = database.RunInTransaction(new _TransactionalTask_44(this));
			Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
				() - startMillis).ToString() + "," + GetNumberOfDocuments() + "," + GetSizeOfAttachment
				());
		}

		private sealed class _TransactionalTask_44 : TransactionalTask
		{
			public _TransactionalTask_44(Test3_CreateDocsWithAttachments _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public bool Run()
			{
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < this._enclosing.GetSizeOfAttachment(); i++)
				{
					sb.Append('1');
				}
				byte[] attach1 = Sharpen.Runtime.GetBytesForString(sb.ToString());
				try
				{
					Status status = new Status();
					for (int i_1 = 0; i_1 < this._enclosing.GetNumberOfDocuments(); i_1++)
					{
						IDictionary<string, object> rev1Properties = new Dictionary<string, object>();
						rev1Properties.Put("foo", 1);
						rev1Properties.Put("bar", false);
						RevisionInternal rev1 = this._enclosing.database.PutRevision(new RevisionInternal
							(rev1Properties, this._enclosing.database), null, false, status);
						NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
						this._enclosing.database.InsertAttachmentForSequenceWithNameAndType(new ByteArrayInputStream
							(attach1), rev1.GetSequence(), Test3_CreateDocsWithAttachments._testAttachmentName
							, "text/plain", rev1.GetGeneration());
						NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
					}
				}
				catch (Exception t)
				{
					Log.E(Test3_CreateDocsWithAttachments.Tag, "Document create with attachment failed"
						, t);
					return false;
				}
				return true;
			}

			private readonly Test3_CreateDocsWithAttachments _enclosing;
		}

		private int GetSizeOfAttachment()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test3_sizeOfAttachment"));
		}

		private int GetNumberOfDocuments()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test3_numberOfDocuments"));
		}
	}
}
