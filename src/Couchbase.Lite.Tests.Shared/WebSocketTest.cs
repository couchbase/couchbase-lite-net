//
//  WebSocketTest.cs
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

using Couchbase.Lite.DI;
using Couchbase.Lite.Sync;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using LiteCore.Interop;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Net.NetworkInformation;

using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public unsafe interface IWebSocketWrapper : IDisposable
    {
        Stream NetworkStream { get; set; }
        unsafe void CloseSocket();
        void CompletedReceive(ulong byteCount);
        void Start();
        void Write(byte[] data);
    }

    public sealed unsafe class WebSocketTest : TestCase
    {
        Type WebSocketWrapperType = typeof(WebSocketWrapper);
        Type ReachabilityType = typeof(Reachability);
        Type HttpLogicType = typeof(HTTPLogic);
        WebSocketWrapper webSocketWrapper;
        HTTPLogic hTTPLogic = new HTTPLogic(new Uri("ws://localhost:4984"));

        public WebSocketTest(ITestOutputHelper output) : base(output)
        {
            var dict = new ReplicatorOptionsDictionary();
            var authDict = new AuthOptionsDictionary();
            authDict.Username = "admin";
            authDict.Password = "admintest";
            authDict.Type = AuthType.HttpBasic;
            dict.Auth = authDict;
            dict.Headers.Add("User-Agent", "CouchbaseLite/2.1.0 (.NET; Microsoft Windows 10.0.17134 ) Build/0 LiteCore/ (1261) Commit/b01fad7");
            dict.Add("WS-Protocols", "BLIP_3+CBMobile_2");
            var uri = new Uri("ws://localhost:4984");

            var s = new LiteCore.Interop.C4Socket();
            webSocketWrapper = new WebSocketWrapper(uri, (&s), dict);
        }

        [Fact]
        public void TestBase64Digest()
        {
            string input = "test coverage";

            var method = WebSocketWrapperType.GetMethod("Base64Digest", BindingFlags.NonPublic | BindingFlags.Static);
            var res = method!.Invoke(null, new object[1] { input });

            res.ShouldBe("/EOfaNlb2IwudhJuOmFE3Ps9D38=");
        }

        [Fact]
        public void TestCheckHeader()
        {
            HttpMessageParser parser = new HttpMessageParser("HTTP/1.1 401 Unauthorized");
            parser.Append("Content - Type: application / json");
            parser.Append("Server: Couchbase Sync Gateway/2.0.0");
            parser.Append("Www-Authenticate: Basic realm=\"Couchbase Sync Gateway\"");
            parser.Append("Date: Mon, 06 Aug 2018 17:44:51 GMT");
            parser.Append("Content-Length: 50");
            string key = "Connection";
            string expectedValue = "Upgrade";
            bool caseSens = false; // true

            var method = WebSocketWrapperType.GetMethod("CheckHeader", BindingFlags.NonPublic | BindingFlags.Static);
            var res = method!.Invoke(null, new object[4] { parser, key, expectedValue, caseSens });

            res.ShouldBe(false);

            parser = new HttpMessageParser("HTTP/1.1 101 Switching Protocols");
            parser.Append("Upgrade: websocket");
            parser.Append("Connection: Upgrade");
            parser.Append("Sec-WebSocket-Accept: R3ztu/aZLI+izEEtS3Ao1kzub4s=");
            parser.Append("Sec-WebSocket-Protocol: BLIP_3+CBMobile_2");

            key = "Connection";
            expectedValue = "Upgrade";
            caseSens = false;

            method = WebSocketWrapperType.GetMethod("CheckHeader", BindingFlags.NonPublic | BindingFlags.Static);
            res = method!.Invoke(null, new object[4] { parser, key, expectedValue, caseSens });

            res.ShouldBe(true);

            key = "Upgrade";
            expectedValue = "websocket";
            caseSens = false;

            method = WebSocketWrapperType.GetMethod("CheckHeader", BindingFlags.NonPublic | BindingFlags.Static);
            res = method!.Invoke(null, new object[4] { parser, key, expectedValue, caseSens });

            res.ShouldBe(true);

            key = "Sec-WebSocket-Accept";
            expectedValue = "R3ztu/aZLI+izEEtS3Ao1kzub4s=";
            caseSens = true;

            method = WebSocketWrapperType.GetMethod("CheckHeader", BindingFlags.NonPublic | BindingFlags.Static);
            res = method!.Invoke(null, new object[4] { parser, key, expectedValue, caseSens });

            res.ShouldBe(true);
        }
    }
}
