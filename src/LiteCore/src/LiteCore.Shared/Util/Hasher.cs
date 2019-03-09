// 
// Hasher.cs
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LiteCore.Util
{
    // https://stackoverflow.com/a/18613926/1155387
    [ExcludeFromCodeCoverage]
    internal struct Hasher
    {
        private int _hashCode;

        public static readonly Hasher Start = new Hasher(17);

        public Hasher(int hashCode)
        {
            _hashCode = hashCode;
        }

        public Hasher Add<T>(T obj)
        {
            var h = EqualityComparer<T>.Default.GetHashCode(obj);
            _hashCode = _hashCode * 31 + h;
            return this;
        }

        public static implicit operator int(Hasher hasher) => hasher.GetHashCode();

        public override int GetHashCode() => _hashCode;

        public override bool Equals(object obj)
        {
            if (obj is Hasher other) {
                return _hashCode == other._hashCode;
            }

            return false;
        }
    }
}
