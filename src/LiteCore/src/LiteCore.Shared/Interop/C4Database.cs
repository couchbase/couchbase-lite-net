//
//  C4Database.cs
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
using System.Diagnostics.CodeAnalysis;

using LiteCore.Util;

namespace LiteCore.Interop;

[ExcludeFromCodeCoverage]
internal unsafe partial struct C4EncryptionKey
{
    // ReSharper disable MemberCanBePrivate.Global
    public static readonly int Size = 32;
    // ReSharper restore MemberCanBePrivate.Global
        
    public override int GetHashCode()
    {
        var hasher = new HashCode();
        for(var i = 0; i < Size; i++) {
            hasher.Add(bytes[i]);
        }

        return hasher.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if(obj is not C4UUID other) {
            return false;
        }

        for(var i = 0; i < Size; i++) {
            if(bytes[i] != other.bytes[i]) {
                return false;
            }
        }

        return true;
    }
}

[ExcludeFromCodeCoverage]
internal unsafe partial struct C4UUID
{
    public static readonly int Size = 16;

    public override int GetHashCode()
    {
        var hasher = new HashCode();
        for(var i = 0; i < Size; i++) {
            hasher.Add(bytes[i]);
        }

        return hasher.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if(obj is not C4UUID other) {
            return false;
        }

        for(var i = 0; i < Size; i++) {
            if(bytes[i] != other.bytes[i]) {
                return false;
            }
        }

        return true;
    }
}

internal sealed class CollectionSpec(string scope, string name) : IDisposable
{
    private C4String _name = new(name);
    private C4String _scope = new(scope);

    public static implicit operator C4CollectionSpec(CollectionSpec c)
    {
        return new C4CollectionSpec
        {
            name = c._name.AsFLSlice(),
            scope = c._scope.AsFLSlice()
        };
    }

    public void Dispose()
    {
        _name.Dispose();
        _scope.Dispose();
    }
}