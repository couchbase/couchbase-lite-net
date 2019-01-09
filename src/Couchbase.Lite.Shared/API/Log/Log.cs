// 
//  Log.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// The class that stores the three available logging facilities in Couchbase Lite
    /// </summary>
    public sealed class Log
    {
        #region Properties

        /// <summary>
        /// Gets the logging facility that logs to a debugging console
        /// </summary>
        [NotNull]
        public IConsoleLogger Console { get; internal set; }

        /// <summary>
        /// Gets or sets the user defined logging facility
        /// </summary>
        public ILogger Custom { get; set; }

        /// <summary>
        /// Gets the logging facility that logs to files on the disk
        /// </summary>
        [NotNull]
        public FileLogger File { get; } = new FileLogger();

        #endregion
    }
}
