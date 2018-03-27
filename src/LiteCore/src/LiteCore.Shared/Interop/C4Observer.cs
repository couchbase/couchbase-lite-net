//
// C4Observer.cs
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

using ObjCRuntime;

namespace LiteCore.Interop
{
    using Couchbase.Lite.Interop;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
         unsafe delegate void C4DatabaseObserverCallback(C4DatabaseObserver* observer, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if LITECORE_PACKAGED
    internal
#else
    public
#endif
         unsafe delegate void C4DocumentObserverCallback(C4DocumentObserver* observer, C4Slice docID, ulong sequence, void* context);

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
         unsafe delegate void DatabaseObserverCallback(C4DatabaseObserver* observer, object context);

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
         unsafe delegate void DocumentObserverCallback(C4DocumentObserver* observer, string docID, ulong sequence, object context);

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
         sealed unsafe class DatabaseObserver : IDisposable
    {
        private readonly object _context;
        private readonly DatabaseObserverCallback _callback;
        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private readonly C4DatabaseObserverCallback _nativeCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
        private GCHandle _id;
        private static readonly Dictionary<Guid, DatabaseObserver> _ObserverMap = new Dictionary<Guid, DatabaseObserver>();

        public C4DatabaseObserver* Observer => (C4DatabaseObserver*)_observer;
        private long _observer;

        public DatabaseObserver(C4Database* database, DatabaseObserverCallback callback, object context)
        {
            _context = context;
            _callback = callback;
            _nativeCallback = DbObserverCallback;
            var id = Guid.NewGuid();
            _id = GCHandle.Alloc(id, GCHandleType.Pinned);
            _observer = (long)LiteCoreBridge.Check(err => {
                _ObserverMap[id] = this;
                return Native.c4dbobs_create(database, _nativeCallback, GCHandle.ToIntPtr(_id).ToPointer());
            });
        }

        ~DatabaseObserver()
        {
            Dispose(false);
        }

        [MonoPInvokeCallback(typeof(C4DatabaseObserverCallback))]
        private static void DbObserverCallback(C4DatabaseObserver* observer, void* context)
        {
            var idHolder = GCHandle.FromIntPtr((IntPtr)context);
            var id = (Guid)idHolder.Target;
            var obj = _ObserverMap[id];
            obj._callback?.Invoke(observer, obj._context);
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Only types that need to be disposed unconditionally are dealt with")]
        private void Dispose(bool disposing)
        {
            var old = (C4DatabaseObserver*)Interlocked.Exchange(ref _observer, 0);
            Native.c4dbobs_free(old);
            var id = (Guid)_id.Target;
            _ObserverMap.Remove(id);
            _id.Free();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

#if LITECORE_PACKAGED
    internal
#else
    public
#endif
         sealed unsafe class DocumentObserver : IDisposable
    {
        private readonly object _context;
        private readonly DocumentObserverCallback _callback;
        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private readonly C4DocumentObserverCallback _nativeCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
        private GCHandle _id;
        private static readonly Dictionary<Guid, DocumentObserver> _ObserverMap = new Dictionary<Guid, DocumentObserver>();

        public C4DocumentObserver* Observer { get; private set; }

        public DocumentObserver(C4Database* database, string docID, DocumentObserverCallback callback, object context)
        {
            _context = context;
            _callback = callback;
            _nativeCallback = DocObserverCallback;
            var id = Guid.NewGuid();
            _id = GCHandle.Alloc(id, GCHandleType.Pinned);
            Observer = (C4DocumentObserver *)LiteCoreBridge.Check(err => {
                _ObserverMap[id] = this;
                return Native.c4docobs_create(database, docID, _nativeCallback, GCHandle.ToIntPtr(_id).ToPointer());
            });
        }

        [MonoPInvokeCallback(typeof(C4DocumentObserverCallback))]
        private static void DocObserverCallback(C4DocumentObserver* observer, C4Slice docID, ulong sequence, void* context)
        {
            var idHolder = GCHandle.FromIntPtr((IntPtr)context);
            var id = (Guid)idHolder.Target;
            var obj = _ObserverMap[id];
            obj._callback?.Invoke(observer, docID.CreateString(), sequence, obj._context);
        }

        public void Dispose()
        {
            Native.c4docobs_free(Observer);
            Observer = null;
            var id = (Guid)_id.Target;
            _ObserverMap.Remove(id);
            _id.Free();
        }
    }
}

namespace Couchbase.Lite.Interop
{
    
    using LiteCore.Interop;

    internal static unsafe partial class Native
    {
        public static DatabaseObserver c4dbobs_create(C4Database *db, DatabaseObserverCallback callback, object context)
        {
            return new DatabaseObserver(db, callback, context);
        }

        public static DocumentObserver c4docobs_create(C4Database *db, string docID, DocumentObserverCallback callback, object context)
        {
            return new DocumentObserver(db, docID, callback, context);
        }
    }
}