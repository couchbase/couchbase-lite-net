// 
// QueryResultsArray.cs
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
using Couchbase.Lite.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class QueryResultsArray : IReadOnlyList<Result>
    {
        #region Variables

        [NotNull]private readonly QueryResultSet _rs;

        #endregion

        #region Properties

        public int Count { get; }

        public Result this[int index] => _rs[index];

        #endregion

        #region Constructors

        public QueryResultsArray([NotNull]QueryResultSet resultSet, int count)
        {
            _rs = resultSet;
            Count = count;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"{GetType().Name}[{Count}] rows";
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<IResult>

        public IEnumerator<Result> GetEnumerator()
        {
            return new Enumerator(_rs);
        }

        #endregion

        #region Nested

        // HACK to still be able to iterate (the original enumerator will not work
        // alongside random access)
        private sealed class Enumerator : IEnumerator<Result>
        {
            #region Variables

            [NotNull]private readonly QueryResultSet _parent;
            private int _pos = -1;

            #endregion

            #region Properties

            public Result Current => _parent[_pos];

            object IEnumerator.Current => Current;

            #endregion

            #region Constructors

            public Enumerator([NotNull]QueryResultSet parent)
            {
                _parent = parent;
            }

            #endregion

            #region IDisposable

            public void Dispose()
            {
                
            }

            #endregion

            #region IEnumerator

            public bool MoveNext()
            {
                if (_pos >= _parent.Count - 1) {
                    return false;
                }

                _pos++;
                return true;
            }

            public void Reset()
            {
                _pos = -1;
            }

            #endregion
        }

        #endregion
    }
}