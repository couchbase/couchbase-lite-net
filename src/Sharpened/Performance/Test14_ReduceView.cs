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
//using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Performance;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Performance
{
    public class Test14_ReduceView : LiteTestCase
    {
        public const string Tag = "ReduceViewPerformance";

        /// <exception cref="System.Exception"></exception>
        protected override void SetUp()
        {
            Log.V(Tag, "ReduceViewPerformance setUp");
            base.SetUp();
            View view = database.GetView("vacant");
            view.SetMapReduce(new _Mapper_49(), new _Reducer_59(), "1.0.0");
            bool success = database.RunInTransaction(new _TransactionalTask_67(this));
            view.UpdateIndex();
        }

        private sealed class _Mapper_49 : Mapper
        {
            public _Mapper_49()
            {
            }

            public void Map(IDictionary<string, object> document, Emitter emitter)
            {
                bool vacant = (bool)document.Get("vacant");
                string name = (string)document.Get("name");
                if (vacant && name != null)
                {
                    emitter.Emit(name, vacant);
                }
            }
        }

        private sealed class _Reducer_59 : Reducer
        {
            public _Reducer_59()
            {
            }

            public object Reduce(IList<object> keys, IList<object> values, bool rereduce)
            {
                return View.TotalValues(values);
            }
        }

        private sealed class _TransactionalTask_67 : TransactionalTask
        {
            public _TransactionalTask_67(Test14_ReduceView _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public bool Run()
            {
                for (int i = 0; i < this._enclosing.GetNumberOfDocuments(); i++)
                {
                    string name = string.Format("%s%s", "n", i);
                    bool vacant = ((i + 2) % 2 == 0) ? true : false;
                    IDictionary<string, object> props = new Dictionary<string, object>();
                    props.Put("name", name);
                    props.Put("apt", i);
                    props.Put("phone", 408100000 + i);
                    props.Put("vacant", vacant);
                    Document doc = this._enclosing.database.CreateDocument();
                    try
                    {
                        doc.PutProperties(props);
                    }
                    catch (CouchbaseLiteException cblex)
                    {
                        Log.E(Test14_ReduceView.Tag, "!!! Failed to create doc " + props, cblex);
                        return false;
                    }
                }
                return true;
            }

            private readonly Test14_ReduceView _enclosing;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public virtual void TestViewReducePerformance()
        {
            long startMillis = Runtime.CurrentTimeMillis();
            Query query = database.GetView("vacant").CreateQuery();
            query.SetMapOnly(false);
            QueryEnumerator rowEnum = query.Run();
            QueryRow row = rowEnum.GetRow(0);
            Log.V("PerformanceStats", Tag + ":testViewReducePerformance," + Sharpen.Extensions.ValueOf
                (Runtime.CurrentTimeMillis() - startMillis).ToString() + "," + GetNumberOfDocuments
                ());
        }

        private int GetNumberOfDocuments()
        {
            return System.Convert.ToInt32(Runtime.GetProperty("Test14_numberOfDocuments"));
        }
    }
}
