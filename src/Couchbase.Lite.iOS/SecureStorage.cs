//
//  SecureStorage.cs
//
//  Author:
//      Jim Borden  <jim.borden@couchbase.com>
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
using System.Text;
using Couchbase.Lite.Util;
using Foundation;
using Security;
using UIKit;

namespace Couchbase.Lite
{
    internal sealed class SecureStorage : ISecureStorage
    {
        private const string Tag = nameof(SecureStorage);

        public void Delete(SecureStorageRequest request)
        {
            var atts = GetKeychainAttributes(request);
            SecKeyChain.Remove(atts);
        }

        public IEnumerable<byte> Read(SecureStorageRequest request)
        {
            var attrs = GetKeychainAttributes(request);
            var err = default(SecStatusCode);
            var result = SecKeyChain.QueryAsRecord(attrs, out err);
           
            if(err != SecStatusCode.Success || result == null) {
                if(err == SecStatusCode.ItemNotFound) {
                    Log.To.Sync.I(Tag, "{0} No ID token found in Keychain", this);
                } else {
                    Log.To.Sync.W(Tag, "{0} Couldn't load ID token: {1}", this, err);
                }

                return null;
            }

            return result.ValueData.ToArray();
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

            var attrs = GetKeychainAttributes(request);
            attrs.ValueData = NSData.FromArray(request.Data.ToArray());
            attrs.CreationDate = NSDate.Now;
            attrs.ModificationDate = NSDate.Now;

            var err = SecKeyChain.Add(attrs);
            if(err == SecStatusCode.DuplicateItem) {
                Delete(request);
                err = SecKeyChain.Add(attrs);
            }

            if(err != SecStatusCode.Success) {
                Log.To.Sync.W(Tag, "{0} Couldn't save ID token: {1}", this, err);
                throw new IOException($"Couldn't save ID token: {err}");
            }

            Log.To.Sync.I(Tag, "{0} saved ID token to Keychain", this);
        }

        private SecRecord GetKeychainAttributes(SecureStorageRequest request)
        {
            return new SecRecord(SecKind.GenericPassword) {
                Service = request.Service,
                Account = request.Account,
                Label = request.Label
            };
        }
    }
}