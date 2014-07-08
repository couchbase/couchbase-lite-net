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
using Couchbase.Lite;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Performance
{
	public class Test11_DeleteDocs : LiteTestCase
	{
		public const string Tag = "DeleteDocsPerformance";

		private const string _propertyValue = "1234567";

		private Document[] docs;

		/// <exception cref="System.Exception"></exception>
		protected override void SetUp()
		{
			Log.V(Tag, "DeleteDocsPerformance setUp");
			base.SetUp();
			docs = new Document[GetNumberOfDocuments()];
			//Create docs that will be deleted in test case
			NUnit.Framework.Assert.IsTrue(database.RunInTransaction(new _TransactionalTask_49
				(this)));
		}

		private sealed class _TransactionalTask_49 : TransactionalTask
		{
			public _TransactionalTask_49(Test11_DeleteDocs _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public bool Run()
			{
				string[] bigObj = new string[this._enclosing.GetSizeOfDocument()];
				for (int i = 0; i < this._enclosing.GetSizeOfDocument(); i++)
				{
					bigObj[i] = Test11_DeleteDocs._propertyValue;
				}
				for (int i_1 = 0; i_1 < this._enclosing.GetNumberOfDocuments(); i_1++)
				{
					//create a document
					IDictionary<string, object> props = new Dictionary<string, object>();
					props.Put("bigArray", bigObj);
					Document doc = this._enclosing.database.CreateDocument();
					this._enclosing.docs[i_1] = doc;
					try
					{
						doc.PutProperties(props);
					}
					catch (CouchbaseLiteException cblex)
					{
						Log.E(Test11_DeleteDocs.Tag, "Document creation failed", cblex);
						return false;
					}
				}
				return true;
			}

			private readonly Test11_DeleteDocs _enclosing;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestDeleteDocsPerformance()
		{
			long startMillis = Runtime.CurrentTimeMillis();
			NUnit.Framework.Assert.IsTrue(database.RunInTransaction(new _TransactionalTask_85
				(this)));
			Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
				() - startMillis).ToString() + "," + GetNumberOfDocuments() + "," + GetSizeOfDocument
				());
		}

		private sealed class _TransactionalTask_85 : TransactionalTask
		{
			public _TransactionalTask_85(Test11_DeleteDocs _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public bool Run()
			{
				for (int i = 0; i < this._enclosing.GetNumberOfDocuments(); i++)
				{
					Document doc = this._enclosing.docs[i];
					try
					{
						doc.Delete();
					}
					catch (Exception t)
					{
						Log.E(Test11_DeleteDocs.Tag, "Document delete failed", t);
						return false;
					}
				}
				return true;
			}

			private readonly Test11_DeleteDocs _enclosing;
		}

		private int GetSizeOfDocument()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test11_sizeOfDocument"));
		}

		private int GetNumberOfDocuments()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test11_numberOfDocuments"));
		}
	}
}
