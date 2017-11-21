// 
// XQuery.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal unsafe class XQuery : IQuery
    {
        #region Constants

        private const string Tag = nameof(XQuery);

        #endregion

        #region Variables

        private C4Query* _c4Query;
        private Dictionary<string, int> _columnNames;

        #endregion

        #region Properties

        public Database Database { get; set; }

        public IParameters Parameters { get; } = new QueryParameters();

        protected bool Distinct { get; set; }

        protected QueryDataSource FromImpl { get; set; }

        protected QueryGroupBy GroupByImpl { get; set; }

        protected Having HavingImpl { get; set; }

        protected QueryJoin JoinImpl { get; set; }

        protected object LimitValue { get; set; }

        protected QueryOrdering OrderByImpl { get; set; }

        protected Select SelectImpl { get; set; }

        protected object SkipValue { get; set; }

        protected QueryExpression WhereImpl { get; set; }

        #endregion

        #region Constructors

        ~XQuery()
        {
            Dispose(true);
        }

        #endregion

        #region Protected Methods

        protected void Copy(XQuery source)
        {
            Database = source.Database;
            SelectImpl = source.SelectImpl;
            Distinct = source.Distinct;
            FromImpl = source.FromImpl;
            WhereImpl = source.WhereImpl;
            OrderByImpl = source.OrderByImpl;
            JoinImpl = source.JoinImpl;
            GroupByImpl = source.GroupByImpl;
            HavingImpl = source.HavingImpl;
            LimitValue = source.LimitValue;
            SkipValue = source.SkipValue;
        }

        protected virtual void Dispose(bool finalizing)
        {
            if (!finalizing) {
                FromImpl.ThreadSafety.DoLocked(() => Native.c4query_free(_c4Query));
            }
            else {
                // Database is not valid inside finalizer, but thread safety
                // is guaranteed
                Native.c4query_free(_c4Query);
            }

            _c4Query = null;
        }

        #endregion

        #region Internal Methods

        internal string Explain()
        {
            // Used for debugging
            if (_c4Query == null) {
                Check();
            }

            return FromImpl.ThreadSafety.DoLocked(() => Native.c4query_explain(_c4Query));
        }

        #endregion

        #region Private Methods

        private void Check()
        {
            var jsonData = EncodeAsJSON();
            if (_columnNames == null) {
                _columnNames = CreateColumnNames();
            }

            Log.To.Query.I(Tag, $"Query encoded as {jsonData}");

            FromImpl.ThreadSafety.DoLockedBridge(err =>
            {
                var query = Native.c4query_new(Database.c4db, jsonData, err);
                if(query == null) {
                    return false;
                }

                Native.c4query_free(_c4Query);
                _c4Query = query;
                return true;
            });
        }

        private Dictionary<string, int> CreateColumnNames()
        {
            var map = new Dictionary<string, int>();
            var index = 0;
            var provisionKeyIndex = 0;
            foreach (var select in SelectImpl.SelectResults) {
                var name = select.ColumnName ?? $"${++provisionKeyIndex}";
                if (map.ContainsKey(name)) {
                    throw new CouchbaseLiteException(StatusCode.InvalidQuery, $"Duplicate select result named {name}");
                }

                map[name] = index;
                index++;
            }

            return map;
        }

        private string EncodeAsJSON()
        {
            var parameters = new Dictionary<string, object>();
            if (WhereImpl != null) {
                parameters["WHERE"] = WhereImpl.ConvertToJSON();
            }

            if (Distinct) {
                parameters["DISTINCT"] = true;
            }

            if (LimitValue != null) {
                if (LimitValue is QueryExpression e) {
                    parameters["LIMIT"] = e.ConvertToJSON();
                } else {
                    parameters["LIMIT"] = LimitValue;
                }
            }

            if (SkipValue != null) {
                if (SkipValue is QueryExpression e) {
                    parameters["OFFSET"] = e.ConvertToJSON();
                }
                else {
                    parameters["OFFSET"] = SkipValue;
                }
            }

            if (OrderByImpl != null) {
                parameters["ORDER_BY"] = OrderByImpl.ToJSON();
            }

            var selectParam = SelectImpl?.ToJSON();
            if (selectParam != null) {
                parameters["WHAT"] = selectParam;
            }

            if (JoinImpl != null) {
                var fromJson = FromImpl?.ToJSON();
                if (fromJson == null) {
                    throw new InvalidOperationException(
                        "The default database must have an alias in order to use a JOIN statement" +
                        " (Make sure your data source uses the As() function)");
                }

                var joinJson = JoinImpl.ToJSON() as IList<object>;
                Debug.Assert(joinJson != null);
                joinJson.Insert(0, fromJson);
                parameters["FROM"] = joinJson;
            } else {
                var fromJson = FromImpl?.ToJSON();
                if(fromJson != null) {
                    parameters["FROM"] = new[] { fromJson };
                }
            }
            
            if (GroupByImpl != null) {
                parameters["GROUP_BY"] = GroupByImpl.ToJSON();
            }
            
            if (HavingImpl != null) {
                parameters["HAVING"] = HavingImpl.ToJSON();
            }

            return JsonConvert.SerializeObject(parameters);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(false);
        }

        #endregion

        #region IQuery

        public IResultSet Execute()
        {
            if (Database == null) {
                throw new InvalidOperationException("Invalid query, Database == null");
            }

            if (SelectImpl == null || FromImpl == null) {
                throw new InvalidOperationException("Invalid query, missing Select or From");
            }


            if (_c4Query == null) {
                Check();
            }

            var options = C4QueryOptions.Default;
            var paramJson = ((QueryParameters) Parameters).ToString();

            var e = (C4QueryEnumerator*) FromImpl.ThreadSafety.DoLockedBridge(err =>
            {
                var localOpts = options;
                return Native.c4query_run(_c4Query, &localOpts, paramJson, err);
            });

            return new QueryResultSet(this, FromImpl.ThreadSafety, e, _columnNames);
        }

        public ILiveQuery ToLive()
        {
            Dispose();
            return new LiveQuery(new XQuery {
                Database = Database,
                SelectImpl = SelectImpl,
                Distinct = Distinct,
                FromImpl = FromImpl,
                WhereImpl = WhereImpl,
                OrderByImpl = OrderByImpl,
                JoinImpl = JoinImpl,
                GroupByImpl = GroupByImpl,
                HavingImpl = HavingImpl,
                LimitValue = LimitValue,
                SkipValue = SkipValue
            });
        }

        #endregion
    }
}
