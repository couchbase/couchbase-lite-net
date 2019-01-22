// 
// QueryTypeExpression.cs
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
using System.Linq;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using Newtonsoft.Json;

namespace Couchbase.Lite.Internal.Query
{
    internal enum ExpressionType
    {
        KeyPath,
        Parameter,
        Variable,
        Aggregate
    }

    internal sealed class QueryTypeExpression : QueryExpression, IPropertyExpression, IMetaExpression, IVariableExpression
    {
        #region Variables

        private readonly IList<IExpression> _subpredicates;
        private string _from;
        private string _columnName;

        #endregion

        #region Properties
        
        internal ExpressionType ExpressionType { get; }

        internal string KeyPath { get; }

        internal string ColumnName => _columnName ?? (_columnName = KeyPath.Split('.').Last());

        #endregion

        #region Constructors

        public QueryTypeExpression(IList<IExpression> subpredicates)
        {
            ExpressionType = ExpressionType.Aggregate;
            _subpredicates = subpredicates;
        }

        public QueryTypeExpression(string keyPath, ExpressionType type)
        {
            Debug.Assert(type >= ExpressionType.KeyPath && type <= ExpressionType.Variable,
                $"Cannot use this constructor for {type}");
            ExpressionType = type;
            KeyPath = keyPath;
        }

        public QueryTypeExpression(string keyPath, string columnName)
        {
            ExpressionType = ExpressionType.KeyPath;
            KeyPath = keyPath;
            _columnName = columnName;
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
                case ExpressionType.KeyPath:
                case ExpressionType.Parameter:
                case ExpressionType.Variable:
                    return CalculateKeyPath();
                case ExpressionType.Aggregate:
                {
                    var obj = new List<object>();
                    foreach (var entry in _subpredicates) {
                        var queryExp = Misc.TryCast<IExpression, QueryExpression>(entry);
                        obj.Add(queryExp.ConvertToJSON());
                    }

                    return obj;
                }
            }

            return null;
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
