// 
// IArray.cs
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

namespace Couchbase.Lite
{
    public interface IArray : IReadOnlyArray, IArrayFragment
    {
        #region Properties

        new Fragment this[int index] { get; }

        #endregion

        #region Public Methods

        ArrayObject Add(object value);

        new ArrayObject GetArray(int index);

        new Subdocument GetSubdocument(int index);

        ArrayObject Insert(int index, object value);

        ArrayObject RemoveAt(int index);

        ArrayObject Set(IList array);

        ArrayObject Set(int index, object value);

        #endregion
    }
}
