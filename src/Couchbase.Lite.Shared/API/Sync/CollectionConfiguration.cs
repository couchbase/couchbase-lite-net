// 
//  CollectionConfiguration.cs
// 
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Support;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Sync
{
    /// <summary>
    /// A configuration object for setting the details of how to treat
    /// a collection when used inside of a <see cref="Replicator"/>
    /// </summary>
    public sealed class CollectionConfiguration
    {
        #region Constants

        private const string Tag = nameof(CollectionConfiguration);

        #endregion

        #region Properties

        /// <summary>
        /// The implemented custom conflict resolver object can be registered to the replicator 
        /// at ConflictResolver property. 
        /// When the value is null, the default conflict resolution will be applied.
        /// The default value is <see cref="ConflictResolver.Default" />. 
        /// </summary>
        public IConflictResolver? ConflictResolver { get; init; } = Lite.ConflictResolver.Default;

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document pull will be allowed if output is true, othewise, Document pull 
        /// will not be allowed
        /// </summary>
        public Func<Document, DocumentFlags, bool>? PullFilter { get; init; }

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document push will be allowed if output is true, othewise, Document push 
        /// will not be allowed
        /// </summary>
        public Func<Document, DocumentFlags, bool>? PushFilter { get; init; }

        /// <summary>
        /// A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Zero length lists are not allowed, and will be replaced with null
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        /// <remarks>
        /// Note: Channels property is only applicable in the replications with Sync Gateway. 
        /// </remarks>
        public IList<string>? Channels
        {
            get => Options.Channels;
            init => Options.Channels = value?.Any() == true ? value : null;
        }

        /// <summary>
        /// A set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
        /// and/or pulled.  Zero length lists are not allowed, and will be replaced with null
        /// </summary>
        public IList<string>? DocumentIDs
        {
            get => Options.DocIDs;
            init => Options.DocIDs = value?.Any() == true ? value : null;
        }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        internal ReplicatorType ReplicatorType { get; set; } = ReplicatorType.PushAndPull;

        internal ReplicatorOptionsDictionary Options { get; } = new();

        #endregion

        #region Constructor

        /// <summary>
        /// The default constructor
        /// </summary>
        public CollectionConfiguration() {}

        internal CollectionConfiguration(CollectionConfiguration copy)
        {
            PushFilter = copy?.PushFilter;
            PullFilter = copy?.PullFilter;
            ConflictResolver = copy?.ConflictResolver;
            ReplicatorType = copy?.ReplicatorType ?? ReplicatorType.PushAndPull;
            Options = copy?.Options ?? new ReplicatorOptionsDictionary();
        }

        #endregion
    }
}
