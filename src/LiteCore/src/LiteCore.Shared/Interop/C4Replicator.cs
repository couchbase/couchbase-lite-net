//
// C4Replicator.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using LiteCore.Interop;
using LiteCore.Util;

namespace LiteCore.Interop
{

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4ReplicatorStatusChangedCallback(C4Replicator* replicator,
            C4ReplicatorStatus replicatorState, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4ReplicatorDocumentEndedCallback(C4Replicator* replicator,
            [MarshalAs(UnmanagedType.U1)]bool pushing, IntPtr numDocs, C4DocumentEnded**docs,
            void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4ReplicatorBlobProgressCallback(C4Replicator* replicator,
        [MarshalAs(UnmanagedType.U1)]bool pushing, C4CollectionSpec collectionSpec, FLSlice docID, FLSlice docProperty, 
        C4BlobKey blobKey, ulong bytesComplete, ulong bytesTotal, C4Error error, 
        void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal unsafe delegate bool C4ReplicatorValidationFunction(C4CollectionSpec collectionSpec, FLSlice docID,
        FLSlice revID, C4RevisionFlags revisionFlags, FLDict* body, void* context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate FLSliceResult C4ReplicatorPropertyEncryptionCallback(void* context, 
        C4CollectionSpec collection, FLSlice documentID, FLDict properties, FLSlice keyPath, FLSlice input, 
        FLSliceResult* outAlgorithm, FLSliceResult* outKeyID, C4Error error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate FLSliceResult C4ReplicatorPropertyDecryptionCallback(void* context,
        C4CollectionSpec collection, FLSlice documentID, FLDict properties, FLSlice keyPath, FLSlice input,
        FLSlice outAlgorithm, FLSlice outKeyID, C4Error error);
}

namespace LiteCore.Interop
{
    [ExcludeFromCodeCoverage]
    internal sealed class ReplicatorParameters : IDisposable
    {
        #region Variables

        private C4ReplicatorParameters _c4Params;
        private C4SocketFactory _factoryKeepAlive;
        private bool _hasFactory;
        private C4ReplicatorDocumentEndedCallback _onDocumentEnded;
        private C4ReplicatorStatusChangedCallback _onStatusChanged;
        private C4ReplicatorValidationFunction _pushFilter;
        private C4ReplicatorValidationFunction _validation;
        private C4ReplicationCollection[] c4ReplicationCollections;
        private C4CollectionSpec[] c4CollectionSpec;

        #endregion

        #region Properties

        public C4ReplicatorParameters C4Params => _c4Params;

        public unsafe object Context
        {
            get => GCHandle.FromIntPtr((IntPtr) _c4Params.callbackContext).Target;
            set {
                if (_c4Params.callbackContext != null) {
                    GCHandle.FromIntPtr((IntPtr)_c4Params.callbackContext).Free();
                }

                if (value != null) {
                    _c4Params.callbackContext = GCHandle.ToIntPtr(GCHandle.Alloc(value)).ToPointer();
                }
            }
        }

        public C4ReplicatorDocumentEndedCallback OnDocumentEnded
        {
            get => _onDocumentEnded;
            set {
                _onDocumentEnded = value;
                _c4Params.onDocumentEnded = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public C4ReplicatorStatusChangedCallback OnStatusChanged
        {
            get => _onStatusChanged;
            set {
                _onStatusChanged = value;
                _c4Params.onStatusChanged = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public C4ReplicatorMode Pull
        {
            get => _c4Params.pull;
            set => _c4Params.pull = value;
        }

        public C4ReplicatorValidationFunction PullFilter
        {
            get => _validation;
            set {
                _validation = value;
                _c4Params.validationFunc = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public C4ReplicatorMode Push
        {
            get => _c4Params.push;
            set => _c4Params.push = value;
        }

        public C4ReplicatorValidationFunction PushFilter
        {
            get => _pushFilter;
            set {
                _pushFilter = value;
                _c4Params.pushFilter = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public unsafe C4SocketFactory* SocketFactory
        {
            get => _c4Params.socketFactory;
            set {
                if (_hasFactory) {
                    // Unlikely, but here for safety.  This means that the SocketFactory
                    // was already set before, but being reset.  So free the old context.
                    GCHandle.FromIntPtr((IntPtr)_factoryKeepAlive.context).Free();
                }

                _c4Params.socketFactory = value;
                _hasFactory = value != null;
                if (_hasFactory) {
                    // HACK: This object will not survive long because it is a struct
                    // but the property type is a pointer.  Keep the actual object here
                    // so that it can be disposed later
                    _factoryKeepAlive = *value;
                }
            }
        }

        public List<ReplicationCollection> CollectionConfigs { get; set; }

        public long CollectionCount
        {
            get => _c4Params.collectionCount.ToInt64();
            set => _c4Params.collectionCount = (IntPtr)value;
        }

        #endregion

        #region Constructors

        public ReplicatorParameters(IDictionary<string, object> options)
        {
            if (options != null) {
                _c4Params.optionsDictFleece = (FLSlice) options.FLEncode();
            }
        }

        ~ReplicatorParameters()
        {
            Dispose(true);
        }

        #endregion

        #region Internal Methods

        internal unsafe void UpdateC4ReplicationCollection()
        {
            if (CollectionCount == 0)
                return;

            c4ReplicationCollections = new C4ReplicationCollection[CollectionCount];
            c4CollectionSpec = new C4CollectionSpec[CollectionCount];
            for (int i =0; i< CollectionCount; i++) {
                var colName = CollectionConfigs[i].CollectionSpec.Name;
                var scopeName = CollectionConfigs[i].CollectionSpec.Scope;
                c4CollectionSpec[i] = new C4CollectionSpec()
                {
                    name = new C4String(colName).AsFLSlice(),
                    scope = new C4String(scopeName).AsFLSlice()
                };
                var localC4ReplicationCol = CollectionConfigs[i].C4ReplicationCol;
                localC4ReplicationCol.collection = c4CollectionSpec[i];
                c4ReplicationCollections[i] = localC4ReplicationCol;
            }

            fixed (C4ReplicationCollection* ptr = c4ReplicationCollections) {
                _c4Params.collections = ptr;
            }
        }

        #endregion

        #region Private Methods

        private unsafe void Dispose(bool finalizing)
        {
            Native.FLSliceResult_Release((FLSliceResult)_c4Params.optionsDictFleece);
            c4CollectionSpec = null;
            c4ReplicationCollections = null;
            foreach (var c in CollectionConfigs) {
                c.Dispose();
            }

            Context = null;
            if (_hasFactory) {
                GCHandle.FromIntPtr((IntPtr)_factoryKeepAlive.context).Free();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    [ExcludeFromCodeCoverage]
    internal sealed class ReplicationCollection : IDisposable
    {
        #region Variables

        private C4ReplicationCollection _c4ReplicationCol;
        private C4ReplicatorValidationFunction _pushFilter;
        private C4ReplicatorValidationFunction _validation;

        #endregion

        #region Properties

        public C4ReplicationCollection C4ReplicationCol => _c4ReplicationCol;

        public unsafe object Context
        {
            get => GCHandle.FromIntPtr((IntPtr)_c4ReplicationCol.callbackContext).Target;
            set {
                if (_c4ReplicationCol.callbackContext != null) {
                    GCHandle.FromIntPtr((IntPtr)_c4ReplicationCol.callbackContext).Free();
                }

                if (value != null) {
                    _c4ReplicationCol.callbackContext = GCHandle.ToIntPtr(GCHandle.Alloc(value)).ToPointer();
                }
            }
        }

        public C4ReplicatorMode Pull
        {
            get => _c4ReplicationCol.pull;
            set => _c4ReplicationCol.pull = value;
        }

        public C4ReplicatorValidationFunction PullFilter
        {
            get => _validation;
            set {
                _validation = value;
                _c4ReplicationCol.pullFilter = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        public C4ReplicatorMode Push
        {
            get => _c4ReplicationCol.push;
            set => _c4ReplicationCol.push = value;
        }

        public C4ReplicatorValidationFunction PushFilter
        {
            get => _pushFilter;
            set {
                _pushFilter = value;
                _c4ReplicationCol.pushFilter = Marshal.GetFunctionPointerForDelegate(value);
            }
        }

        internal CollectionSpec CollectionSpec { get; set; }

        #endregion

        #region Constructors

        public ReplicationCollection(IDictionary<string, object> options)
        {
            if (options != null) {
                _c4ReplicationCol.optionsDictFleece = (FLSlice)options.FLEncode();
            }
        }

        ~ReplicationCollection()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private unsafe void Dispose(bool finalizing)
        {
            Native.FLSliceResult_Release((FLSliceResult)_c4ReplicationCol.optionsDictFleece);
            Context = null;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    internal static unsafe partial class Native
    {
        #region Public Methods

        public static C4Replicator* c4repl_new(C4Database* db, C4Address remoteAddress, string remoteDatabaseName, C4ReplicatorParameters @params, C4Error* err)
        {
            using (var remoteDatabaseName_ = new C4String(remoteDatabaseName)) {
                return c4repl_new(db, remoteAddress, remoteDatabaseName_.AsFLSlice(), @params, err);
            }
        }

        #endregion
    }
}
