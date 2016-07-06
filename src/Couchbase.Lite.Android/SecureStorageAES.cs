//
//  SecureStorageAES.cs
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
using Android.Security.Keystore;
using Couchbase.Lite.Util;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace Couchbase.Lite
{
    internal sealed class SecureStorageAES : ISecureStorage
    {
        private const string Tag = nameof(SecureStorageAES);
        private const string ServiceName = "CouchbaseLite";
        private const string Alias = "CouchbaseLiteSecureStorage";
        

        static SecureStorageAES()
        {
            InitializePrivateKey();
        }

        public void Delete(SecureStorageRequest request)
        {
            var prefs = Application.Context.GetSharedPreferences(ServiceName, FileCreationMode.Private);
            var editor = prefs.Edit();
            editor.Remove(GetKey(request));
        }

        public IEnumerable<byte> Read(SecureStorageRequest request)
        {
            var key = GetKey(request);
            var prefs = Application.Context.GetSharedPreferences(ServiceName, FileCreationMode.Private);
            var saved = prefs.GetString(key, null);
            if(saved == null) {
                return null;
            }

            var data = Convert.FromBase64String(saved);
            var iv = Convert.FromBase64String(prefs.GetString($"{key}_iv", null));
            var keyStore = KeyStore.GetInstance("AndroidKeyStore");
            keyStore.Load(null);
            var entry = (KeyStore.SecretKeyEntry)keyStore.GetEntry(Alias, null);
            var cipher = Cipher.GetInstance("AES/CBC/PKCS7Padding");
            cipher.Init(CipherMode.DecryptMode, entry.SecretKey, new IvParameterSpec(iv));
            data = cipher.DoFinal(data);

            return data;
        }

        public void Write(SecureStorageRequest request)
        {
            if(request == null) {
                throw new ArgumentNullException(nameof(request), "Cannot write a null storage request");
            }

            if(request.Data == null) {
                Delete(request);
                return;
            }

            var data = request.Data.ToArray();
            var iv = default(string);
            var keyStore = KeyStore.GetInstance("AndroidKeyStore");
            keyStore.Load(null);
            var entry = (KeyStore.SecretKeyEntry)keyStore.GetEntry(Alias, null);
            var cipher = Cipher.GetInstance("AES/CBC/PKCS7Padding");
            cipher.Init(CipherMode.EncryptMode, entry.SecretKey);
            iv = Convert.ToBase64String(cipher.GetIV());
            data = cipher.DoFinal(data);

            var prefs = Application.Context.GetSharedPreferences(ServiceName, FileCreationMode.Private);
            var editor = prefs.Edit();
            var key = GetKey(request);
            editor.PutString(key, Convert.ToBase64String(data));
            editor.PutString($"{key}_iv", iv);
            editor.Commit();
        }

        private string GetKey(SecureStorageRequest request)
        {
            return $"{request.Label}{request.Account}";
        }

        private static void InitializePrivateKey()
        {
            var keyStore = KeyStore.GetInstance("AndroidKeyStore");
            keyStore.Load(null);
            var entry = keyStore.GetEntry(Alias, null);
            if(entry != null && entry is KeyStore.SecretKeyEntry) {
                return;
            }

            var keyBuilder = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore");
            var spec = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt).SetBlockModes(KeyProperties.BlockModeCbc).SetEncryptionPaddings(KeyProperties.EncryptionPaddingPkcs7).Build();
            keyBuilder.Init(spec);
            keyBuilder.GenerateKey();
        }
    }
}