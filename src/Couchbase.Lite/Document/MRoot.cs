// 
// MRoot.cs
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
using Couchbase.Lite.Internal.Serialization;
using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class MRoot : MCollection, IDisposable
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
            : this(context, NativeRaw.FLValue_FromData(context.Data), isMutable)
        {
            
        }

        public MRoot(FLSlice fleeceData, FLSharedKeys* sk, FLValue* value, bool isMutable = true)
            : this(new MContext(fleeceData, sk), isMutable)
        {
            
        }

        public MRoot(FLSlice fleeceData, FLSharedKeys* sk = null, bool isMutable = true)
            : this(fleeceData, sk, NativeRaw.FLValue_FromData(fleeceData), isMutable)
        {
            
        }

        #endregion

        #region Public Methods

        public static object AsObject(FLSlice fleeceData, FLSharedKeys* sk = null, bool mutableContainers = true)
        {
            using (var root = new MRoot(fleeceData, sk, mutableContainers)) {
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
                throw new LiteCoreException(new C4Error(error));
            }

            return result;
        }

        public FLSliceResult EncodeDelta()
        {
            var enc = Native.FLEncoder_New();
            NativeRaw.FLEncoder_MakeDelta(enc, Context.Data, true);
            FLEncode(enc);

            FLError error;
            var result = NativeRaw.FLEncoder_Finish(enc, &error);
            if (result.buf == null) {
                throw new LiteCoreException(new C4Error(error));
            }

            return result;
        }

        #endregion

        #region IDisposable

        public void Dispose()
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