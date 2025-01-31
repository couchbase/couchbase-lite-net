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

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using System;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// [DEPRECATED] The class that stores the three available logging facilities in Couchbase Lite
    /// </summary>
    [Obsolete("Use the new LogSinks static class")]
    public sealed class Log
    {
        private ILogger? _custom;

        #region Properties

        /// <summary>
        /// [DEPRECATED] Gets the logging facility that logs to a debugging console
        /// </summary>
        [Obsolete("Use LogSinks.Console instead")]
        public IConsoleLogger Console { get; } = Service.GetRequiredInstance<IConsoleLogger>();

        /// <summary>
        /// [DEPRECATED] Gets or sets the user defined logging facility
        /// </summary>
        [Obsolete("Use LogSinks.Custom instead")]
        public ILogger? Custom
        {
            get => _custom;
            set {
                DomainLogger.ThrowIfNewApiUsed();
                _custom = value;
            }
        }

        /// <summary>
        /// [DEPRECATED] Gets the logging facility that logs to files on the disk
        /// </summary>
        [Obsolete("Use LogSinks.File instead")]
        public FileLogger File { get; } = new FileLogger();

        internal LogLevel OverallLogLevel
        {
            get {
                var customLevel = Custom?.Level ?? LogLevel.None;

                return (LogLevel)Math.Min((int)File.Level, Math.Min((int)customLevel, (int)Console.Level));
            }
        }

        #endregion
    }
}
