using Couchbase.Lite.Support;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite.Sync
{
    public sealed class CollectionConfiguration
    {
        #region Constants

        private const string Tag = nameof(CollectionConfiguration);

        #endregion

        #region Variables

        [NotNull] private readonly Freezer _freezer = new Freezer();
        private IConflictResolver _resolver;
        private Func<Document, DocumentFlags, bool> _pushFilter;
        private Func<Document, DocumentFlags, bool> _pullValidator;

        #endregion

        #region Properties

        /// <summary>
        /// The implemented custom conflict resolver object can be registered to the replicator 
        /// at ConflictResolver property. The default value of the conflictResolver is null. 
        /// When the value is null, the default conflict resolution will be applied.
        /// </summary>
        [CanBeNull]
        public IConflictResolver ConflictResolver
        {
            get => _resolver;
            set => _freezer.PerformAction(() => _resolver = value);
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

        [NotNull]
        internal ReplicatorOptionsDictionary Options { get; set; } = new ReplicatorOptionsDictionary();

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
                Options = Options
            };

            retVal._freezer.Freeze("Cannot modify a CollectionConfiguration that is in use");
            return retVal;
        }

        #endregion
    }
}
