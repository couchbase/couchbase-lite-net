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

        protected ISelect SelectImpl { get; set; }

        protected bool Distinct { get; set; }

        protected IDataSource FromImpl { get; set; }

        protected IExpression WhereImpl { get; set; }

        protected IOrdering OrderingImpl { get; set; }

        protected IJoin JoinImpl { get; set; }

        protected IGroupBy GroupByImpl { get; set; }

        protected IHaving HavingImpl { get; set; }

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
                OrderingImpl = OrderingImpl,
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
            OrderingImpl = source.OrderingImpl;
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
            var where = WhereImpl as QueryExpression;
            if (where != null) {
                parameters["WHERE"] = where.ConvertToJSON();
            } else if (WhereImpl != null) {
                throw new NotSupportedException("Custom IWhere not supported");
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

            var orderBy = OrderingImpl as QueryOrdering;
            if (orderBy != null) {
                parameters["ORDER_BY"] = orderBy.ToJSON();
            } else if (OrderingImpl != null) {
                throw new NotSupportedException("Custom IOrdering not supported");
            }

            var select = SelectImpl as Select;
            if (select != null) {
                var selectParam = select.ToJSON();
                if (selectParam != null) {
                    parameters["WHAT"] = selectParam;
                }
            } else {
                throw new NotSupportedException("Custom ISelect not supported");
            }

            var join = JoinImpl as Join;
            var from = FromImpl as DataSource;
            if (join != null) {
                var fromJson = from?.ToJSON();
                if (fromJson == null) {
                    throw new InvalidOperationException(
                        "The default database must have an alias in order to use a JOIN statement" +
                        " (Make sure your data source uses the As() function)");
                }

                var joinJson = join.ToJSON() as IList<object>;
                Debug.Assert(joinJson != null);
                joinJson.Insert(0, fromJson);
                parameters["FROM"] = joinJson;
            } else if(JoinImpl != null) {
                throw new NotSupportedException("Custom IJoin not supported");
            }

            var groupBy = GroupByImpl as QueryGroupBy;
            if (groupBy != null) {
                parameters["GROUP_BY"] = groupBy.ToJSON();
            } else if (GroupByImpl != null) {
                throw new NotSupportedException("Custom IGroupBy not supported");
            }

            var having = HavingImpl as Having;
            if (having != null) {
                parameters["HAVING"] = having.ToJSON();
            }
            else if (HavingImpl != null) {
                throw new NotSupportedException("Custom IHaving not supported");
            }

            return JsonConvert.SerializeObject(parameters);
        }

        public void Dispose()
        {
            Dispose(false);
        }
    }
}
