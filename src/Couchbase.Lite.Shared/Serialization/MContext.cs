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

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization
{
    internal unsafe class MContext : IDisposable
    {
        #region Constants

        public static readonly MContext Null = new MContext();

        #endregion

        private bool _disposed;

        private readonly FLSlice _data;

        #region Properties

        public FLSlice Data
        {
            get {
                CheckDisposed();
                return _data;
            }
        }

        #endregion

        #region Constructors

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

        #endregion

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }

        #endregion

        internal void CheckDisposed()
        {
            if(_disposed) {
                throw new ObjectDisposedException("MContext was disposed (probably QueryResultSet)");
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}