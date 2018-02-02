// 
// MCollection.cs
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
using System.Diagnostics;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Serialization
{
    internal abstract unsafe class MCollection : IFLEncodable
    {
        #region Variables

        private MValue _slot;

        #endregion

        #region Properties

        public MContext Context { get; private set; }

        public bool IsMutable { get; private set; }

        public virtual bool IsMutated { get; private set; }

        public bool MutableChildren { get; set; }

        public MCollection Parent { get; private set; }

        #endregion

        #region Constructors

        protected MCollection() : this(MContext.Null, true)
        {
            
        }

        protected MCollection(MContext context, bool isMutable)
        {
            Context = context;
            IsMutable = isMutable;
            MutableChildren = isMutable;
        }

        #endregion

        #region Public Methods

        public virtual void InitAsCopyOf(MCollection original, bool isMutable)
        {
            Debug.Assert(Context == MContext.Null);
            Context = original.Context;
            IsMutable = MutableChildren = isMutable;
        }

        #endregion

        #region Protected Internal Methods

        protected internal void SetSlot(MValue newSlot, MValue oldSlot)
        {
            if (_slot == oldSlot) {
                _slot = newSlot;
                if (newSlot == null) {
                    Parent = null;
                }
            }
        }

        #endregion

        #region Protected Methods

        protected virtual void InitInSlot(MValue slot, MCollection parent, bool isMutable)
        {
            Debug.Assert(slot != null);
            Debug.Assert(Context == MContext.Null);
            _slot = slot;
            Parent = parent;
            IsMutable = isMutable;
            MutableChildren = isMutable;
            IsMutated = _slot.IsMutated;
            if (_slot.Value != null) {
                Context = Parent?.Context;
            }
        }

        protected void Mutate()
        {
            Debug.Assert(IsMutable);
            if (!IsMutated) {
                IsMutated = true;
                _slot?.Mutate();
                Parent?.Mutate();
            }
        }

        #endregion

        #region IFLEncodable

        public abstract void FLEncode(FLEncoder* enc);

        #endregion
    }
}