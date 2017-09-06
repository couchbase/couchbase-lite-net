//
// LogDomains.cs
//
// Author:
//  Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using LiteCore.Interop;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// Contains all the available logging domains for the library
    /// </summary>
	[Flags]
    public enum LogDomain
    {
        #region Properties

        /// <summary>
        /// Gets all the logging interfaces so logic can be applied to
        /// all of them
        /// </summary>
        All = Couchbase | Database | LiteCore | Query | Replicator,

		/// <summary>
		/// Gets the logging domain for overall information that doesn't fit into
		/// a more specific category, and is not generated from the native LiteCore
		/// module.
		/// </summary>
		Couchbase = 1 << 0,

        /// <summary>
        /// Gets the logging domain for database logging, which is responsible
        /// for logging activity between the library and the disk, including creation
        /// of Documents / Revisions, disk I/O, etc
        /// </summary>
        Database = 1 << 1,

		/// <summary>
		/// Gets the logging domain for the LiteCore logging, which is responsible
		/// for messages sent up from the native LiteCore module
		/// </summary>
		LiteCore = 1 << 2,

		/// <summary>
		/// Gets the logging domain for query logging, which is responsible for
		/// logging information about in progress queries on data.
		/// </summary>
		Query = 1 << 3,

		/// <summary>
		/// Gets the logging domain for sync logging, which is responsible for
		/// logging activity between the library and remote (network) endpoints.
		/// </summary>
		Replicator = 1 << 4

        #endregion
    }

	public enum LogLevel
	{
		Debug = C4LogLevel.Debug,
		Verbose = C4LogLevel.Verbose,
		Info = C4LogLevel.Info,
		Warning = C4LogLevel.Warning,
		Error = C4LogLevel.Error,
		None = C4LogLevel.None
	}
}

