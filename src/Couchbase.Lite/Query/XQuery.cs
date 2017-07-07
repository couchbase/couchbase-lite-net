using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal unsafe class XQuery : IQuery, IQueryInternal
    {
        private const string Tag = nameof(XQuery);

        private C4Query* _c4Query;

        public Database Database { get; set; }

        public IParameters Parameters { get; } = new QueryParameters();

        protected Select SelectImpl { get; set; }

        protected bool Distinct { get; set; }

        protected QueryDataSource FromImpl { get; set; }

        protected QueryExpression WhereImpl { get; set; }

        protected QueryOrdering OrderByImpl { get; set; }

        protected QueryJoin JoinImpl { get; set; }

        protected QueryGroupBy GroupByImpl { get; set; }

        protected Having HavingImpl { get; set; }

        protected object SkipValue { get; set; }

        protected object LimitValue { get; set; }

        ~XQuery()
        {
            Dispose(true);
        }

        public IResultSet Run()
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

            var e = (C4QueryEnumerator*) LiteCoreBridge.Check(err =>
            {
                var localOpts = options;
                return Native.c4query_run(_c4Query, &localOpts, paramJson, err);
            });

            return new QueryEnumerator(this, _c4Query, e);
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
            Native.c4query_free(_c4Query);
            _c4Query = null;
        }

        internal string Explain()
        {
            // Used for debugging
            return Native.c4query_explain(_c4Query);
        }

        private void Check()
        {
            var jsonData = EncodeAsJSON();
            Log.To.Query.I(Tag, $"Query encoded as {jsonData}");
            var query = (C4Query*)LiteCoreBridge.Check(err => Native.c4query_new(Database.c4db, jsonData, err));
            Native.c4query_free(_c4Query);
            _c4Query = query;
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
                parameters["LIMIT"] = LimitValue;
            }

            if (SkipValue != null) {
                parameters["OFFSET"] = SkipValue;
            }
;
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
            }
            
            if (GroupByImpl != null) {
                parameters["GROUP_BY"] = GroupByImpl.ToJSON();
            }
            
            if (HavingImpl != null) {
                parameters["HAVING"] = HavingImpl.ToJSON();
            }

            return JsonConvert.SerializeObject(parameters);
        }

        public void Dispose()
        {
            Dispose(false);
        }
    }
}
