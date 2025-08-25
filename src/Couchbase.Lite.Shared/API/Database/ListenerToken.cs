// 
//  ListenerToken.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Util;

using LiteCore.Util;

namespace Couchbase.Lite;

internal enum ListenerTokenType
{
    Database,
    Document,
    Query,
    Replicator,
    DocReplicated,
    MessageEndpoint
}

/// <summary>
/// A token that stores information about an event handler that
/// is registered on a Couchbase Lite object (for example
/// <see cref="Collection.AddChangeListener(System.EventHandler{CollectionChangedEventArgs})"/>)
/// </summary>
public readonly struct ListenerToken : IEquatable<ListenerToken>
{
    internal readonly CouchbaseEventHandler EventHandler;

    internal readonly ListenerTokenType Type;

    private readonly IChangeObservableRemovable _changeObservableRemovable;

    internal ListenerToken(CouchbaseEventHandler handler, ListenerTokenType type,
        IChangeObservableRemovable changeObservableRemovable)
    {
        EventHandler = handler;
        Type = type;
        _changeObservableRemovable = changeObservableRemovable;
    }

    /// <summary>
    /// Remove the linked change listener
    /// </summary>
    public void Remove()
    {
        _changeObservableRemovable.RemoveChangeListener(this);
    }
    
    public bool Equals(ListenerToken other) => EventHandler.Equals(other.EventHandler) && Type == other.Type && _changeObservableRemovable.Equals(other._changeObservableRemovable);
    
    public override bool Equals(object? obj) => obj is ListenerToken other && Equals(other);

    public override int GetHashCode()
    {
        var hasher = Hasher.Start;
        hasher.Add(EventHandler);
        hasher.Add(Type);
        hasher.Add(_changeObservableRemovable);
        return hasher.GetHashCode();
    }
}