// 
//  BasicAuthenticator.cs
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

using System.Diagnostics;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// An object that will authenticate a <see cref="Replicator"/> using
    /// HTTP Basic authentication
    /// </summary>
    public sealed class BasicAuthenticator : Authenticator
    {
        #region Constants

        private const string Tag = nameof(BasicAuthenticator);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the username that this object holds
        /// </summary>
        [NotNull]
        public string Password { get; }

        /// <summary>
        /// Gets the password that this object holds
        /// </summary>
        [NotNull]
        public string Username { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="username">The username to send through HTTP Basic authentication</param>
        /// <param name="password">The password to send through HTTP Basic authentication</param>
        public BasicAuthenticator([NotNull]string username, [NotNull]string password)
        {
            Username = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(username), username);
            Password = CBDebug.MustNotBeNull(WriteLog.To.Sync, Tag, nameof(password), password);
        }

        #endregion

        #region Overrides

        internal override void Authenticate([NotNull]ReplicatorOptionsDictionary options)
        {
            Debug.Assert(options != null);

            var authDict = new AuthOptionsDictionary
            {
                Username = Username,
                Password = Password,
                Type = AuthType.HttpBasic
            };

            options.Auth = authDict;
        }

        #endregion
    }
}
