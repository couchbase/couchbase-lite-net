
//
//  Program.cs
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
using Couchbase.Lite;
using Couchbase.Lite.Listener;
using Couchbase.Lite.Listener.Tcp;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite.Security;

namespace Listener
{
    class MainClass
    {
        private const int port = 59840;
        public static void Main(string[] args)
        {
            var cert = SSLGenerator.GenerateCert("foo", "miracle");
            SSLGenerator.WriteCertificate("/Users/borrrden/foo2.pfx", cert);
                
            CouchbaseLiteServiceListener listener = new CouchbaseLiteTcpListener(Manager.SharedInstance, port, CouchbaseLiteTcpOptions.Default, new X509Certificate2(cert, "foo"));
            listener.SetPasswords(new Dictionary<string, string> { { "jim", "borden" } });
            listener.Start();

            Console.WriteLine("LISTENING...");
            Console.ReadKey(true);
            listener.Stop();

        }
    }
}
