// 
// EncryptionKeyFactory.cs
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
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite
{
    /// <summary>
    /// A factory for creating <see cref="IEncryptionKey"/> instances
    /// </summary>
    public static class EncryptionKeyFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates a new <see cref="IEncryptionKey"/> instance using a <see cref="String"/> based password
        /// </summary>
        /// <param name="password">The password to derive the key from</param>
        /// <returns>An instantiated <see cref="IEncryptionKey"/> object</returns>
        public static IEncryptionKey Create(string password)
        {
            return new SymmetricKey(password);
        }

        /// <summary>
        /// Creates a new <see cref="IEncryptionKey"/> instance using preexisting derived data
        /// </summary>
        /// <param name="derivedBytes">The pre-derived key data</param>
        /// <returns>An instantiated <see cref="IEncryptionKey"/> object</returns>
        public static IEncryptionKey Create(IEnumerable<byte> derivedBytes)
        {
            return new SymmetricKey(derivedBytes.ToArray());
        }

        #endregion
    }
}
