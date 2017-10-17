// 
// FleeceArray.cs
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

using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Lite.Internal.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class FleeceArray : IEnumerable<object>
    {
        #region Properties

        public FLArray* Array { get; }

        public Database Database { get; }

        #endregion

        #region Constructors

        public FleeceArray(FLArray* array, Database database)
        {
            Array = array;
            Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable<object>

        public IEnumerator<object> GetEnumerator()
        {
            return new Enumerator(this);
        }

        #endregion

        #region Nested

        private class Enumerator : IEnumerator<object>
        {
            #region Variables

            private readonly FleeceArray _parent;
            private bool _first;
            private readonly bool _empty;
            private FLArrayIterator _iter;

            #endregion

            #region Properties

            public object Current
            {
                get {
                    fixed (FLArrayIterator* i = &_iter) {
                        var val = Native.FLArrayIterator_GetValue(i);
                        return FLValueConverter.ToCouchbaseObject(val, _parent.Database, true);
                    }
                }
            }

            object IEnumerator.Current => Current;

            #endregion

            #region Constructors

            public Enumerator(FleeceArray parent)
            {
                _first = true;
                _parent = parent;
                if (Native.FLArray_Count(_parent.Array) == 0) {
                    _empty = true;
                }
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
                if (_empty) {
                    return false;
                }

                if (_first) {
                    _first = false;
                    fixed (FLArrayIterator* i = &_iter) {
                        Native.FLArrayIterator_Begin(_parent.Array, i);
                    }

                    return true;
                }

                fixed (FLArrayIterator* i = &_iter) {
                    return Native.FLArrayIterator_Next(i);
                }
            }

            public void Reset()
            {
                _first = true;
            }

            #endregion
        }

        #endregion
    }
}
