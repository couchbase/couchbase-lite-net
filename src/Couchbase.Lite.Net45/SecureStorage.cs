//
//  SecureStorage.cs
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite.Util;
using Microsoft.IO;

namespace Couchbase.Lite
{
    internal class SecureStorage : ISecureStorage
    {
        private static readonly byte[] _Entropy = new byte[] { 9, 8, 7, 6, 5 };
        private readonly MessageDigest _digest = MessageDigest.GetInstance("SHA-1");
        private static readonly string _BaseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cbl_store");

        static SecureStorage()
        {
            Directory.CreateDirectory(_BaseDirectory);
        }

        public void Delete(SecureStorageRequest request)
        {
            var filename = GetFilename(request);
            File.Delete(filename);
        }

        public IEnumerable<byte> Read(SecureStorageRequest request)
        {
            var filename = GetFilename(request);
            if(!File.Exists(filename)) {
                return null;
            }

            using(var fs = File.OpenRead(filename))
            using(var buffer = RecyclableMemoryStreamManager.SharedInstance.GetStream("SecureStorage", (int)fs.Length)) {
                fs.CopyTo(buffer);
                return ProtectedData.Unprotect(buffer.GetBuffer(), _Entropy, DataProtectionScope.CurrentUser);
            }
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

            var filename = GetFilename(request);
            using(var fs = File.OpenWrite(filename)) {
                var encrypted = ProtectedData.Protect(request.Data.ToArray(), _Entropy, DataProtectionScope.CurrentUser);
                fs.Write(encrypted, 0, encrypted.Length);
            }
        }

        private string GetFilename(SecureStorageRequest request)
        {
            var filenameBytes = Encoding.UTF8.GetBytes($"{request.Service}{request.Label}{request.Account}");
            _digest.Reset();
            _digest.Update(filenameBytes);
            return Path.Combine(_BaseDirectory, $"{Convert.ToBase64String(_digest.Digest())}.bin");
        }
    }
}
