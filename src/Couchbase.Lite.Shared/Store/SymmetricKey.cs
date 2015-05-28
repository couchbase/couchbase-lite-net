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

namespace Couchbase.Lite.Store
{
    /// <summary>
    /// Basic AES encryption. Uses a 256-bit (32-byte) key.
    /// </summary>
    public sealed class SymmetricKey : IDisposable
    {

        #region Constants

        public const int DATA_SIZE = 32; //Number of bytes in a 256-bit key
        public const string ENCRYPTED_CONTENT_TYPE = "application/x-beanbag-aes-256";

        private const int KEY_SIZE = 32;
        private const int BLOCK_SIZE = 16;
        private const int IV_SIZE = BLOCK_SIZE;
        private const int CHECKSUM_SIZE = sizeof(uint);
        private const string DEFAULT_SALT = "Salty McNaCl";
        private const int DEFAULT_PBKDF_ROUNDS = 64000;

        /// <summary>
        /// Type of block returned by SymmetricKey.CreateEncryptor.
        /// This block can be called repeatedly with input data and returns additional output data.
        /// At EOF, the block should be called with a null parameter, and
        /// it will return the remaining encrypted data from its buffer.
        /// </summary>
        public delegate byte[] CryptorBlock(byte[] input);

        #endregion

        #region Private Members

        private Aes _cryptor;

        private struct Header
        {
            public byte[] iv;
            public byte[] encrypted;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The SymmetricKey's key data; can be used to reconstitute it.
        /// </summary>
        public byte[] KeyData { 
            get {
                return _cryptor.Key;
            }
        }

        /// <summary>
        /// The key data encoded as hex.
        /// </summary>
        public string HexData { 
            get {
                return BitConverter.ToString(KeyData);
            }
        }

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
                throw new ArgumentNullException("password");
            }
            if(salt.Length <= 4) {
                throw new ArgumentOutOfRangeException("salt", "Value is too short");
            }
            if(rounds <= 200) {
                throw new ArgumentOutOfRangeException("rounds", "Insufficient rounds");
            }

            InitCryptor();
            Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt);
            pbkdf2.IterationCount = rounds;
            _cryptor.Key = pbkdf2.GetBytes(KEY_SIZE);
        }

        /// <summary>
        /// Creates an instance with a key derived from a password, using default salt and rounds.
        /// </summary>
        public SymmetricKey(string password) : 
        this(password, Encoding.UTF8.GetBytes(DEFAULT_SALT), DEFAULT_PBKDF_ROUNDS) {}

        /// <summary>
        /// Creates an instance from existing key data.
        /// </summary>
        public SymmetricKey(byte[] keyData) 
        {
            InitCryptor();
            if(keyData.Length != KEY_SIZE) {
                throw new ArgumentOutOfRangeException("keyData", "Value is incorrect size");
            }

            _cryptor.Key = keyData;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Encrypts a data blob.
        /// The output consists of a 16-byte random initialization vector,
        /// followed by PKCS7-padded ciphertext. 
        /// </summary>
        public byte[] EncryptData(byte[] data)
        {
            byte[] encrypted = null;
            _cryptor.GenerateIV();
            using(var ms = new MemoryStream())
            using(var cs = new CryptoStream(ms, _cryptor.CreateEncryptor(), CryptoStreamMode.Write)) {
                ms.Write(_cryptor.IV, 0, IV_SIZE);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                encrypted = ms.ToArray();
            }

            return encrypted;
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
            if(!stream.CanRead) {
                throw new ArgumentException("Unable to read from stream", "stream");
            }

            byte[] iv = new byte[IV_SIZE];
            int bytesRead = stream.ReadAsync(iv, 0, IV_SIZE).Result;

            if(bytesRead != IV_SIZE) {
                return null;
            }

            _cryptor.IV = iv;
            return new CryptoStream(stream, _cryptor.CreateDecryptor(), CryptoStreamMode.Read);
        }

        /// <summary>
        /// Incremental encryption: returns a block that can be called repeatedly with input data and
        /// returns additional output data. At EOF the block should be called with a nil parameter, and
        /// it will return the remaining encrypted data from its buffer. 
        /// </summary>
        public CryptorBlock CreateEncryptor()
        {
            _cryptor.GenerateIV();
            var encryptor = _cryptor.CreateEncryptor();
            bool wroteIv = false;
            byte[] prevBlock = null;
            var inputBuffer = new List<byte>();
            return new CryptorBlock((input) => {
                if(prevBlock == null) {
                    prevBlock = input;
                    return new byte[0];
                }

                inputBuffer.AddRange(prevBlock);
                List<byte> outputBuffer = null;
                if(input != null) {
                    //Unlike iOS, .NET has no "update" method so we have to manually
                    //break the input into blocks
                    outputBuffer = new List<byte>();
                    while(inputBuffer.Count > encryptor.InputBlockSize) {
                        var tmpBuffer = new byte[encryptor.OutputBlockSize];
                        encryptor.TransformBlock(inputBuffer.Take(encryptor.InputBlockSize).ToArray(), 0, encryptor.InputBlockSize, tmpBuffer, 0);
                        inputBuffer.RemoveRange(0, encryptor.InputBlockSize);
                        outputBuffer.AddRange(tmpBuffer);
                    }
                } else {
                    var tmp = encryptor.TransformFinalBlock(inputBuffer.ToArray(), 0, inputBuffer.Count);
                    outputBuffer = new List<byte>(tmp);
                }

                if(!wroteIv) {
                    outputBuffer.InsertRange(0, _cryptor.IV);
                    wroteIv = true;
                }

                prevBlock = input;
                return outputBuffer.ToArray();
            });
        }

        #endregion

        #region Private Methods

        private void InitCryptor()
        {
            _cryptor = Aes.Create();
            _cryptor.KeySize = KEY_SIZE * 8;
            _cryptor.BlockSize = BLOCK_SIZE * 8;
            _cryptor.Padding = PaddingMode.PKCS7;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _cryptor.Dispose();
        }

        #endregion
    }
}
