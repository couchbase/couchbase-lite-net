// 
// QueryTypeExpression.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Query;
using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal enum ExpressionType
    {
        Constant,
        KeyPath,
        Parameter,
        Variable,
        Aggregate,
    }

    internal sealed class QueryTypeExpression : QueryExpression, IPropertyExpression, IMetaExpression
    {
        #region Variables

        private readonly IList _subpredicates;
        private string _from;

        #endregion

        #region Properties

        internal object ConstantValue { get; set; }
        internal ExpressionType ExpressionType { get; }

        internal string KeyPath { get; }

        #endregion

        #region Constructors

        public QueryTypeExpression()
        {
            ExpressionType = ExpressionType.Constant;
        }

        public QueryTypeExpression(IList subpredicates)
        {
            ExpressionType = ExpressionType.Aggregate;
            _subpredicates = subpredicates;
        }

        public QueryTypeExpression(string keyPath, ExpressionType type)
        {
            Debug.Assert(type >= ExpressionType.KeyPath && type <= ExpressionType.Variable);
            ExpressionType = type;
            KeyPath = keyPath;
        }

        #endregion

        #region Private Methods

        private object CalculateKeyPath()
        {
            var op = ExpressionType == ExpressionType.Parameter
                ? '$'
                : ExpressionType == ExpressionType.Variable
                    ? '?'
                    : '.';

            if (KeyPath.StartsWith("rank(")) {
                return new object[] {"rank()", new[] {op.ToString(), KeyPath.Substring(5, KeyPath.Length - 6)}};
            }

            return _from != null ? new[] { $"{op}{_from}.{KeyPath}" } : new[] { $"{op}{KeyPath}" };
        }

        #endregion

        #region Overrides

        protected override object ToJSON()
        {
            switch (ExpressionType) {
                case ExpressionType.Constant:
                    return ConstantValue;
                case ExpressionType.KeyPath:
                case ExpressionType.Parameter:
                case ExpressionType.Variable:
                    return CalculateKeyPath();
                case ExpressionType.Aggregate:
                {
                    var obj = new List<object>();
                    foreach (var entry in _subpredicates) {
                        var queryExp = entry as QueryExpression;
                        obj.Add(queryExp == null ? entry.ToString() : queryExp.ConvertToJSON());
                    }

                    return obj;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(ConvertToJSON());
        }

        public IExpression From(string alias)
        {
            _from = alias;
            Reset();
            return this;
        }

        #endregion
    }
}
