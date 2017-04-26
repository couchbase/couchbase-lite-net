// 
// AggregateEnumerator.cs
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

namespace Couchbase.Lite.Util
{
    internal sealed class AggregateEnumerator<T> : IEnumerator<T>
    {
        #region Variables

        private readonly IEnumerator<T>[] _enumerators;
        private int _current = -1;

        #endregion

        #region Properties

        public T Current => _enumerators[_current].Current;

        object IEnumerator.Current => Current;

        #endregion

        #region Constructors

        public AggregateEnumerator(params IEnumerator<T>[] enumerators)
        {
            _enumerators = enumerators;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            foreach (var e in _enumerators) {
                e.Dispose();
            }
        }

        #endregion

        #region IEnumerator

        public bool MoveNext()
        {
            while (_current == -1 || !_enumerators[_current].MoveNext()) {
                _current++;
                if (_current >= _enumerators.Length) {
                    return false;
                }
            }

            return true;
        }

        public void Reset()
        {
            foreach (var e in _enumerators) {
                e.Reset();
            }
        }

        #endregion
    }
}
