//
//  IViewStoreDelegate.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System;

namespace Couchbase.Lite.Store
{

    /// <summary>
    /// Delegate of an IViewStore instance. View implements this.
    /// </summary>
    internal interface IViewStoreDelegate
    {

        /// <summary>
        /// The current map block. Never null.
        /// </summary>
        MapDelegate Map { get; }

        /// <summary>
        /// The current reduce block, or null if there is none.
        /// </summary>
        ReduceDelegate Reduce { get; }

        /// <summary>
        /// The current map version string. If this changes, the storage's SetVersion() method will be
        /// called to notify it, so it can invalidate the index.
        /// </summary>
        string MapVersion { get; }

        /// <summary>
        /// The document "type" property values this view is filtered to (null if none.)
        /// </summary>
        string DocumentType { get; }
    }
}


