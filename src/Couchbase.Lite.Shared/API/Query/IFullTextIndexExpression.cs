// 
//  IFullTextIndexExpression.cs
// 
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// Specifies an unambiguous full text index to use when building a query via QueryBuilder.
    /// </summary>
    public interface IFullTextIndexExpression : IIndexExpression
    {
        /// <summary>
        /// Specifies the data source of the index (i.e. collection).  The alias must
        /// match the alias of a data source in the query.
        /// </summary>
        /// <param name="alias">The alias of the data source in the query</param>
        /// <returns>An alias aware full text index expression for use in a QueryBuilder query</returns>
        IIndexExpression From(string alias);
    }

    internal sealed class FullTextIndexExpression : IFullTextIndexExpression
    {

        #region Variables

        private string? _alias;
        private readonly string _name;

        #endregion

        #region Constructors

        public FullTextIndexExpression(string name)
        {
            _name = name;
        }

        #endregion

        #region IFullTextIndexExpression

        public IIndexExpression From(string alias)
        {
            _alias = alias;
            return this;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            if (_alias != null) {
                return $"{_alias}.{_name}";
            }

            return _name;
        }

        #endregion
    }
}
