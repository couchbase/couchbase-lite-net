//
//  GenericEqualityComparer.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// A simple equality comparer that just calls default functions
    /// </summary>
    [Serializable]
    public sealed class GenericEqualityComparer <T> : EqualityComparer <T> where T : IEquatable <T> {

        #pragma warning disable 1591

        public override int GetHashCode (T obj)
        {
            if (obj == null) {
                return 0;
            }

            return obj.GetHashCode ();
        }

        public override bool Equals (T x, T y)
        {
            if (x == null) {
                return y == null;
            }

            return x.Equals (y);
        }

        #pragma warning restore 1591
    }
}

