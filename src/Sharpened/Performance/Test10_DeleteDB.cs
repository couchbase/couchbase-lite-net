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
    public class Test10_DeleteDB : LiteTestCase
    {
        public const string Tag = "DeleteDBPerformance";

        private const string _propertyValue = "1234567";

        /// <exception cref="System.Exception"></exception>
        protected override void SetUp()
        {
            Log.V(Tag, "DeleteDBPerformance setUp");
            base.SetUp();
            //Create docs that will be deleted in test case
            NUnit.Framework.Assert.IsTrue(database.RunInTransaction(new _TransactionalTask_43
                (this)));
        }

        private sealed class _TransactionalTask_43 : TransactionalTask
        {
            public _TransactionalTask_43(Test10_DeleteDB _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public bool Run()
            {
                string[] bigObj = new string[this._enclosing.GetSizeOfDocument()];
                for (int i = 0; i < this._enclosing.GetSizeOfDocument(); i++)
                {
                    bigObj[i] = Test10_DeleteDB._propertyValue;
                }
                for (int i_1 = 0; i_1 < this._enclosing.GetNumberOfDocuments(); i_1++)
                {
                    //create a document
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
                        Log.E(Test10_DeleteDB.Tag, "Document create failed", t);
                        return false;
                    }
                }
                return true;
            }

            private readonly Test10_DeleteDB _enclosing;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestDeleteDBPerformance()
        {
            long startMillis = Runtime.CurrentTimeMillis();
            try
            {
                for (int i = 0; i < GetNumberOfDBs(); i++)
                {
                    //Note: This shuts down the current manager and database
                    //but does not delete the database, that is done in setUp()
                    base.TearDown();
                    SetUp();
                }
            }
            catch (Exception ex)
            {
                //run local test setup
                Log.E(Tag, "DB Teardown/Setup failed", ex);
                Fail();
            }
            Log.V("PerformanceStats", Tag + "," + Sharpen.Extensions.ValueOf(Runtime.CurrentTimeMillis
                () - startMillis).ToString() + "," + GetNumberOfDocuments() + "," + GetSizeOfDocument
                () + "," + GetNumberOfDBs());
        }

        private int GetSizeOfDocument()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test10_sizeOfDocument"));
        }

        private int GetNumberOfDocuments()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test10_numberOfDocuments"));
        }

        private int GetNumberOfDBs()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test10_numberOfDBs"));
        }
    }
}
