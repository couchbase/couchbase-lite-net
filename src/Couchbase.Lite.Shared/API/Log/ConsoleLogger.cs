// 
//  ConsoleLogger.cs
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
    /// An interface for an object that will log messages to the
    /// relevant debug console.  For desktop this is the terminal,
    /// UWP uses the visual studio debug output pane, iOS uses NSLog
    /// and Android uses logcat.
    /// </summary>
    public interface IConsoleLogger : ILogger
    {
        #region Properties

        /// <summary>
        /// Gets or sets the domains that this logger will output
        /// </summary>
        LogDomain Domains { get; set; }

        /// <summary>
        /// Overrides the <see cref="ILogger"/> Level property
        /// with a public setter.
        /// </summary>
        new LogLevel Level { get; set; }

        #endregion
    }
}
