//
// C4Document.cs
//
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
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

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649  // Member never assigned to
#pragma warning disable CS0169  // Member never used



namespace LiteCore.Interop
{
    internal unsafe delegate FLSliceResult C4DocDeltaApplier(void* context, C4Revision* baseRevision,
        FLSlice delta, C4Error* outError);

    internal unsafe partial struct C4Document
    {
        // This definition is simply so silly in the C header that it is 
        // more effort than it is worth to try to parse it via script
        void* _internal1;
        void* _internal2;
        public C4DocumentFlags flags;
        public FLHeapSlice docID;
        public FLHeapSlice revID;
        public ulong sequence;
        public C4Revision selectedRev;
        public C4ExtraInfo extraInfo;
    }
}

#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0649  // Member never assigned to
#pragma warning restore CS0169  // Member never used
