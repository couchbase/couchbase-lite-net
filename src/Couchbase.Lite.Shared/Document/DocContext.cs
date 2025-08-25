﻿// 
// DocContext.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using Couchbase.Lite.Internal.Serialization;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc;

internal unsafe class DocContext(Database database, C4DocumentWrapper? doc) : MContext(new FLSlice())
{
    public Database Db { get; } = database;

    public C4DocumentWrapper? Doc { get; } = doc?.Retain<C4DocumentWrapper>();

    public object? ToObject(FLValue* value, bool dotNetType) => 
        FLValueConverter.ToCouchbaseObject(value, Db, dotNetType);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing) {
            Doc?.Dispose();
        }
    }
}