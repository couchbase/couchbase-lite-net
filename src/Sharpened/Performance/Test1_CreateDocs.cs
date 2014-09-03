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
using Couchbase.Lite.Internal;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Performance
{
    public class Test1_CreateDocs : LiteTestCase
    {
        public const string Tag = "CreateDocsPerformance";

        private const string _propertyValue = "1234567";

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestCreateDocsPerformance()
        {
            long startMillis = Runtime.CurrentTimeMillis();
            bool success = database.RunInTransaction(new _TransactionalTask_50(this));
            //create a document
            Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
                () - startMillis).ToString() + "," + GetNumberOfDocuments() + "," + GetSizeOfDocument
                ());
        }

        private sealed class _TransactionalTask_50 : TransactionalTask
        {
            public _TransactionalTask_50(Test1_CreateDocs _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public bool Run()
            {
                string[] bigObj = new string[this._enclosing.GetSizeOfDocument()];
                for (int i = 0; i < this._enclosing.GetSizeOfDocument(); i++)
                {
                    bigObj[i] = Test1_CreateDocs._propertyValue;
                }
                for (int i_1 = 0; i_1 < this._enclosing.GetNumberOfDocuments(); i_1++)
                {
                    IDictionary<string, object> props = new Dictionary<string, object>();
                    props.Put("bigArray", bigObj);
                    Body body = new Body(props);
                    RevisionInternal rev1 = new RevisionInternal(body, this._enclosing.database);
                    Status status = new Status();
                    try
                    {
                        rev1 = this._enclosing.database.PutRevision(rev1, null, false, status);
                    }
                    catch (Exception t)
                    {
                        Log.E(Test1_CreateDocs.Tag, "Document create failed", t);
                        return false;
                    }
                }
                return true;
            }

            private readonly Test1_CreateDocs _enclosing;
        }

        private int GetSizeOfDocument()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test1_sizeOfDocument"));
        }

        private int GetNumberOfDocuments()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test1_numberOfDocuments"));
        }
    }
}
