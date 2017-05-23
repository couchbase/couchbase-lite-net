// 
// NonRemovedEnumerator.cs
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
    internal sealed class NonRemovedEnumerator : IEnumerator<KeyValuePair<string, object>>
    {
        #region Variables

        private readonly IEnumerator<KeyValuePair<string, object>> _source;

        #endregion

        #region Properties

        public KeyValuePair<string, object> Current => _source.Current;

        object IEnumerator.Current => Current;

        #endregion

        #region Constructors

        public NonRemovedEnumerator(IDictionary<string, object> source)
        {
            _source = source.GetEnumerator();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _source.Dispose();
        }

        #endregion

        #region IEnumerator

        public bool MoveNext()
        {
            var moved = false;
            do {
                moved = _source.MoveNext();
            } while (moved && ReferenceEquals(Current.Value, DictionaryObject.RemovedValue));

            return moved;
        }

        public void Reset()
        {
            _source.Reset();
        }

        #endregion
    }
}
