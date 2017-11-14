// 
// Conflict.cs
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
namespace Couchbase.Lite
{
    /// <summary>
    /// A class representing a conflict situation.  A conflict occurs as part of a distributed system
    /// where two offline nodes modify the same data at the same time.  This class serves to give
    /// information on such situations so that they can be resolved correctly.
    /// </summary>
    public sealed class Conflict
    {
        #region Properties

        /// <summary>
        /// Gets the state of the document before any edits were made
        /// </summary>
        public Document Base { get; }

        /// <summary>
        /// Gets the version of the document that is already existing
        /// </summary>
        public Document Mine { get; }

        /// <summary>
        /// Gets the version of the document that is attempting to be
        /// written but cannot due to an existing version
        /// </summary>
        public Document Theirs { get; }

        #endregion

        #region Constructors

        internal Conflict(Document mine, Document theirs, Document @base)
        {
            Mine = mine;
            Theirs = theirs;
            Base = @base;
        }

        #endregion
    }
}
