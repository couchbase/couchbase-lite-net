//
//  LazyJsonArray.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Views
{
    internal class LazyJsonArray : IEnumerable<object>
    {
        private readonly IEnumerable<object> _source;

        public LazyJsonArray(IEnumerable<object> source)
        {
            _source = source;
        }

        #region IEnumerable implementation

        public IEnumerator<object> GetEnumerator()
        {
            var e = _source.GetEnumerator();
            while (e.MoveNext()) {
                var jsonBytes = e.Current as byte[];
                if(jsonBytes != null) {
                    yield return Manager.GetObjectMapper().ReadValue<object>(jsonBytes);
                } else {
                    yield return e.Current;
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<object>)this).GetEnumerator();
        }

        #endregion
    }
}

