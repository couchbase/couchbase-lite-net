//
// C4BlobStore_native_safe.cs
//
// Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

// Shadowing the C function naming style
#pragma warning disable IDE1006

namespace LiteCore.Interop;

internal static unsafe partial class NativeSafe
{
    // Thread Safe Methods
    public static byte[]? c4cert_copyData(C4Cert* x, bool pemEncoded)
    {
        using var retVal = Native.c4cert_copyData(x, pemEncoded);
        return ((FLSlice)retVal).ToArrayFast();
    }

}