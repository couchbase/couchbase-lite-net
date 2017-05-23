// 
// IQueryRow.cs
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
using Couchbase.Lite.Internal.Serialization;
using LiteCore.Interop;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing an entry of a result set for
    /// a plain value query
    /// </summary>
    public interface IQueryRow
    {
        #region Properties

        /// <summary>
        /// Gets the document that was used for creating this
        /// entry
        /// </summary>
        Document Document { get; }

        /// <summary>
        /// Gets the ID of the document that was used for 
        /// creating this entry
        /// </summary>
        string DocumentID { get; }

        /// <summary>
        /// Gets the sequence of the document that was used for 
        /// creating this entry
        /// </summary>
        ulong Sequence { get; }

        #endregion

        #region Public Methods

        bool GetBoolean(int index);

        DateTimeOffset GetDate(int index);

        double GetDouble(int index);

        int GetInt(int index);

        long GetLong(int index);

        object GetObject(int index);

        string GetString(int index);

        #endregion
    }
}
