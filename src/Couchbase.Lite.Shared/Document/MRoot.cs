// 
// MRoot.cs
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
using Couchbase.Lite.Internal.Serialization;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class MRoot : MCollection
    {
        #region Variables

        private readonly MValue _slot;

        #endregion

        #region Properties

        public override bool IsMutated => _slot.IsMutated;

        #endregion

        #region Constructors

        public MRoot()
        {
            
        }

        public MRoot(MContext context, FLValue* value, bool isMutable)
            : base(context, isMutable)
        {
            _slot = new MValue(value);
        }

        public MRoot(MContext context, bool isMutable = true)
            : this(context, NativeRaw.FLValue_FromData(context.Data, FLTrust.Untrusted), isMutable)
        {
            
        }

        public MRoot(FLSlice fleeceData, FLValue* value, bool isMutable = true)
            : this(new MContext(fleeceData), isMutable)
        {
            
        }

        public MRoot(FLSlice fleeceData, bool isMutable = true)
            : this(fleeceData, NativeRaw.FLValue_FromData(fleeceData, FLTrust.Untrusted), isMutable)
        {
            
        }

        public MRoot(MRoot other)
            : this(other?.Context?.Data ?? FLSlice.Null,
                other?.IsMutable == true)
        {

        }

        #endregion

        #region Public Methods

        public static object AsObject(FLSlice fleeceData, bool mutableContainers = true)
        {
            using (var root = new MRoot(fleeceData, mutableContainers)) {
                return root.AsObject();
            }
        }

        public static implicit operator bool(MRoot root)
        {
            return !root._slot.IsEmpty;
        }

        public object AsObject()
        {
            return _slot.AsObject(this);
        }

        public FLSliceResult Encode()
        {
            var enc = Native.FLEncoder_New();
            FLEncode(enc);

            FLError error;
            var result = NativeRaw.FLEncoder_Finish(enc, &error);
            if (result.buf == null) {
                throw new CouchbaseFleeceException(error);
            }

            return result;
        }

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            Context?.Dispose();
        }

        #endregion

        #region IFLEncodable

        public override void FLEncode(FLEncoder* enc)
        {
            _slot.FLEncode(enc);
        }

        #endregion
    }
}