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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Couchbase.Lite.Sync;

/// <summary>
/// A configuration object for setting the details of how to treat
/// a collection when used inside a <see cref="Replicator"/>
/// </summary>
public sealed record CollectionConfiguration
{
    /// <summary>
    /// The collection that this configuration is going
    /// to be applied to
    /// </summary>
    public required Collection Collection { get; init; }
    
    /// <summary>
    /// The implemented custom conflict resolver object can be registered to the replicator 
    /// at ConflictResolver property. 
    /// When the value is null, the default conflict resolution will be applied.
    /// The default value is <see cref="ConflictResolver.Default" />. 
    /// </summary>
    public IConflictResolver? ConflictResolver { get; init; } = Lite.ConflictResolver.Default;

    /// <summary>
    /// Func delegate that takes Document input parameter and bool output parameter
    /// Document pull will be allowed if output is true, otherwise, Document pull 
    /// will not be allowed
    /// </summary>
    public Func<Document, DocumentFlags, bool>? PullFilter { get; init; }

    /// <summary>
    /// Func delegate that takes Document input parameter and bool output parameter
    /// Document push will be allowed if output is true, otherwise, Document push 
    /// will not be allowed
    /// </summary>
    public Func<Document, DocumentFlags, bool>? PushFilter { get; init; }

    /// <summary>
    /// A set of Sync Gateway channel names to pull from.  Ignored for push replication.
    /// The default value is null, meaning that all accessible channels will be pulled.
    /// Zero length lists are not allowed, and will be replaced with null
    /// Note: channels that are not accessible to the user will be ignored by Sync Gateway.
    /// </summary>
    /// <remarks>
    /// Note: Channels property is only applicable in the replications with Sync Gateway. 
    /// </remarks>
    public IImmutableList<string>? Channels
    {
        get => Options.Channels;
        init => Options.Channels = value?.Any() == true ? value : null;
    }

    /// <summary>
    /// A set of document IDs to filter by.  If not null, only documents with these IDs will be pushed
    /// and/or pulled.  Zero length lists are not allowed, and will be replaced with null
    /// </summary>
    public IImmutableList<string>? DocumentIDs
    {
        get => Options.DocIDs;
        init => Options.DocIDs = value?.Any() == true ? value : null;
    }

    internal ReplicatorOptionsDictionary Options { get; } = new();
    
    /// <summary>
    /// A convenience method to create a list of configurations for the provided collections
    /// when the default settings are acceptable.
    /// </summary>
    /// <param name="collections">The collections to create configurations for</param>
    /// <returns>The list of configuration options.</returns>
    public static List<CollectionConfiguration> FromCollections(params Collection[] collections) =>
        collections.Select(c => new CollectionConfiguration(c)).ToList();
    
    /// <summary>
    /// Convenience constructor
    /// </summary>
    /// <param name="collection">The collection to apply the configuration to</param>
    [SetsRequiredMembers]
    public CollectionConfiguration(Collection collection)
    {
        Collection = collection;
    }

    [SetsRequiredMembers]
    internal CollectionConfiguration(CollectionConfiguration other)
    {
        Collection = other.Collection;
        Options = new(other.Options);
        
        ConflictResolver = other.ConflictResolver;
        PushFilter = other.PushFilter;
        PullFilter = other.PullFilter;
    }
}