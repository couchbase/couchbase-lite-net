//
//  C4Database.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LiteCore.Util;

namespace LiteCore
{

    [ExcludeFromCodeCoverage]
    internal struct C4StorageEngine
    {
        public static readonly string SQLite = "SQLite";
    }
}

namespace LiteCore.Interop
{

    [ExcludeFromCodeCoverage]
    internal partial struct C4EncryptionKey
    {
        public static readonly int Size = 32;
    }

    [ExcludeFromCodeCoverage]
    internal unsafe partial struct C4UUID
    {
        public static readonly int Size = 16;

        // NOTE: The below produces IL that is not understandable by Mono
        /*public override int GetHashCode()
        {
            var hasher = Hasher.Start;
            fixed (byte* b = bytes) {
                for (int i = 0; i < Size; i++) {
                    hasher.Add(b[i]);
                }
            }

            return hasher.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(!(obj is C4UUID)) {
                return false;
            }

            var other = (C4UUID)obj;
            fixed(byte* b = bytes) {
                for(var i = 0; i < Size; i++) {
                    if(b[i] != other.bytes[i]) {
                        return false;
                    }
                }
            }

            return true;
        }*/
    }

    [ExcludeFromCodeCoverage]
    internal unsafe partial struct C4DatabaseConfig : IDisposable
    {
        public static C4DatabaseConfig Clone(C4DatabaseConfig *source)
        {
            var retVal = new C4DatabaseConfig {
                flags = source->flags,
                versioning = source->versioning,
                encryptionKey = source->encryptionKey,
                storageEngine = source->storageEngine
            };

            return retVal;
        }

        public static C4DatabaseConfig Get(C4DatabaseConfig *source)
        {
            var retVal = new C4DatabaseConfig {
                flags = source->flags,
                versioning = source->versioning,
                encryptionKey = source->encryptionKey,
                _storageEngine = source->_storageEngine // Note: raw copy!
            };

            return retVal;
        }

        public void Dispose()
        {
            storageEngine = null;
        }
    }
}
