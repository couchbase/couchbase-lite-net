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

    [ExcludeFromCodeCoverage]
    internal sealed unsafe class DatabaseConfig2 : IDisposable
    {
        #region Variables

        private C4DatabaseConfig2 _c4DatabaseConfig2;
        private C4String _parentDirectory;
        private C4EncryptionAlgorithm _c4EncryptionAlgorithm = C4EncryptionAlgorithm.None;

        #endregion

        #region Properties

        /// <summary>
        /// Configuration for a C4Database.
        /// </summary>
        public C4DatabaseConfig2 C4DatabaseConfig2 => _c4DatabaseConfig2;

        /// <summary>
        /// Directory for databases
        /// </summary>
        public string ParentDirectory
        {
            get => _c4DatabaseConfig2.parentDirectory.CreateString();
            set {
                _parentDirectory.Dispose();
                _parentDirectory = new C4String(value);
                _c4DatabaseConfig2.parentDirectory = _parentDirectory.AsFLSlice();
            }
        }

        /// <summary>
        /// Create, ReadOnly, NoUpgrade (AutoCompact & SharedKeys always set)
        /// </summary>
        public C4DatabaseFlags DatabaseFlags
        {
            get => _c4DatabaseConfig2.flags;
            set => _c4DatabaseConfig2.flags = value;
        }

        /// <summary>
        /// Encryption Key Algorithm to use creating/opening the db
        /// </summary>
        public C4EncryptionAlgorithm EncryptionAlgorithm
        {
            get => _c4EncryptionAlgorithm;
            set {
                _c4EncryptionAlgorithm = value;
            }
        }

        #endregion

        #region Constructors

        public DatabaseConfig2()
        {
            var encryptionKey = new C4EncryptionKey();
            encryptionKey.algorithm = _c4EncryptionAlgorithm;
            _c4DatabaseConfig2.encryptionKey = encryptionKey;
        }

        public DatabaseConfig2(C4DatabaseConfig2* c4dbConfig)
        {
            _c4DatabaseConfig2.encryptionKey = c4dbConfig->encryptionKey;
            _c4DatabaseConfig2.flags = c4dbConfig->flags;
            _c4DatabaseConfig2.parentDirectory = c4dbConfig->parentDirectory;
        }

        ~DatabaseConfig2()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private void Dispose(bool finalizing)
        {
            _parentDirectory.Dispose();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
