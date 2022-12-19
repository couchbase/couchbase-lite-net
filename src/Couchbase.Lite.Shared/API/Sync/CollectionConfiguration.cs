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

using Couchbase.Lite.Info;
using Couchbase.Lite.Support;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;

namespace Couchbase.Lite.Sync
{
    public sealed class CollectionConfiguration
    {
        #region Constants

        private const string Tag = nameof(CollectionConfiguration);

        #endregion

        #region Variables

        [NotNull] private readonly Freezer _freezer = new Freezer();
        private IConflictResolver _resolver = Lite.ConflictResolver.Default;
        private Func<Document, DocumentFlags, bool> _pushFilter;
        private Func<Document, DocumentFlags, bool> _pullValidator;
        internal ReplicatorType _replicatorType = ReplicatorType.PushAndPull;

        #endregion

        #region Properties

        /// <summary>
        /// The implemented custom conflict resolver object can be registered to the replicator 
        /// at ConflictResolver property. 
        /// When the value is null, the default conflict resolution will be applied.
        /// The default value is <see cref="ConflictResolver.Default" />. 
        /// </summary>
        [CanBeNull]
        public IConflictResolver ConflictResolver
        {
            get => _resolver;
            set 
            { 
                if(value == null) 
                    _freezer.PerformAction(() => _resolver = Lite.ConflictResolver.Default);
                else
                    _freezer.PerformAction(() => _resolver = value); 
            }
        }

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document pull will be allowed if output is true, othewise, Document pull 
        /// will not be allowed
        /// </summary>
        [CanBeNull]
        public Func<Document, DocumentFlags, bool> PullFilter
        {
            get => _pullValidator;
            set => _freezer.PerformAction(() => _pullValidator = value);
        }

        /// <summary>
        /// Func delegate that takes Document input parameter and bool output parameter
        /// Document push will be allowed if output is true, othewise, Document push 
        /// will not be allowed
        /// </summary>
        [CanBeNull]
        public Func<Document, DocumentFlags, bool> PushFilter
        {
            get => _pushFilter;
            set => _freezer.PerformAction(() => _pushFilter = value);
        }

        /// <summary>
        /// A set of Sync Gateway channel names to pull from.  Ignored for push replicatoin.
        /// The default value is null, meaning that all accessible channels will be pulled.
        /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
        /// </summary>
        [CanBeNull]
        public IList<string> Channels
        {
            get => Options.Channels;
            set => _freezer.PerformAction(() => Options.Channels = value);
        }

        /// <summary>
        /// A set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
        /// and/or pulled
        /// </summary>
        [CanBeNull]
        public IList<string> DocumentIDs
        {
            get => Options.DocIDs;
            set => _freezer.PerformAction(() => Options.DocIDs = value);
        }

        /// <summary>
        /// A value indicating the direction of the replication.  The default is
        /// <see cref="ReplicatorType.PushAndPull"/> which is bidirectional
        /// </summary>
        internal ReplicatorType ReplicatorType
        {
            get => _replicatorType;
            set => _freezer.SetValue(ref _replicatorType, value);
        }

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

        #endregion

        #region Constructor

        public CollectionConfiguration() {}

        internal CollectionConfiguration(CollectionConfiguration copy)
        {
            PushFilter = copy?.PushFilter;
            PullFilter = copy?.PullFilter;
            ConflictResolver = copy?.ConflictResolver;
            ReplicatorType = copy.ReplicatorType;
            Options = copy?.Options;
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal CollectionConfiguration Freeze()
        {
            var retVal = new CollectionConfiguration()
            {
                PushFilter = PushFilter,
                PullFilter = PullFilter,
                ConflictResolver = ConflictResolver,
                ReplicatorType = ReplicatorType,
                Options = Options
            };

            retVal._freezer.Freeze("Cannot modify a CollectionConfiguration that is in use");
            return retVal;
        }

        #endregion
    }
}
