//
//  SymmetricKey.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using Couchbase.Lite.Logging;

namespace Couchbase.Lite
{
    /// <summary>
    /// Type of block returned by SymmetricKey.CreateEncryptor.
    /// This block can be called repeatedly with input data and returns additional output data.
    /// At EOF, the block should be called with a null parameter, and
    /// it will return the remaining encrypted data from its buffer.
    /// </summary>
    public delegate byte[] CryptorBlock(byte[] input);

    /// <summary>
    /// Basic AES encryption. Uses a 256-bit (32-byte) key.
    /// </summary>
    internal sealed class SymmetricKey : IEncryptionKey
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

        private const string Tag = nameof(SymmetricKey);

        #endregion

        #region Variables

        #region Private Members

        private Aes _cryptor;

        #endregion

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
        public SymmetricKey() 
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
        public SymmetricKey(string password, byte[] salt, int rounds) 
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
        public SymmetricKey(string password) : 
        this(password, Encoding.UTF8.GetBytes(DefaultSalt), DefaultPbkdfRounds) {}

        /// <summary>
        /// Creates an instance from existing key data.
        /// </summary>
        public SymmetricKey(byte[] keyData) 
        {
            InitCryptor();
            if(keyData == null || keyData.Length != KeySize) {
                throw new ArgumentOutOfRangeException(nameof(keyData), "Value is incorrect size");
            }

            _cryptor.Key = keyData;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a strem that will encrypt the given base stream
        /// </summary>
        /// <returns>The stream to write to for encryption</returns>
        /// <param name="baseStream">The stream to read from</param>
        public CryptoStream CreateStream(Stream baseStream)
        {
            if (_cryptor == null || baseStream == null) {
                return null;
            }

            var retVal = new CryptoStream(baseStream, _cryptor.CreateEncryptor(), CryptoStreamMode.Write);
            retVal.Write(_cryptor.IV, 0, IvSize);
            return retVal;
        }

        /// <summary>
        /// Decrypts data encoded by encryptData.
        /// </summary>
        public byte[] DecryptData(byte[] encryptedData)
        {
            var buffer = new List<byte>();
            using(var ms = new MemoryStream(encryptedData))
            using(var cs = DecryptStream(ms)) {
                int next;
                while((next = cs.ReadByte()) != -1) {
                    buffer.Add((byte)next);
                }
            }

            return buffer.ToArray();
        }

        /// <summary>
        /// Streaming decryption.
        /// </summary>
        public Stream DecryptStream(Stream stream)
        {
            if(stream == null || !stream.CanRead) {
                Log.To.Database.E(Tag, "Unable to read from stream, throwing...");
                throw new ArgumentException("Unable to read from stream", nameof(stream));
            }

            byte[] iv = new byte[IvSize];
            int bytesRead = stream.ReadAsync(iv, 0, IvSize).Result;

            if(bytesRead != IvSize) {
                return null;
            }

            _cryptor.IV = iv;
            return new CryptoStream(stream, _cryptor.CreateDecryptor(), CryptoStreamMode.Read);
        }

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _cryptor.Dispose();
        }

        #endregion

        /// <summary>
        /// Encrypts a data blob.
        /// The output consists of a 16-byte random initialization vector,
        /// followed by PKCS7-padded ciphertext. 
        /// </summary>
        public byte[] EncryptData(byte[] data)
        {
            if (data == null) {
                return null;
            }

            byte[] encrypted;
            _cryptor.GenerateIV();
            using(var ms = new MemoryStream())
            using(var cs = new CryptoStream(ms, _cryptor.CreateEncryptor(), CryptoStreamMode.Write)) {
                ms.Write(_cryptor.IV, 0, IvSize);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                encrypted = ms.ToArray();
            }

            return encrypted;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a new SymmetricKey using the supplied data
        /// </summary>
        /// <param name="keyOrPassword">A password as a string or a byte
        /// IEnumerable containig key data</param>
        internal static SymmetricKey Create(object keyOrPassword)
        {
            if (keyOrPassword == null) {
                return null;
            }

            var password = keyOrPassword as string;
            if(password != null) {
                return new SymmetricKey(password);
            }

            var data = keyOrPassword as IEnumerable<byte>;
            if (data == null) {
                Log.To.Database.E(Tag, "Invalid keyOrPassword type ({0}) received, must be string " +
                "or IEnumerable<byte>, throwing...", keyOrPassword.GetType().FullName);
                throw new InvalidDataException("keyOrPassword must be either string or IEnumerable<byte>");
            }

            return new SymmetricKey(data.ToArray());
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
    }
}
