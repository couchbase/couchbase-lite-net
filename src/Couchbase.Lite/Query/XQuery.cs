using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using LiteCore;
using LiteCore.Interop;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract unsafe class XQuery : IQuery, IQueryInternal
    {
        private const string Tag = nameof(XQuery);

        private C4Query* _c4Query;
        private ulong _skip;
        private ulong _limit = UInt64.MaxValue;
        private IDictionary<string, object> _parameters = new Dictionary<string, object>();

        public Database Database { get; set; }

        protected ISelect SelectImpl { get; set; }

        protected bool Distinct { get; set; }

        protected IDataSource FromImpl { get; set; }

        protected IExpression WhereImpl { get; set; }

        protected IOrderBy OrderByImpl { get; set; }

        ~XQuery()
        {
            Dispose(true);
        }

        public IReadOnlyList<IQueryRow> Run()
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
            options.skip = _skip;
            options.limit = _limit;
            var paramJson = default(string);
            if (_parameters.Any()) {
                paramJson = JsonConvert.SerializeObject(_parameters);
            }

            var e = (C4QueryEnumerator*) LiteCoreBridge.Check(err =>
            {
                var localOpts = options;
                return Native.c4query_run(_c4Query, &localOpts, paramJson, err);
            });

            return new QueryEnumerator(this, _c4Query, e);
        }

        public IQuery Skip(ulong skip)
        {
            _skip = skip;
            return this;
        }

        public ILiveQuery ToLiveQuery()
        {
            Dispose();
            return new LiveQuery(this);
        }

        public IQuery Limit(ulong limit)
        {
            _limit = limit;
            return this;
        }

        public IQuery SetParameters(IDictionary<string, object> parameters)
        {
            _parameters = parameters ?? new Dictionary<string, object>();
            return this;
        }

        protected void Copy(XQuery source)
        {
            Database = source.Database;
            SelectImpl = source.SelectImpl;
            Distinct = source.Distinct;
            FromImpl = source.FromImpl;
            WhereImpl = source.WhereImpl;
            OrderByImpl = source.OrderByImpl;
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

            var orderBy = OrderByImpl as OrderBy;
            if (orderBy != null) {
                parameters["ORDER_BY"] = orderBy.ToJSON();
            } else if (OrderByImpl != null) {
                throw new NotSupportedException("Custom IOrderBy not supported");
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

            return JsonConvert.SerializeObject(parameters);
        }

        public void Dispose()
        {
            Dispose(false);
        }
    }
}
