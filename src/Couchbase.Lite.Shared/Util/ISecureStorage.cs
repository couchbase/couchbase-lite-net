//
//  ISecureStorage.cs
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
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Lite.Util
{
    internal sealed class SecureStorageRequest
    {

        public string Account { get; }

        public string Service { get; }

        public string Label { get; }

        public IEnumerable<byte> Data { get; set; }

        public SecureStorageRequest( string account, string service, string label)
        {
            Account = account;
            Service = service;
            Label = label;
        }
    }

    internal interface ISecureStorage : IInjectable
    {
        void Write(SecureStorageRequest request);

        IEnumerable<byte> Read(SecureStorageRequest request);

        void Delete(SecureStorageRequest request);
    }
}
