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

namespace Couchbase.Lite.Internal.Serialization;

internal abstract unsafe class MCollection(MContext context, bool isMutable) : IFLEncodable, IFLSlotSetable
{
    private MValue? _slot;

    public MContext? Context { get; private set; } = context;

    public bool IsMutable { get; private set; } = isMutable;

    public virtual bool IsMutated { get; private set; }

    public bool MutableChildren { get; set; } = isMutable;

    public MCollection? Parent { get; private set; }

    protected MCollection() : this(MContext.Null, true)
    {
            
    }

    public virtual void InitAsCopyOf(MCollection original, bool isMutable)
    {
        Debug.Assert(Context == MContext.Null);
        Context = original.Context;
        IsMutable = MutableChildren = isMutable;
    }

    protected virtual void InitInSlot(MValue slot, MCollection? parent, bool isMutable)
    {
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

    public abstract void FLEncode(FLEncoder* enc);

    public abstract void FLSlotSet(FLSlot* slot);
}