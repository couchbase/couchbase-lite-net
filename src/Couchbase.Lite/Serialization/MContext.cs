// 
// MContext.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization
{
    internal unsafe class MContext : IDisposable
    {
        #region Constants

        public static readonly MContext Null = new MContext();

        #endregion

        #region Properties

        public FLSlice Data { get; }

        public FLSharedKeys* SharedKeys { get; }

        #if DEBUG
        internal bool Disposed { get; private set; }
        #endif

        #endregion

        #region Constructors

        private MContext()
        {
        }

        public MContext(FLSlice data, FLSharedKeys* sk)
        {
            Data = data;
            SharedKeys = sk;
        }

        ~MContext()
        {
            Dispose(false);
        }

        #endregion

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            #if DEBUG
            Disposed = true;
            #endif
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (this == Null) {
                return;
            }
            
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}