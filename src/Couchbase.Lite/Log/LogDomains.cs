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

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// Contains all the available logging domains for the library,
    /// along with some functions to easily manipulate their verbosity
    /// </summary>
    public sealed class LogDomains
    {

        #region Private Variables

        private readonly LogTo _source;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the logging interface for database logging, which is responsible
        /// for logging activity between the library and the disk, including creation
        /// of Documents / Revisions, disk I/O, etc
        /// </summary>
        public IDomainLogging Database
        {
            get {
                return _source.Database;
            }
        }

        /// <summary>
        /// Gets the logging interface for query logging, which is responsible for
        /// logging information about in progress queries on data.
        /// </summary>
        public IDomainLogging Query
        { 
            get { 
                return _source.Query;
            }
        }

        /// <summary>
        /// Gets the logging interface for view logging, which is responsible for
        /// logging information about constructed views on data.
        /// </summary>
        public IDomainLogging View 
        { 
            get { 
                return _source.View; 
            } 
        }

        /// <summary>
        /// Gets the logging interface for router logging, which is responsible for
        /// logging information about the routing logic in the listener component
        /// (i.e. REST API provider for P2P)
        /// </summary>
        public IDomainLogging Router 
        { 
            get { 
                return _source.Router;
            }
        }

        /// <summary>
        /// Gets the logging interface for sync logging, which is responsible for
        /// logging activity between the library and remote (network) endpoints.
        /// </summary>
        public IDomainLogging Sync 
        {
            get { 
                return _source.Sync;
            }
        }

        /// <summary>
        /// Gets the logging interface for change tracker logging, which is responsible
        /// for logging information about the change tracker portion of a pull replication
        /// (where the incoming changes are received and parsed)
        /// </summary>
        public IDomainLogging ChangeTracker 
        { 
            get { 
                return _source.ChangeTracker;
            }
        }

        /// <summary>
        /// Gets the logging interface for validation logging, which is responsible
        /// for logging information about revision validation (a user-defined
        /// function)
        /// </summary>
        public IDomainLogging Validation 
        { 
            get { 
                return _source.Validation;
            }
        }

        /// <summary>
        /// Gets the logging interface for upgrade logging, which is responsible
        /// for logging information about local database upgrades (when a new
        /// version of Couchbase Lite changes the database layout in a way that
        /// is not backwards compatible)
        /// </summary>
        public IDomainLogging Upgrade 
        { 
            get { 
                return _source.Upgrade;
            }
        }

        /// <summary>
        /// Gets the logging interface for listener logging, which is responsible
        /// for logging information about the P2P REST API listener (connections,
        /// authorization, non-routing logic)
        /// </summary>
        public IDomainLogging Listener
        {
            get { 
                return _source.Listener;
            }
        }

        /// <summary>
        /// Gets the logging interface for discovery logging, which is responsible
        /// for logging information about P2P discovery.
        /// </summary>
        public IDomainLogging Discovery
        {
            get {
                return _source.Discovery;
            }
        }

        /// <summary>
        /// Gets the logging interface for task scheduling, which is responsible
        /// for logging information about scheduling tasks in task schedulers.
        /// </summary>
        public IDomainLogging TaskScheduling
        {
            get {
                return _source.TaskScheduling;
            }
        }

        /// <summary>
        /// Gets all the logging interfaces so logic can be applied to
        /// all of them
        /// </summary>
        public IDomainLogging All
        {
            get {
                return GetAll();
            }
        }

        #endregion

        #region Constructors

        internal LogDomains(LogTo source)
        {
            _source = source;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Groups the specified loggers together to apply the given logic
        /// to all of them at once.  For example:
        /// 
        /// <code>
        /// Log.Domains.Group(Log.Domains.Listener, Log.Domains.Discovery).Level = Log.LogLevel.Verbose;
        /// </code>
        /// </summary>
        /// <param name="loggers">The loggers to apply the logic to.</param>
        public IDomainLogging Group(params IDomainLogging[] loggers)
        {
            return new LogGroup(loggers);
        }

        /// <summary>
        /// Gets all of the logging interfaces except the ones specified
        /// so that logic can be applied to all of them
        /// </summary>
        /// <param name="loggers">The loggers to exclude</param>
        public IDomainLogging Except(params IDomainLogging[] loggers)
        {
            var exclusiveList = from logger in GetAll()
                                         where !loggers.Contains(logger)
                                         select logger;

            return new LogGroup(exclusiveList.ToArray());
        }

        #endregion

        #region Private Methods

        private LogGroup GetAll()
        {
            return new LogGroup(Database, Query, View, Router, Sync, ChangeTracker,
                Validation, Upgrade, Listener, Discovery, TaskScheduling);
        }

        #endregion
    }
}

