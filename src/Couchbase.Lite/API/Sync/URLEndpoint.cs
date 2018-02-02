// 
//  URLEndpoint.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// Represents a remote endpoint for a <see cref="Replicator"/>
    /// </summary>
    public sealed class URLEndpoint : IEndpoint
    {
        #region Constants

        private const string Tag = nameof(URLEndpoint);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the URL used to populate this endpoint
        /// </summary>
        [NotNull]
        public Uri Url { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an endpoint given a url.  Note that the scheme must be ws or wss
        /// or an exception will be thrown
        /// </summary>
        /// <param name="url">The url </param>
        /// <exception cref="ArgumentException">Thrown if the url scheme is not ws or wss</exception>
        public URLEndpoint([NotNull]Uri url)
        {
            var urlToUse = CBDebug.MustNotBeNull(Log.To.Sync, Tag, "url", url);
            if (!urlToUse.Scheme.StartsWith("ws")) {
                throw new ArgumentException($"Invalid scheme for URLEndpoint url ({urlToUse.Scheme}); must be either ws or wss");
            }

            Url = url;
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override string ToString() => Url.ToString();

        #endregion
    }
}