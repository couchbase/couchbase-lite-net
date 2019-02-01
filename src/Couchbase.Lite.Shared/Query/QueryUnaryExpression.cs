// 
// QueryUnaryExpression.cs
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

using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query
{
    internal enum UnaryOpType
    {
        Missing,
        NotMissing,
        NotNull,
        Null
    }

    internal sealed class QueryUnaryExpression : QueryExpression
    {
        #region Variables

        private readonly IExpression _argument;
        private readonly UnaryOpType _type;

        #endregion

        #region Constructors

        internal QueryUnaryExpression([NotNull]IExpression argument, UnaryOpType type)
        {
            _argument = argument;
            _type = type;
        }

        #endregion

        #region Overrides

        protected override object ToJSON()
        {
            var obj = new List<object>();
            switch (_type) {
                case UnaryOpType.Missing:
                case UnaryOpType.Null:
                    obj.Add("IS");
                    obj.Add(_type == UnaryOpType.Null ? null : MissingValue );
                    break;
                case UnaryOpType.NotMissing:
                case UnaryOpType.NotNull:
                    obj.Add("IS NOT");
                    obj.Add(_type == UnaryOpType.NotNull ? null : MissingValue );
                    break;
            }

            var operand = Misc.TryCast<IExpression, QueryExpression>(_argument);

            if ((operand as QueryTypeExpression)?.ExpressionType == ExpressionType.Aggregate) {
                obj.InsertRange(1, operand.ConvertToJSON() as IList<object>);
            } else {
                obj.Insert(1, operand.ConvertToJSON());
            }



            return obj;
        }

        #endregion
    }
}
