//
// LogDomains.cs
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
    /// Contains all the available logging domains for the library.  Each log domain writes messages
    /// regarding a specific area of Couchbase Lite operation.
    /// </summary>
	[Flags]
    public enum LogDomain
    {
        #region Properties

        /// <summary>
        /// A default value meaning "no domains"
        /// </summary>
        None = 0,

        /// <summary>
        /// Gets all the logging interfaces so logic can be applied to
        /// all of them
        /// </summary>
        All = Couchbase | Database | Query | Replicator | Network,

		/// <summary>
		/// Internal Use Only, has no effect from the outside
		/// </summary>
		Couchbase = 1 << 0,

        /// <summary>
        /// Gets the logging domain for database logging, which is responsible
        /// for logging activity between the library and the disk, including creation
        /// of Documents / Revisions, disk I/O, etc
        /// </summary>
        Database = 1 << 1,

		/// <summary>
		/// Gets the logging domain for query logging, which is responsible for
		/// logging information about in progress queries on data.
		/// </summary>
		Query = 1 << 2,

		/// <summary>
		/// Gets the logging domain for sync logging, which is responsible for
		/// logging activity between the library and remote (network) endpoints.
		/// </summary>
		Replicator = 1 << 3,

        /// <summary>
        /// Gest the logging domain for network related logging (web socket connections,
        /// BLIP protocol, etc)
        /// </summary>
        Network = 1 << 4

        #endregion
    }

	/// <summary>
	/// Defines the Couchbase Lite log verbosity levels.  The default level is
	/// <see cref="Warning"/>.
	/// </summary>
	public enum LogLevel
	{
		/// <summary>
		/// Debug log messages, only present in debug builds.  Information useful for
		/// development.
		/// </summary>
		Debug = C4LogLevel.Debug,
		/// <summary>
		/// Verbose log messages.  Additional information to help track down a problem,
		/// but noisy in every day use.
		/// </summary>
		Verbose = C4LogLevel.Verbose,
		/// <summary>
		/// Informational log messages.  Standard messages that indicate what is happening.
		/// </summary>
		Info = C4LogLevel.Info,
		/// <summary>
		/// Warning log messages, useful to look at if a problem is detected, but not necessarily
		/// indicative of a problem on their own.
		/// </summary>
		Warning = C4LogLevel.Warning,
		/// <summary>
		/// Error log messages.  These indicate immediate errors that need to be addressed.
		/// </summary>
		Error = C4LogLevel.Error,
		/// <summary>
		/// Log level for disabling a given domain
		/// </summary>
		None = C4LogLevel.None
	}
}

