// 
// SubdocumentFactory.cs
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

using Couchbase.Lite.DB;

namespace Couchbase.Lite
{
    /// <summary>
    /// A factory class for creating <see cref="ISubdocument"/> instances
    /// </summary>
    public static class SubdocumentFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates a new blank <see cref="ISubdocument"/>
        /// </summary>
        /// <returns>A constructed <see cref="ISubdocument"/> object</returns>
        public static ISubdocument Create()
        {
            return new Subdocument();
        }

        /// <summary>
        /// Creates a new <see cref="ISubdocument"/> using the given instance as a model
        /// </summary>
        /// <param name="other">The <see cref="ISubdocument"/> to use as a model for the new one</param>
        /// <returns>A constructed <see cref="ISubdocument"/> object</returns>
        public static ISubdocument Create(ISubdocument other)
        {
            var o = new Subdocument {
                Properties = other.Properties
            };

            return o;
        }

        #endregion
    }
}
