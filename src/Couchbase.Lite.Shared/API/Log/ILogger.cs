// 
//  ILogger.cs
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
namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// An interface for implementing a class that can accept
    /// logging messages from Couchbase Lite
    /// </summary>
    public interface ILogger
    {
        #region Properties

        /// <summary>
        /// Gets the level that the logger is currently
        /// logging
        /// </summary>
        LogLevel Level { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Performs the actual logging to the log storage
        /// </summary>
        /// <param name="level">The level of the message being logged</param>
        /// <param name="domain">The domain of the message being logged</param>
        /// <param name="message">The content of the message being logged</param>
        void Log(LogLevel level, LogDomain domain, string message);

        #endregion
    }
}
