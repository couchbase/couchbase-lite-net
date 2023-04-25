﻿// 
//  NullResultSet.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections;
using System.Collections.Generic;

using Couchbase.Lite.Query;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class NullResultSet : IResultSet, IEnumerator<Result>
    {
        #region Properties

        public int Count { get; } = 0;

        object? IEnumerator.Current => Current;

        public Result Current => throw new InvalidOperationException("NullResultSet cannot be iterated, it is empty");

        #endregion

        #region IDisposable

        public void Dispose()
        {
            
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IEnumerable<Result>

        public IEnumerator<Result> GetEnumerator() => this;

        #endregion

        #region IEnumerator

        public bool MoveNext() => false;

        public void Reset()
        {
            
        }

        #endregion

        #region IResultSet

        public List<Result> AllResults() => new List<Result>();

        #endregion
    }
}