// 
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
//using Couchbase.Lite;
using Sharpen;

namespace Couchbase.Lite
{
    /// <summary>Portable java wrapper around a "system specific" context.</summary>
    /// <remarks>
    /// Portable java wrapper around a "system specific" context.  The main implementation wraps an
    /// Android context.
    /// The wrapper is needed so that there are no compile time dependencies on Android classes
    /// within the Couchbase Lite java core library.
    /// This also has the nice side effect of having a single place to see exactly what parts of the
    /// Android context are being used.
    /// </remarks>
    public interface Context
    {
        /// <summary>The files dir.</summary>
        /// <remarks>The files dir.  On Android implementation, simply proxies call to underlying Context
        ///     </remarks>
        FilePath GetFilesDir();

        /// <summary>
        /// Override the default behavior and set your own NetworkReachabilityManager subclass,
        /// which allows you to completely control how to respond to network reachability changes
        /// in your app affects the replicators that are listening for change events.
        /// </summary>
        /// <remarks>
        /// Override the default behavior and set your own NetworkReachabilityManager subclass,
        /// which allows you to completely control how to respond to network reachability changes
        /// in your app affects the replicators that are listening for change events.
        /// </remarks>
        void SetNetworkReachabilityManager(NetworkReachabilityManager networkReachabilityManager
            );

        /// <summary>
        /// Replicators call this to get the NetworkReachabilityManager, and they register/unregister
        /// themselves to receive network reachability callbacks.
        /// </summary>
        /// <remarks>
        /// Replicators call this to get the NetworkReachabilityManager, and they register/unregister
        /// themselves to receive network reachability callbacks.
        /// If setNetworkReachabilityManager() was called prior to this, that instance will be used.
        /// Otherwise, the context will create a new default reachability manager and return that.
        /// </remarks>
        NetworkReachabilityManager GetNetworkReachabilityManager();
    }
}
