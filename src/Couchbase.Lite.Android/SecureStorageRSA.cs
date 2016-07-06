//
//  SecureStorageRSA.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Security;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using Java.IO;
using Java.Math;
using Java.Security;
using Java.Util;
using Javax.Crypto;
using Javax.Security.Auth.X500;
using Microsoft.IO;

namespace Couchbase.Lite
{
    internal sealed class SecureStorageRSA : ISecureStorage
    {
        private const string CipherAlgorithm = "RSA/ECB/PKCS1Padding";
        private const string KeyPairGenAlgorithm = "RSA";
        private const string Tag = nameof(SecureStorageRSA);
        private const string ServiceName = "CouchbaseLite";
        private const string Alias = "CouchbaseLiteSecureStorage";
        private static readonly bool _HasKeyStore = Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr2;

        public SecureStorageRSA()
        {
            InitializePrivateKey();
        }

        private void InitializePrivateKey()
        {
            if(!_HasKeyStore) {
                return;
            }

            try {
                var keystore = KeyStore.GetInstance("AndroidKeyStore");
                keystore.Load(null);
                if(keystore.ContainsAlias(Alias)) {
                    return;
                }
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Unable to open Android keystore", e);
                return;
            }

            try {
                var start = Calendar.GetInstance(Locale.Default);
                var end = Calendar.GetInstance(Locale.Default);
                end.Add(CalendarField.Year, 1);
                var spec = new KeyPairGeneratorSpec.Builder(Application.Context)
                    .SetAlias(Alias)
                    .SetSubject(new X500Principal($"CN={Alias}"))
                    .SetSerialNumber(BigInteger.ValueOf(1337))
                    .SetStartDate(start.Time)
                    .SetEndDate(end.Time)
                    .Build();
                var generator = KeyPairGenerator.GetInstance(KeyPairGenAlgorithm, "AndroidKeyStore");
                generator.Initialize(spec);
                var keyPair = generator.GenerateKeyPair();
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Unable to create new key", e);
            }
        }

        private string GetKey(SecureStorageRequest request)
        {
            return $"{request.Label}{request.Account}";
        }

        private IEnumerable<byte> Decrypt(byte[] key, byte[] data)
        {
            var symmetricKey = new SymmetricKey(DecryptRSA(GetPrivateKey(), key));
            if(symmetricKey == null) {
                return null;
            }

            return symmetricKey.DecryptData(data);
        }

        private byte[][] Encrypt(byte[] data)
        {
            try {
                var symmetricKey = new SymmetricKey();
                var encryptedKey = EncryptRSA(GetPublicKey(), symmetricKey.KeyData);
                if(encryptedKey == null) {
                    return null;
                }

                var encryptedData = new byte[2][];
                encryptedData[0] = encryptedKey;
                encryptedData[1] = symmetricKey.EncryptData(data);
                return encryptedData;
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Error during encryption", e);
                return null;
            }
        }

        private IKey GetPrivateKey()
        {
            try {
                var keyStore = KeyStore.GetInstance("AndroidKeyStore");
                keyStore.Load(null);
                var entry = (KeyStore.PrivateKeyEntry)keyStore.GetEntry(Alias, null);
                return entry.PrivateKey;
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Unable to open keystore or get RSA private key", e);
                return null;
            }
        }

        private IKey GetPublicKey()
        {
            try {
                var keyStore = KeyStore.GetInstance("AndroidKeyStore");
                keyStore.Load(null);
                var entry = (KeyStore.PrivateKeyEntry)keyStore.GetEntry(Alias, null);
                return entry.Certificate.PublicKey;
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Unable to open keystore or get RSA private key", e);
                return null;
            }
        }

        private static byte[] EncryptRSA(IKey key, byte[] data)
        {
            try {
                var cipher = Cipher.GetInstance(CipherAlgorithm);
                cipher.Init(CipherMode.EncryptMode, key);
                using(var bos = RecyclableMemoryStreamManager.SharedInstance.GetStream()) {
                    using(var cos = new CipherOutputStream(bos, cipher)) {
                        cos.Write(data);
                    }
                    return bos.GetBuffer().Take((int)bos.Length).ToArray();
                }
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Unable to open keystore or encrypt AES key", e);
                return null;
            }
        }

        private static byte[] DecryptRSA(IKey key, byte[] data)
        {
            try {
                using(var bos = new ByteArrayOutputStream(2048)) {
                    var cipher = Cipher.GetInstance(CipherAlgorithm);
                    cipher.Init(CipherMode.DecryptMode, key);
                    using(var bis = RecyclableMemoryStreamManager.SharedInstance.GetStream(Tag, data, 0, data.Length))
                    using(var cis = new CipherInputStream(bis, cipher)) {
                        var read = new byte[512];
                        for(int i; (i = cis.Read(read)) != -1;) {
                            bos.Write(read, 0, i);
                        }
                    }

                    return bos.ToByteArray();
                }
            } catch(Exception e) {
                Log.To.NoDomain.E(Tag, "Unable to decrypt AES key", e);
                return null;
            }
        }

        public void Delete(SecureStorageRequest request)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<byte> Read(SecureStorageRequest request)
        {
            if(!_HasKeyStore) {
                return null;
            }

            var prefs = Application.Context.GetSharedPreferences(ServiceName, FileCreationMode.Private);
            var key = GetKey(request);
            if(!prefs.Contains($"{key}_key") || !prefs.Contains($"{key}_data")) {
                return null;
            }

            var secretKey = Convert.FromBase64String(prefs.GetString("${key}_key", null));
            var data = Convert.FromBase64String(prefs.GetString("${key}_data", null));
            return Decrypt(secretKey, data);
        }

        public void Write(SecureStorageRequest request)
        {
            if(!_HasKeyStore) {
                return;
            }

            var encrypted = Encrypt(request.Data.ToArray());
            if(encrypted == null) {
                return;
            }

            var prefs = Application.Context.GetSharedPreferences(ServiceName, FileCreationMode.Private);
            var editor = prefs.Edit();
            var key = GetKey(request);
            editor.PutString($"{key}_key", Convert.ToBase64String(encrypted[0]));
            editor.PutString($"{key}_data", Convert.ToBase64String(encrypted[1]));
            editor.Commit();
        }
    }
}