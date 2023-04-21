// 
//  QueryTestBase.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

#nullable disable
#if !CBL_NO_EXTERN_FILES
using LiteCore.Interop;
using FluentAssertions;

using System.Collections.Generic;
using System.Text;
using LiteCore.Util;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe abstract class QueryTestBase : Test
    {
        internal C4Query *_query;

        internal delegate T CollectingDelegate<out T>(C4QueryEnumerator *e);

        protected abstract string JsonPath
        {
            get;
        }

#if !WINDOWS_UWP
        protected QueryTestBase(ITestOutputHelper output) : base(output)
        {

        }
#endif

        internal IList<T> RunCollecting<T>(string bindings, CollectingDelegate<T> callback)
        {
            ((long)_query).Should().NotBe(0, "because otherwise what are we testing?");
            var returnList = new List<T>();
            var e = (C4QueryEnumerator*)LiteCoreBridge.Check(err => {
                var options = C4QueryOptions.Default;
                return Native.c4query_run(_query, &options, bindings, err);
            });

            C4Error error;
            while(Native.c4queryenum_next(e, &error)) {
                Native.FLArrayIterator_GetCount(&e->columns).Should().BeGreaterThan(0);
                returnList.Add(callback(e));
            }

            error.code.Should().Be(0, "because otherwise an error occurred during enumeration");
            Native.c4queryenum_release(e);
            return returnList;
        }

        protected IList<string> Run(string bindings = null)
        {
            return RunCollecting(bindings, e =>
            {
                Native.FLArrayIterator_GetCount(&e->columns).Should().BeGreaterThan(0);
                var docID = Native.FLValue_AsString(Native.FLArrayIterator_GetValueAt(&e->columns, 0U));
                return docID;
            });
        }

        internal IList<C4FullTextMatch[]> RunFTS(string bindings = null)
        {
            return RunCollecting(bindings, e =>
            {
                var retVal = new C4FullTextMatch[e->fullTextMatchCount];
                for (var i = 0; i < e->fullTextMatchCount; i++) {
                    retVal[i] = e->fullTextMatches[i];
                }

                return retVal;
            });
        }

        protected string Json5(string input)
        {
            FLError err;
            var json = Native.FLJSON5_ToJSON(input, null, null, &err);
            json.Should().NotBeNull("because otherwise the JSON failed to parse");
            return json;
        }

        internal C4Query* Compile(string whereExpr, string sortExpr = null, bool addOffsetLimit = false)
        {
            var json = new StringBuilder($"[\"SELECT\", {{\"WHERE\": {whereExpr}");
            if (sortExpr != null) {
                json.Append($", \"ORDER_BY\": {sortExpr}");
            }

            if (addOffsetLimit) {
                json.Append($", \"OFFSET\": [\"$offset\"], \"LIMIT\": [\"$limit\"]");
            }

            json.Append("}]");
            return CompileSelect(json.ToString());
        }

        internal C4Query* CompileSelect(string queryString)
        {
            WriteLine($"Query = {queryString}");
            Native.c4query_release(_query);
            _query = (C4Query*)LiteCoreBridge.Check(err => Native.c4query_new2(Db, C4QueryLanguage.JSONQuery, queryString, null, err));
            return _query;
        }

        internal void AddPersonInState(string docID, string state, string firstName = null)
        {
            LiteCoreBridge.Check(err => Native.c4db_beginTransaction(Db, err));
            C4Error error;
            var enc = Native.c4db_getSharedFleeceEncoder(Db);
            try {
                Native.FLEncoder_BeginDict(enc, 3);
                Native.FLEncoder_WriteKey(enc, "custom");
                Native.FLEncoder_WriteBool(enc, true);
                if (!string.IsNullOrEmpty(firstName)) {
                    Native.FLEncoder_WriteKey(enc, "name");
                    Native.FLEncoder_BeginDict(enc, 2);
                    Native.FLEncoder_WriteKey(enc, "first");
                    Native.FLEncoder_WriteString(enc, firstName);
                    Native.FLEncoder_WriteKey(enc, "last");
                    Native.FLEncoder_WriteString(enc, "lastname");
                    Native.FLEncoder_EndDict(enc);
                }

                Native.FLEncoder_WriteKey(enc, "contact");
                Native.FLEncoder_BeginDict(enc, 1);
                Native.FLEncoder_WriteKey(enc, "address");
                Native.FLEncoder_BeginDict(enc, 1);
                Native.FLEncoder_WriteKey(enc, "state");
                Native.FLEncoder_WriteString(enc, state);
                Native.FLEncoder_EndDict(enc);
                Native.FLEncoder_EndDict(enc);
                Native.FLEncoder_EndDict(enc);

                // Save document:
                FLSliceResult body = NativeRaw.FLEncoder_Finish(enc, null);
                //REQUIRE(body.buf);
                using (var docID_ = new C4String(docID)){
                    var rq = new C4DocPutRequest {
                        allowConflict = false,
                        docID = docID_.AsFLSlice(),
                        allocedBody = body,
                        save = true
                    };

                    C4Document* doc;
                    doc = Native.c4coll_putDoc(Native.c4db_getDefaultCollection(Db, null), &rq, null, &error);
                    ((long)doc).Should().NotBe(0, "because otherwise the put failed");
                    Native.c4doc_release(doc);
                    Native.FLSliceResult_Release(body);
                }
            } finally {
                LiteCoreBridge.Check(err => Native.c4db_endTransaction(Db, true, err));
            }
        }

        protected override void SetupVariant(int option)
        {
            base.SetupVariant(option);
            ImportJSONLines(JsonPath);
        }

        protected override void TeardownVariant(int option)
        {
            Native.c4query_release(_query);
            _query = null;
            base.TeardownVariant(option);
        }
    }
}
#endif