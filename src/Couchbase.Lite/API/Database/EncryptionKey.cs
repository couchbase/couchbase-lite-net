// 
// EncryptionKey.cs
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
using System.Security.Cryptography;
using System.Text;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite
{
    /// <summary>
    /// Basic AES encryption. Uses a 256-bit (32-byte) key.
    /// </summary>
    public sealed class EncryptionKey
    {
        #region Constants

        /// <summary>
        /// Number of bytes in a 256-bit key
        /// </summary>
        public static readonly int DataSize = 32;

        /// <summary>
        /// The data type associated with encrypted content
        /// </summary>
        public static readonly string EncryptedContentType = "application/x-beanbag-aes-256";

        private const int BlockSize = 16;
        private const int DefaultPbkdfRounds = 64000;
        private const string DefaultSalt = "Salty McNaCl";
        private const int IvSize = BlockSize;

        private const int KeySize = 32;

        private const string Tag = nameof(EncryptionKey);

        #endregion

        #region Variables

        private Aes _cryptor;

		#endregion

		#region Properties

		/// <summary>
		/// The key data encoded as hex.
		/// </summary>
		public string HexData => BitConverter.ToString(KeyData).Replace("-", String.Empty).ToLower();

		/// <summary>
		/// The SymmetricKey's key data; can be used to reconstitute it.
		/// </summary>
		public byte[] KeyData => _cryptor.Key;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance with a random key.
        /// </summary>
        public EncryptionKey() 
        {
            InitCryptor();
            _cryptor.GenerateKey();
        }

        /// <summary>
        /// Creates an instance with a key derived from a password.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <param name="salt">A fixed data blob that perturbs the generated key. 
        /// Should be kept fixed for any particular app, but doesn't need to be secret.</param>
        /// <param name="rounds">The number of rounds of hashing to perform. 
        /// More rounds is more secure but takes longer.</param>
        public EncryptionKey(string password, byte[] salt, int rounds) 
        {
            if(password == null) {
                Log.To.Database.E(Tag, "password cannot be null in ctor, throwing...");
                throw new ArgumentNullException(nameof(password));
            }

            if (salt == null) {
                Log.To.Database.E(Tag, "salt cannot be null in ctor, throwing...");
                throw new ArgumentNullException(nameof(salt));
            }

            if(salt.Length <= 4) {
                Log.To.Database.E(Tag, "salt cannot be less than 4 bytes in ctor, throwing...");
                throw new ArgumentOutOfRangeException(nameof(salt), "Value is too short");
            }
            if(rounds <= 200) {
                Log.To.Database.E(Tag, "rounds cannot be <= 200 in ctor, throwing...");
                throw new ArgumentOutOfRangeException(nameof(rounds), "Insufficient rounds");
            }

            InitCryptor();
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt) {
                IterationCount = rounds
            };

            _cryptor.Key = pbkdf2.GetBytes(KeySize);
        }

        /// <summary>
        /// Creates an instance with a key derived from a password, using default salt and rounds.
        /// </summary>
        /// <param name="password">The password to derive the key from</param>
        public EncryptionKey(string password) : 
        this(password, Encoding.UTF8.GetBytes(DefaultSalt), DefaultPbkdfRounds) {}

        /// <summary>
        /// Creates an instance from existing key data.
        /// </summary>
        /// <param name="keyData">The derived key data to use</param>
        public EncryptionKey(byte[] keyData) 
        {
            InitCryptor();
            if(keyData == null || keyData.Length != KeySize) {
                throw new ArgumentOutOfRangeException(nameof(keyData), "Value is incorrect size");
            }

            _cryptor.Key = keyData;
        }

        #endregion

        #region Private Methods

        private void InitCryptor()
        {
            _cryptor = Aes.Create();
            _cryptor.KeySize = KeySize * 8;
            _cryptor.BlockSize = BlockSize * 8;
            _cryptor.Padding = PaddingMode.PKCS7;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _cryptor.Dispose();
        }

        #endregion
    }
}
