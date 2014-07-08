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
//using System.Collections;
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Performance
{
	public class Test8_DocRevisions : LiteTestCase
	{
		public const string Tag = "DocRevisionsPerformance";

		private Document[] docs;

		/// <exception cref="System.Exception"></exception>
		protected override void SetUp()
		{
			Log.V(Tag, "DocRevisionsPerformance setUp");
			base.SetUp();
			docs = new Document[GetNumberOfDocuments()];
			//Create docs that will be updated in test case
			NUnit.Framework.Assert.IsTrue(database.RunInTransaction(new _TransactionalTask_46
				(this)));
		}

		private sealed class _TransactionalTask_46 : TransactionalTask
		{
			public _TransactionalTask_46(Test8_DocRevisions _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public bool Run()
			{
				for (int i = 0; i < this._enclosing.GetNumberOfDocuments(); i++)
				{
					//create a document
					IDictionary<string, object> props = new Dictionary<string, object>();
					props.Put("toogle", true);
					Document doc = this._enclosing.database.CreateDocument();
					this._enclosing.docs[i] = doc;
					try
					{
						doc.PutProperties(props);
					}
					catch (CouchbaseLiteException cblex)
					{
						Log.E(Test8_DocRevisions.Tag, "Document creation failed", cblex);
						return false;
					}
				}
				return true;
			}

			private readonly Test8_DocRevisions _enclosing;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestDocRevisionsPerformance()
		{
			long startMillis = Runtime.CurrentTimeMillis();
			for (int j = 0; j < GetNumberOfDocuments(); j++)
			{
				Document doc = docs[j];
				for (int k = 0; k < GetNumberOfUpdates(); k++)
				{
					IDictionary<string, object> contents = new Hashtable(doc.GetProperties());
					bool wasChecked = (bool)contents.Get("toogle");
					//toggle value of check property
					contents.Put("toogle", !wasChecked);
					try
					{
						doc.PutProperties(contents);
					}
					catch (CouchbaseLiteException cblex)
					{
						Log.E(Tag, "Document update failed", cblex);
						//return false;
						throw;
					}
				}
			}
			Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
				() - startMillis).ToString() + "," + GetNumberOfDocuments() + ",,,," + GetNumberOfUpdates
				());
		}

		private int GetNumberOfDocuments()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test8_numberOfDocuments"));
		}

		private int GetNumberOfUpdates()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("Test8_numberOfUpdates"));
		}
	}
}
