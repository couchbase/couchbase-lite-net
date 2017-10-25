//
//  CSHarpTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Sync;
using FluentAssertions;
using LiteCore.Interop;
using Newtonsoft.Json;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;

#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
    // NOTE: This tests classes specific to CSharp binding to LiteCore, it does not
    // need to be ported as is.  They exist to get coverage without making the other
    // tests too off track.
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class CSharpTest : TestCase
    {
#if !WINDOWS_UWP
        public CSharpTest(ITestOutputHelper output) : base(output)
        {
        }
#endif

        [Fact]
        public void TestEncryptionKey()
        {
            byte[] derivedData;
            using (var key = new EncryptionKey()) {
                key.KeyData.Should().NotBeNullOrEmpty();
                using (var key2 = new EncryptionKey()) {
                    key2.KeyData.Should().NotBeNullOrEmpty();
                    key2.KeyData.Should().NotEqual(key.KeyData);
                }

                derivedData = key.KeyData;
            }
            using (var key = new EncryptionKey(derivedData)) {
                key.HexData.Should().Be(BitConverter.ToString(derivedData).Replace("-", String.Empty).ToLower());
                key.KeyData.Should().Equal(derivedData);
            }

            Action badAction = (() => new EncryptionKey(null, null, 200));
            badAction.ShouldThrow<ArgumentNullException>();
            badAction = (() => new EncryptionKey(new byte[] {1, 2, 3, 4}));
            badAction.ShouldThrow<ArgumentOutOfRangeException>("because the encryption key data must be 32 bytes");
            badAction = (() => new EncryptionKey("foo", null, 200));
            badAction.ShouldThrow<ArgumentNullException>();
            badAction = (() => new EncryptionKey("foo", new byte[] {1}, 200));
            badAction.ShouldThrow<ArgumentOutOfRangeException>("because the salt must be at least 4 bytes");
            badAction = (() => new EncryptionKey("foo", new byte[] {1, 2, 3, 4, 5}, 5));
            badAction.ShouldThrow<ArgumentOutOfRangeException>("because the rounds must be >= 200");
        }

        [Fact]
        public unsafe void TestReadOnlyArray()
        {
            var now = DateTimeOffset.UtcNow;
            var nestedArray = new[] {1L, 2L, 3L};
            var nestedDict = new Dictionary<string, object> {["foo"] = "bar"};
            var masterData = new object[] {1, "str", nestedArray, now, nestedDict};

            var flData = new FLSliceResult();
            Db.InBatch(() =>
            {
                flData = masterData.FLEncode();
            });

            try {
                var context = new DocContext(Db, null);
                using (var mRoot = new MRoot(context)) {
                    mRoot.Context.Should().BeSameAs(context);
                    var flValue = NativeRaw.FLValue_FromTrustedData((FLSlice) flData);
                    var mArr = new MArray(new MValue(flValue), mRoot);
                    var deserializedArray = new ReadOnlyArray(mArr, false);
                    deserializedArray.GetArray(2).Should().Equal(1L, 2L, 3L);
                    deserializedArray.GetArray(3).Should().BeNull();
                    deserializedArray.GetBlob(1).Should().BeNull();
                    deserializedArray.GetDate(3).Should().Be(now);
                    deserializedArray.GetDate(4).Should().Be(DateTimeOffset.MinValue);
                    deserializedArray[1].ToString().Should().Be("str");
                    deserializedArray.GetString(2).Should().BeNull();
                    deserializedArray.GetDictionary(4).Should().BeEquivalentTo(nestedDict);
                    deserializedArray[0].ToDictionary().Should().BeNull();

                    var list = deserializedArray.ToList();
                    list[2].Should().BeAssignableTo<IList<object>>();
                    list[4].Should().BeAssignableTo<IDictionary<string, object>>();
                }

#if DEBUG
                context.Disposed.Should().BeTrue();
#endif
            } finally {
                Native.FLSliceResult_Free(flData);
            }
        }

        [Fact]
        public void TestArrayObject()
        {
            var now = DateTimeOffset.UtcNow;
            var nowStr = now.ToString("o");
            var ao = new ArrayObject();
            var blob = new Blob("text/plain", Encoding.UTF8.GetBytes("Winter is coming"));
            var dict = new DictionaryObject(new Dictionary<string, object> {["foo"] = "bar"});
            ao.Add(1.1f);
            ao.Add(blob);
            ao.Add(now);
            ao.Add(dict);

            var obj = new Object();
            var arr = new ArrayObject(new[] {5, 4, 3, 2, 1});
            ao.Insert(0, obj);
            ao.Insert(0, 42);
            ao.Insert(0, Int64.MaxValue);
            ao.Insert(0, 3.14f);
            ao.Insert(0, Math.PI);
            ao.Insert(0, true);
            ao.Insert(0, blob);
            ao.Insert(0, now);
            ao.Insert(0, arr);
            ao.Insert(0, dict);
            ao.Should().Equal(dict, arr, nowStr, blob, true, Math.PI, 3.14f, Int64.MaxValue, 42, obj, 1.1f, blob,
                nowStr,
                dict);

            ao.Set(0, Int64.MaxValue);
            ao.Set(1, 3.14f);
            ao.Set(2, Math.PI);
            ao.Set(3, true);
            ao.Set(4, blob);
            ao.Set(5, arr);
            ao.Set(6, dict);
            ao.Set(7, now);
            ao.Should().Equal(Int64.MaxValue, 3.14f, Math.PI, true, blob, arr, dict, nowStr, 42, obj, 1.1f, blob,
                nowStr,
                dict);
        }

        [Fact]
        public unsafe void TestReadOnlyDictionary()
        {
            var now = DateTimeOffset.UtcNow;
            var nestedArray = new[] {1L, 2L, 3L};
            var nestedDict = new Dictionary<string, object> {["foo"] = "bar"};
            var masterData = new Dictionary<string, object>
            {
                ["date"] = now,
                ["array"] = nestedArray,
                ["dict"] = nestedDict
            };

            var flData = new FLSliceResult();
            Db.InBatch(() =>
            {
                flData = masterData.FLEncode();
            });

            try {
                var context = new DocContext(Db, null);
                using (var mRoot = new MRoot(context)) {
                    mRoot.Context.Should().BeSameAs(context);
                    var flValue = NativeRaw.FLValue_FromTrustedData((FLSlice) flData);
                    var mDict = new MDict(new MValue(flValue), mRoot);
                    var deserializedDict = new ReadOnlyDictionary(mDict, false);

                    deserializedDict["bogus"].ToBlob().Should().BeNull();
                    deserializedDict["date"].ToDate().Should().Be(now);
                    deserializedDict.GetDate("bogus").Should().Be(DateTimeOffset.MinValue);
                    deserializedDict.GetArray("array").Should().Equal(1L, 2L, 3L);
                    deserializedDict.GetArray("bogus").Should().BeNull();
                    deserializedDict.GetDictionary("dict").Should().BeEquivalentTo(nestedDict);
                    deserializedDict.GetDictionary("bogus").Should().BeNull();

                    var dict = deserializedDict.ToDictionary();
                    dict["array"].As<IList>().Should().Equal(1L, 2L, 3L);
                    dict["dict"].As<IDictionary<string, object>>().ShouldBeEquivalentTo(nestedDict);
                }

#if DEBUG
                context.Disposed.Should().BeTrue();
#endif
            } finally {
                Native.FLSliceResult_Free(flData);
            }
        }

        [Fact]
        public void TestHttpMessageParser()
        {
            var httpResponse =
                @"HTTP/1.1 200 OK
X-XSS-Protection: 1; mode=block
X-Frame-Options: SAMEORIGIN
Cache-Control: private, max-age=0
Content-Type: text/html; charset=UTF-8
Date: Fri, 13 Oct 2017 05:54:52 GMT
Expires: -1
P3P: CP=""This is not a P3P policy! See g.co/p3phelp for more info.""
Set-Cookie: 1P_JAR=2017-10-13-05; expires=Fri, 20-Oct-2017 05:54:52 GMT; path=/; domain=.google.co.jp,NID=114=Vzr79B7ISI0vlP54dhHQ1lyoyqxePhvy_k3w2ofp1oce73oG3m9ltBiUgdQNj4tSMkp-oWtzmhUi3rf314Fcrjy6J2DxtyEdA_suJlgfdN9973V2HO32OG9D3svImEJf; expires=Sat, 14-Apr-2018 05:54:52 GMT; path=/; domain=.google.co.jp; HttpOnly
Server: gws
Accept-Ranges: none
Vary: Accept-Encoding
Transfer-Encoding: chunked";

            var parser = new HttpMessageParser(Encoding.ASCII.GetBytes(httpResponse));
            parser.Append("foo: bar");
            parser.StatusCode.Should().Be(HttpStatusCode.OK);
            parser.Reason.Should().Be("OK");
            parser.Headers["x-xss-protection"].Should().Be("1; mode=block");
            parser.Headers["x-frame-options"].Should().Be("SAMEORIGIN");
            parser.Headers["cache-control"].Should().Be("private, max-age=0");
            parser.Headers["p3p"].Should().Be("CP=\"This is not a P3P policy! See g.co/p3phelp for more info.\"");
            parser.Headers["set-cookie"].Should().Be(
                "1P_JAR=2017-10-13-05; expires=Fri, 20-Oct-2017 05:54:52 GMT; path=/; domain=.google.co.jp,NID=114=Vzr79B7ISI0vlP54dhHQ1lyoyqxePhvy_k3w2ofp1oce73oG3m9ltBiUgdQNj4tSMkp-oWtzmhUi3rf314Fcrjy6J2DxtyEdA_suJlgfdN9973V2HO32OG9D3svImEJf; expires=Sat, 14-Apr-2018 05:54:52 GMT; path=/; domain=.google.co.jp; HttpOnly");
            parser.Headers["foo"].Should().Be("bar");

            parser = new HttpMessageParser("HTTP/1.1 200 OK");
            parser.StatusCode.Should().Be(HttpStatusCode.OK);
            parser.Reason.Should().Be("OK");
        }

        [Fact]
        public unsafe void TestSerializationRoundTrip()
        {
            var masterList = new List<Dictionary<string, object>>();
            var settings = new JsonSerializerSettings {
                DateParseHandling = DateParseHandling.None
            };
            var s = JsonSerializer.CreateDefault(settings);

            ReadFileByLines("C/tests/data/iTunesMusicLibrary.json", line =>
            {
                using (var reader = new JsonTextReader(new StringReader(line))) {
                    masterList.Add(s.Deserialize<Dictionary<string, object>>(reader));
                }

                return true;
            });

            var retrieved = default(List<Dictionary<string, object>>);
            Db.InBatch(() =>
            {
                using (var flData = masterList.FLEncode()) {
                    retrieved =
                        FLValueConverter.ToCouchbaseObject(NativeRaw.FLValue_FromTrustedData((FLSlice) flData), Db,
                                true, typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(object))) as
                            List<Dictionary<string, object>>;
                }
            });

                

            var i = 0;
            foreach (var entry in retrieved) {
                var entry2 = masterList[i];
                foreach (var key in entry.Keys) {
                    entry[key].Should().Be(entry2[key]);
                }

                i++;
            }
        }

        [Fact]
        public void TestOptionsDictionary()
        {
            var dict = new AuthOptionsDictionary {
                Password = "pass",
                Type = AuthType.HttpBasic,
                Username = "user"
            };

            dict.Validate("type", "Basic").Should().BeTrue();
            dict.Validate("type", "Bogus").Should().BeFalse();
            dict.Freeze();

            dict.Invoking(d => d["foo"] = "bar")
                .ShouldThrow<InvalidOperationException>("because the dictionary was frozen");
        }
        
        [Fact]
        public void TestNewlineInHeader()
        {
            var logic = new HTTPLogic(new Uri("http://www.couchbase.com"));
            logic["User-Agent"] = "BadUser/1.0\r\n";
            logic["Cookie"] = null;
            var dataString = Encoding.ASCII.GetString(logic.HTTPRequestData());
            dataString.IndexOf("\r\n\r\n").Should().Be(dataString.Length - 4);
        }
    }
}