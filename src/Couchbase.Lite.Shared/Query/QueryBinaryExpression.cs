// 
// QueryBinaryExpression.cs
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
using System.Collections.Generic;
using System.Diagnostics;

using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal enum BinaryOpType
    {
        LessThan,
        LessThanOrEqualTo,
        GreaterThan,
        GreaterThanOrEqualTo,
        EqualTo,
        NotEqualTo,
        Is,
        IsNot,
        Matches,
        Like,
        Between,
        RegexLike,
        In,
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus
    }

    internal sealed class QueryBinaryExpression : QueryExpression
    {
        #region Variables

        private readonly QueryExpression _lhs;
        private readonly QueryExpression _rhs;
        private readonly BinaryOpType _type;

        #endregion

        #region Constructors

        public QueryBinaryExpression(QueryExpression lhs, QueryExpression rhs, BinaryOpType type)
        {
            _lhs = lhs;
            _rhs = rhs;
            _type = type;
        }

        #endregion

        #region Overrides

        protected override object ToJSON()
        {
            var obj = new List<object>();
            var useArrayOp = false;
            switch (_type) {
                case BinaryOpType.Add:
                    obj.Add("+");
                    break;
                case BinaryOpType.Between:
                    obj.Add("BETWEEN");
                    break;
                case BinaryOpType.Divide:
                    obj.Add("/");
                    break;
                case BinaryOpType.EqualTo:
                    obj.Add("=");
                    break;
                case BinaryOpType.GreaterThan:
                    obj.Add(">");
                    break;
                case BinaryOpType.GreaterThanOrEqualTo:
                    obj.Add(">=");
                    break;
                case BinaryOpType.In:
                    obj.Add("IN");
                    useArrayOp = true;
                    break;
                case BinaryOpType.Is:
                    obj.Add("IS");
                    break;
                case BinaryOpType.IsNot:
                    obj.Add("IS NOT");
                    break;
                case BinaryOpType.LessThan:
                    obj.Add("<");
                    break;
                case BinaryOpType.LessThanOrEqualTo:
                    obj.Add("<=");
                    break;
                case BinaryOpType.Like:
                    obj.Add("LIKE");
                    break;
                case BinaryOpType.Matches:
                    obj.Add("MATCH");
                    break;
                case BinaryOpType.Modulus:
                    obj.Add("%");
                    break;
                case BinaryOpType.Multiply:
                    obj.Add("*");
                    break;
                case BinaryOpType.NotEqualTo:
                    obj.Add("!=");
                    break;
                case BinaryOpType.RegexLike:
                    obj.Add("regexp_like()");
                    break;
                case BinaryOpType.Subtract:
                    obj.Add("-");
                    break;
            }

            obj.Add(_lhs.ConvertToJSON());
            if ((_rhs as QueryTypeExpression)?.ExpressionType == ExpressionType.Aggregate) {
                var collection = _rhs.ConvertToJSON() as IList<object>;
                Debug.Assert(collection != null);
                if (useArrayOp) {
                    collection?.Insert(0, "[]");
                    obj.Add(collection);
                } else {
                    obj.AddRange(collection);
                }
            } else {
                obj.Add(_rhs.ConvertToJSON());
            }
            return obj;
        }

        #endregion
    }
}
