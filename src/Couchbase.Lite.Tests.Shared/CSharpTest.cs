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
using System.Security.Authentication;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using FluentAssertions;
using LiteCore.Interop;
using Newtonsoft.Json;

using Extensions = Couchbase.Lite.Util.Extensions;
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

            Action badAction = (() => new EncryptionKey(new byte[] {1, 2, 3, 4}));
            badAction.ShouldThrow<ArgumentOutOfRangeException>("because the encryption key data must be 32 bytes");
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
                    var deserializedArray = new ArrayObject(mArr, false);
                    deserializedArray.GetArray(2).Should().Equal(1L, 2L, 3L);
                    deserializedArray.GetArray(3).Should().BeNull();
                    deserializedArray.GetBlob(1).Should().BeNull();
                    deserializedArray.GetDate(3).Should().Be(now);
                    deserializedArray.GetDate(4).Should().Be(DateTimeOffset.MinValue);
                    deserializedArray[1].ToString().Should().Be("str");
                    deserializedArray.GetString(2).Should().BeNull();
                    deserializedArray.GetDictionary(4).Should().BeEquivalentTo(nestedDict);
                    deserializedArray[0].Dictionary.Should().BeNull();

                    var list = deserializedArray.ToList();
                    list[2].Should().BeAssignableTo<IList<object>>();
                    list[4].Should().BeAssignableTo<IDictionary<string, object>>();
                }
            } finally {
                Native.FLSliceResult_Free(flData);
            }
        }

        [Fact]
        public void TestArrayObject()
        {
            var now = DateTimeOffset.UtcNow;
            var nowStr = now.ToString("o");
            var ao = new MutableArray();
            var blob = new Blob("text/plain", Encoding.UTF8.GetBytes("Winter is coming"));
            var dict = new MutableDictionary(new Dictionary<string, object> {["foo"] = "bar"});
            ao.AddFloat(1.1f);
            ao.AddBlob(blob);
            ao.AddDate(now);
            ao.AddDictionary(dict);

            var obj = new Object();
            var arr = new MutableArray(new[] {5, 4, 3, 2, 1});
            ao.InsertValue(0, obj);
            ao.InsertInt(0, 42);
            ao.InsertLong(0, Int64.MaxValue);
            ao.InsertFloat(0, 3.14f);
            ao.InsertDouble(0, Math.PI);
            ao.InsertBoolean(0, true);
            ao.InsertBlob(0, blob);
            ao.InsertDate(0, now);
            ao.InsertArray(0, arr);
            ao.InsertDictionary(0, dict);
            ao.Should().Equal(dict, arr, nowStr, blob, true, Math.PI, 3.14f, Int64.MaxValue, 42, obj, 1.1f, blob,
                nowStr,
                dict);

            ao.SetLong(0, Int64.MaxValue);
            ao.SetFloat(1, 3.14f);
            ao.SetDouble(2, Math.PI);
            ao.SetBoolean(3, true);
            ao.SetBlob(4, blob);
            ao.SetArray(5, arr);
            ao.SetDictionary(6, dict);
            ao.SetDate(7, now);
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
                    var deserializedDict = new DictionaryObject(mDict, false);

                    deserializedDict["bogus"].Blob.Should().BeNull();
                    deserializedDict["date"].Date.Should().Be(now);
                    deserializedDict.GetDate("bogus").Should().Be(DateTimeOffset.MinValue);
                    deserializedDict.GetArray("array").Should().Equal(1L, 2L, 3L);
                    deserializedDict.GetArray("bogus").Should().BeNull();
                    deserializedDict.GetDictionary("dict").Should().BeEquivalentTo(nestedDict);
                    deserializedDict.GetDictionary("bogus").Should().BeNull();

                    var dict = deserializedDict.ToDictionary();
                    dict["array"].As<IList>().Should().Equal(1L, 2L, 3L);
                    dict["dict"].As<IDictionary<string, object>>().ShouldBeEquivalentTo(nestedDict);
                }
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

            dict["type"].Should().Be("Basic");

            dict.Validate("type", "Basic").Should().BeTrue();
            dict.Validate("type", "Bogus").Should().BeFalse();
            dict.Invoking(d => d.Add("type", "Bogus"))
                .ShouldThrow<InvalidOperationException>("because the type is invalid");
            dict.Invoking(d => d.Add(new KeyValuePair<string, object>("type", "Bogus")))
                .ShouldThrow<InvalidOperationException>("because the type is invalid");
            dict.Invoking(d => d["type"] = "Bogus")
                .ShouldThrow<InvalidOperationException>("because the type is invalid");
            dict.Invoking(d => d.Remove("type"))
                .ShouldThrow<InvalidOperationException>("because the type key is required");
            dict.Invoking(d => d.Remove(new KeyValuePair<string, object>("type", "Basic")))
                .ShouldThrow<InvalidOperationException>("because the type key is required");

            dict.Freeze();

            dict.Invoking(d => d["foo"] = "bar")
                .ShouldThrow<InvalidOperationException>("because the dictionary was frozen");
            dict.Invoking(d => d.Add("foo", "bar"))
                .ShouldThrow<InvalidOperationException>("because the dictionary was frozen");
            dict.Invoking(d => d.Add(new KeyValuePair<string, object>("foo", "bar")))
                .ShouldThrow<InvalidOperationException>("because the dictionary was frozen");
            dict.Invoking(d => d.Remove("foo"))
                .ShouldThrow<InvalidOperationException>("because the dictionary was frozen");
            dict.Invoking(d => d.Remove(new KeyValuePair<string, object>("foo", "bar")))
                .ShouldThrow<InvalidOperationException>("because the dictionary was frozen");
            dict.Invoking(d => d.Clear())
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

        [Fact]
        public void TestFLEncode()
        {
            TestRoundTrip(42);
            TestRoundTrip(Int64.MinValue);
            TestRoundTrip("Fleece");
            TestRoundTrip(new Dictionary<string, object>
            {
                ["foo"] = "bar"
            });
            TestRoundTrip(new List<object> { "foo", "bar" });
            TestRoundTrip(true);
            TestRoundTrip(3.14f);
            TestRoundTrip(Math.PI);
        }

        [Fact]
        public void TestTryGetValue()
        {
            var dict = new Dictionary<string, object>
            {
                ["value"] = 1
            };

            IDictionary<string, object> idict = dict;
            IReadOnlyDictionary<string, object> roDict = dict;

            idict.TryGetValue("value", out int tmpInt).Should().BeTrue();
            tmpInt.Should().Be(1);
            idict.TryGetValue("bogus", out tmpInt).Should().BeFalse();

            tmpInt = 0;
            roDict.TryGetValue("value", out tmpInt).Should().BeTrue();
            tmpInt.Should().Be(1);
            roDict.TryGetValue("bogus", out tmpInt).Should().BeFalse();

            idict.TryGetValue("value", out DateTimeOffset date).Should().BeFalse();
            roDict.TryGetValue("value", out date).Should().BeFalse();

            idict.TryGetValue("value", out long tmpLong).Should().BeTrue();
            tmpLong.Should().Be(1L);

            tmpLong = 0L;
            idict.TryGetValue("value", out tmpLong).Should().BeTrue();
            tmpLong.Should().Be(1L);
        }

        [Fact]
        public void TestReplaceAll()
        {
            var s = "The quick brown fox jumps over the lazy dog";
            s.ReplaceAll("\\s.o.", " squaunch").Should().Be("The quick brown squaunch jumps over the lazy squaunch");
            s.ReplaceAll("bogus", "").Should().Be(s);
        }

        [Fact]
        public unsafe void TestConvertError()
        {
            var exceptions = new Exception[]
            {
                new SocketException((int) SocketError.HostNotFound),
                new SocketException((int) SocketError.HostUnreachable),
                new SocketException((int) SocketError.TimedOut),
                new SocketException((int) SocketError.ConnectionAborted),
                new SocketException((int) SocketError.ConnectionRefused),
                new AuthenticationException("The remote certificate is invalid according to the validation procedure."),
                new InvalidOperationException("Test message")
            };

            var errors = new[]
            {
                new C4Error(C4NetworkErrorCode.UnknownHost),
                new C4Error(C4NetworkErrorCode.DNSFailure),
                new C4Error(C4NetworkErrorCode.Timeout),
                new C4Error(PosixStatus.CONNRESET),
                new C4Error(PosixStatus.CONNREFUSED),
                new C4Error(C4NetworkErrorCode.TLSCertUntrusted),
                new C4Error(C4ErrorCode.RemoteError) 
            };

            foreach (var pair in exceptions.Zip(errors, (a, b) => new { a, b })) {
                C4Error tmp;
                Status.ConvertError(pair.a, &tmp);
                tmp.Should().Be(pair.b);
            }
        }

        [Fact]
        public void TestRecursiveEqual()
        {
            var numbers = new object[]
            {
                SByte.MinValue, Byte.MaxValue, Int16.MinValue, UInt16.MaxValue,
                Int32.MinValue, UInt32.MaxValue, Int64.MinValue, UInt64.MaxValue,
                Single.MaxValue, Double.MaxValue
            };

            var dict = new Dictionary<string, object>
            {
                ["string_val"] = "string",
                ["numbers"] = numbers,
                ["dict"] = new MutableDictionary().SetString("foo", "bar"),
                ["array"] = new MutableArray().AddInt(42)
            };

            dict.RecursiveEqual(dict).Should().BeTrue();
            foreach (var num in new object[] { (sbyte) 42, (short) 42, 42L }) {
                42.RecursiveEqual(num).Should().BeTrue();
            }

            foreach (var num in new object[] { (byte) 42, (ushort) 42, 42UL }) {
                42U.RecursiveEqual(num).Should().BeTrue();
            }

            foreach (var num in new object[] { 3.14f, 3.14 }) {
                3.14m.RecursiveEqual(num).Should().BeTrue();
            }
        }

        private unsafe void TestRoundTrip<T>(T item)
        {
            using (var encoded = item.FLEncode()) {
                var flValue = NativeRaw.FLValue_FromTrustedData((FLSlice) encoded);
                ((IntPtr) flValue).Should().NotBe(IntPtr.Zero);
                if (item is IEnumerable enumerable && !(item is string)) {
                    ((IEnumerable) FLSliceExtensions.ToObject(flValue)).ShouldBeEquivalentTo(enumerable);
                } else {
                    Extensions.CastOrDefault<T>(FLSliceExtensions.ToObject(flValue)).Should().Be(item);
                }
            }
        }
    }
}