// 
//  C4BlobStore.cs
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

using System.Diagnostics.CodeAnalysis;

namespace LiteCore.Interop
{
    internal static unsafe partial class Native
    {
        public static ulong c4stream_read(C4ReadStream *stream, byte[] buffer, C4Error *outError)
        {
            return c4stream_read(stream, buffer, buffer.Length, outError);
        }
        
        public static bool c4stream_write(C4WriteStream* stream, byte[] bytes, C4Error* outError)
        {
            return c4stream_write(stream, bytes, (ulong)bytes.Length, outError);
        }
    }

    internal partial struct C4BlobKey
    {
        public const int Size = 20;
    }
}
