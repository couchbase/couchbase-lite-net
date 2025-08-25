// 
//  MContext.cs
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

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization;

internal class MContext : IDisposable
{
    public static readonly MContext Null = new MContext();

    private bool _disposed;

    private readonly FLSlice _data;

    public FLSlice Data
    {
        get {
            CheckDisposed();
            return _data;
        }
    }

    private MContext()
    {
    }

    public MContext(FLSlice data)
    {
        _data = data;
    }

    ~MContext()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        _disposed = true;
    }

    [SuppressMessage("Maintainability", "CA1513:Use ObjectDisposedException throw helper")]
    internal void CheckDisposed()
    {
        if(_disposed) {
            throw new ObjectDisposedException("MContext was disposed (probably QueryResultSet or IIndexUpdater)");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}