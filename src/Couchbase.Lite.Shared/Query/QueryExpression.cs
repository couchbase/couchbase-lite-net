﻿// 
// QueryExpression.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

using Newtonsoft.Json;
using Debug = System.Diagnostics.Debug;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryExpression : IExpression
    {
        #region Constants

        protected static readonly string[] MissingValue = { "MISSING" };
        private const string Tag = nameof(QueryExpression);

        #endregion

        #region Variables

        private object _serialized;

        #endregion

        #region Public Methods

        public static object EncodeToJSON([NotNull]IList expressions)
        {
            Debug.Assert(expressions != null);
            return EncodeExpressions(expressions);
        }

        public IExpression Match([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Matches, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression NotIn([ItemNotNull]params IExpression[] expressions) => Expression.Negated(In(expressions));

        #endregion

        #region Protected Methods

        protected void Reset()
        {
            _serialized = null;
        }

        protected abstract object ToJSON();

        #endregion

        #region Internal Methods

        internal object ConvertToJSON()
        {
            return _serialized ?? (_serialized = ToJSON());
        }

        #endregion

        #region Private Methods

        private static IList EncodeExpressions(IList expressions)
        {
            var result = new List<object>();
            foreach (var r in expressions) {
                object jsonObj;
                var arr = r as IList;
                if (arr != null) {
                    jsonObj = arr;
                } else {
                    QueryExpression expr;
                    var str = r as string;
                    if (str != null) {
                        expr = new QueryTypeExpression(str, ExpressionType.KeyPath);
                    } else {
                        expr = r as QueryExpression;
                        if (expr == null) {
                            throw new InvalidOperationException(
                                String.Format(CouchbaseLiteErrorMessage.ExpressionsMustBeIExpressionOrString, "IExpression"));
                        }
                    }

                    jsonObj = expr.ConvertToJSON();
                }
                
                result.Add(jsonObj);
            }

            return result;
        }

        [NotNull]
        private QueryExpression GetBetweenExpression([NotNull]IExpression expression)
        {
            Debug.Assert(expression != null);
            switch (expression) {
                case QueryTypeExpression e:
                    return e;
                case QueryConstantExpressionBase e:
                    return e;
                default:
                    throw new ArgumentException(String.Format(CouchbaseLiteErrorMessage.InvalidExpressionValueBetween, 
                        expression.GetType().Name));
            }
        }

        [NotNull]
        private QueryExpression GetOperator(BinaryOpType type, IExpression expression)
        {
            var lhs = this;
            var rhs = Misc.TryCast<IExpression, QueryExpression>(expression);

            return new QueryBinaryExpression(lhs, rhs, type);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"[{GetType().Name}] => {JsonConvert.SerializeObject(ConvertToJSON())}";
        }

        #endregion

        #region IExpression

        public IExpression Add([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Add, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression And([NotNull]IExpression expression) => 
            new QueryCompoundExpression("AND", this, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression Between([NotNull]IExpression expression1, [NotNull]IExpression expression2)
        {
            if (!(this is QueryTypeExpression lhs)) {
                throw new NotSupportedException();
            }
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression1), expression1);
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression2), expression2);

            var exp1 = GetBetweenExpression(expression1);
            var exp2 = GetBetweenExpression(expression2);

            var rhs = new QueryTypeExpression(new[] { exp1, exp2 });
            return new QueryBinaryExpression(lhs, rhs, BinaryOpType.Between);
        }

        public IExpression Collate([NotNull]ICollation collation)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(collation), collation);
            var col = Misc.TryCast<ICollation, QueryCollation>(collation);
            col = new QueryCollation(col);
            col.SetOperand(this);
            return col;
        }

        public IExpression Divide([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Divide, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression EqualTo([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.EqualTo, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression GreaterThan([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.GreaterThan, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression GreaterThanOrEqualTo([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.GreaterThanOrEqualTo, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression In([ItemNotNull]params IExpression[] expressions)
        {
            if (!(this is QueryTypeExpression lhs)) {
                throw new NotSupportedException();
            }

            CBDebug.ItemsMustNotBeNull(WriteLog.To.Query, Tag, nameof(expressions), expressions);
            var rhs = new QueryTypeExpression(expressions);
            return new QueryBinaryExpression(lhs, rhs, BinaryOpType.In);
        }

        public IExpression Is([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Is, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression IsNot([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.IsNot, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        [Obsolete("This query expression deprecated, please use IsNotValued().")]
        public IExpression IsNullOrMissing() => new QueryUnaryExpression(this, UnaryOpType.Null)
            .Or(new QueryUnaryExpression(this, UnaryOpType.Missing));
        public IExpression IsNotValued() => Expression.Not(IsValued());
        public IExpression IsValued() => new QueryUnaryExpression(this, UnaryOpType.Valued);
        public IExpression LessThan([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.LessThan, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        public IExpression LessThanOrEqualTo([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.LessThanOrEqualTo, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        public IExpression Like([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Like, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        public IExpression Modulo([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Modulus, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        public IExpression Multiply([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Multiply, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        public IExpression NotEqualTo([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.NotEqualTo, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        [Obsolete("This query expression deprecated, please use IsValued().")]
        public IExpression NotNullOrMissing() => Expression.Not(IsNullOrMissing());
        public IExpression Or([NotNull]IExpression expression) => 
            new QueryCompoundExpression("OR", this, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        public IExpression Regex([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.RegexLike, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));
        public IExpression Subtract([NotNull]IExpression expression) => 
            GetOperator(BinaryOpType.Subtract, CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(expression), expression));

        #endregion
    }
}
