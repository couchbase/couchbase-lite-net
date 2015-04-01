//
// ManagerOptions.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    /// <summary>
    /// Option flags for Manager initialization.
    /// </summary>
    public sealed class ManagerOptions
    {
        /// <summary>
        /// The maximum number of times to retry
        /// network requests that failed due to
        /// transient network errors.
        /// </summary>
        /// <value>The max retries.</value>
        internal int MaxRetries { get; private set; }

        /// <summary>
        /// Gets the default option flags.
        /// </summary>
        /// <value>The default option flags.</value>
        public static ManagerOptions Default { get; private set; }

        static ManagerOptions()
        {
            Default = new ManagerOptions();
        }

        /// <summary>
        /// Provides configuration settings.
        /// </summary>
        public ManagerOptions()
        {
            MaxRetries = 10;

            MaxOpenHttpConnections = 16;

            MaxRevsToGetInBulk = 50;

            RequestTimeout = TimeSpan.FromSeconds(90);

            TaskScheduler scheduler = null;
            try {
                scheduler = TaskScheduler.FromCurrentSynchronizationContext ();
            } catch (InvalidOperationException) {
                // Running in the unit test runner will throw an exception.
                // Just swallow.
            } finally {
                CallbackScheduler =  scheduler ?? TaskScheduler.Current ?? TaskScheduler.Default;
            }
        }

        /// <summary>Gets or sets, whether changes to the database are disallowed.</summary>
        public Boolean ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the callback synchronization context.
        /// </summary>
        /// <value>The callback context.</value>
        public TaskScheduler CallbackScheduler { get; set; }

        /// <summary>
        /// Gets or sets the default network request timeout.
        /// </summary>
        /// <value>The request timeout. Defaults to 30 seconds.</value>
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Gets or sets max number of open Http Connections
        /// </summary>
        /// <value>Max number of connections</value>
        public int MaxOpenHttpConnections { get; set; }

        /// <summary>
        /// Get or sets the max revs to get in a bulk download
        /// </summary>
        /// <value>Max revs to get in bulk download</value>
        public int MaxRevsToGetInBulk { get; set; }
    }
}
