using System.Collections;
using System.Collections.Generic;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryCollectionExpression : QueryExpression
    {
        #region Constants

        private const string Tag = nameof(QueryCollectionExpression);

        #endregion

        #region Variables

        private readonly IList _arrayContent;

        private readonly IDictionary<string, object> _dictContent;

        #endregion

        #region Constructors

        public QueryCollectionExpression([NotNull]IDictionary<string, object> content)
        {
            _dictContent = EvaluateDictionary(CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(content), content));
        }

        public QueryCollectionExpression(IList content)
        {
            _arrayContent = EvaluateList(CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(content), content));
        }

        #endregion

        #region Private Methods

        [NotNull]
        private IDictionary<string, object> EvaluateDictionary(IDictionary<string, object> content)
        {
            var retVal = new Dictionary<string, object>();
            foreach (var pair in content) {
                switch (pair.Value) {
                    case IDictionary<string, object> dict:
                        retVal[pair.Key] = EvaluateDictionary(dict);
                        break;
                    case IList list:
                        retVal[pair.Key] = EvaluateList(list);
                        break;
                    case QueryExpression qe:
                        retVal[pair.Key] = qe.ConvertToJSON();
                        break;
                    default:
                        retVal[pair.Key] = pair.Value;
                        break;
                }
            }

            return retVal;
        }

        private IList EvaluateList(IList list)
        {
            var retVal = new List<object>();
            foreach (var item in list) {
                switch (item) {
                    case IDictionary<string, object> dict:
                        retVal.Add(EvaluateDictionary(dict));
                        break;
                    case IList l:
                        retVal.Add(EvaluateList(l));
                        break;
                    case QueryExpression qe:
                        retVal.Add(qe.ConvertToJSON());
                        break;
                    default:
                        retVal.Add(item);
                        break;
                }
            }

            return retVal;
        }

        #endregion

        #region Overrides

        protected override object ToJSON() => (object)_dictContent ?? _arrayContent;

        public override string ToString() => JsonConvert.SerializeObject(ConvertToJSON());

        #endregion
    }
}
