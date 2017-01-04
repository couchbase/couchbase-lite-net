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

#if NET_3_5
using System.Net.Couchbase;
#else
using System.Net;
#endif

namespace Couchbase.Lite
{
    /// <summary>
    /// Option flags for Manager initialization.
    /// </summary>
    public sealed class ManagerOptions
    {

        /// <summary>
        /// Sets the default replication options that are set on replications
        /// created from databases owned by this manager.  If not set, a new
        /// object will be created with default values (For the 1.x lifecycle
        /// this includes the relevant settings on ManagerOptions)
        /// </summary>
        /// <value>The default replication options.</value>
        public ReplicationOptions DefaultReplicationOptions { get; set; }

        /// <summary>
        /// The maximum number of times to retry
        /// network requests that failed due to
        /// transient network errors.
        /// </summary>
        [Obsolete("Moving to the ReplicationOptions class as RetryStrategy")]
        public int MaxRetries { get; set; }

        /// <summary>
        /// Gets the default option flags.
        /// </summary>
        /// <value>The default option flags.</value>
        public static ManagerOptions Default { get; private set; }

        //Make public in the future
        internal static IJsonSerializer SerializationEngine { get; set; }

        /// <summary>
        /// Gets or sets the values pertaining to serialization and
        /// deserialization
        /// </summary>
        public static JsonSerializationSettings SerializationSettings 
        {
            get { return SerializationEngine.Settings; }
            set { SerializationEngine.Settings = value; }
        }

        #if __IOS__

        /// <summary>
        /// Specify the file protection to be placed on database files
        /// created by this managed (iOS only)
        /// </summary>
        /// <value>The file protection.</value>
        public Foundation.NSDataWritingOptions FileProtection { get; set; }

        #endif

        static ManagerOptions()
        {
            SerializationEngine = new NewtonsoftJsonSerializer();
            Default = new ManagerOptions();
        }

        /// <summary>
        /// Provides configuration settings.
        /// </summary>
        public ManagerOptions()
        {
            RestoreDefaults();
        }

        internal void RestoreDefaults()
        {
            DefaultReplicationOptions = null; // To maintain backwards compatibility until 2.0

#pragma warning disable 618

            MaxRetries = 2;

            MaxOpenHttpConnections = 8;

            MaxRevsToGetInBulk = 50;

            RequestTimeout = TimeSpan.FromSeconds(60);

            DownloadAttachmentsOnSync = true;
#pragma warning restore 618

#if __UNITY__
            CallbackScheduler = Couchbase.Lite.Unity.UnityMainThreadScheduler.TaskScheduler;
#else
            TaskScheduler scheduler = null;
            try {
                scheduler = TaskScheduler.FromCurrentSynchronizationContext ();
            } catch (InvalidOperationException) {
                // Running in the unit test runner will throw an exception.
                // Just swallow.
            } finally {
                CallbackScheduler =  scheduler ?? TaskScheduler.Current ?? TaskScheduler.Default;
            }
            #endif

            ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 12;
        }
            
        /// <summary>Gets or sets, whether changes to databases are disallowed by default.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the callback synchronization context.
        /// </summary>
        /// <value>The callback context.</value>
        public TaskScheduler CallbackScheduler { get; set; }

        /// <summary>
        /// Gets or sets the default network request timeout.
        /// </summary>
        [Obsolete("Moving to the ReplicationOptions class")]
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Gets or sets max number of open Http Connections
        /// </summary>
        /// <value>Max number of connections</value>
        [Obsolete("Moving to the ReplicationOptions class")]
        public int MaxOpenHttpConnections { get; set; }

        /// <summary>
        /// Get or sets the max revs to get in a bulk download
        /// </summary>
        /// <value>Max revs to get in bulk download</value>
        [Obsolete("Moving to the ReplicationOptions class")]
        public int MaxRevsToGetInBulk { get; set; }

        /// <summary>
        /// Get or sets a flag to indicated when to request attachments
        /// </summary>
        /// <value>true - download attachments with documents, false - defer attachment downloading until later</value>
        public bool DownloadAttachmentsOnSync { get; set; }
#pragma warning disable 1591
        public override string ToString()
        {
            return String.Format("ManagerOptions[ReadOnly={0}, CallbackScheduler={1}, DefaultReplicationOptions={2}]", ReadOnly, CallbackScheduler.GetType().Name, DefaultReplicationOptions);
        }
#pragma warning restore 1591
    }
}
