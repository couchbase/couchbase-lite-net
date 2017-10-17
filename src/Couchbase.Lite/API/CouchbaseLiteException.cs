// 
// CouchbaseLiteException.cs
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

namespace Couchbase.Lite
{
    /// <summary>
    /// Indicates an exception that happened in the platform level of Couchbase Lite (i.e.
    /// not at the LiteCore level)
    /// </summary>
    public sealed class CouchbaseLiteException : Exception
    {
        #region Properties

        /// <summary>
        /// Gets the status code of what went wrong
        /// </summary>
        public StatusCode Status { get; }

        #endregion

        #region Constructors

        internal CouchbaseLiteException(StatusCode status)
            : this(status, String.Empty)
        {
               
        }

        internal CouchbaseLiteException(StatusCode status, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Status = status;
        }

        #endregion
    }
}
