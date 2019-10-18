﻿//
//  CSharpTest.cs
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
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Support;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using FluentAssertions;
using LiteCore.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Extensions = Couchbase.Lite.Util.Extensions;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Fleece;
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
#else
        public CSharpTest()
#endif
        {
        }

#if COUCHBASE_ENTERPRISE
        [Fact]
        public void TestEncryptionKey()
        {
            byte[] derivedData;
            using (var key = new EncryptionKey("test")) {
                key.KeyData.Should().NotBeNullOrEmpty();
                derivedData = key.KeyData;
            }

            using (var key = new EncryptionKey(derivedData)) {
                key.HexData.Should().Be(BitConverter.ToString(derivedData).Replace("-", String.Empty).ToLower());
                key.KeyData.Should().Equal(derivedData);
            }

            Action badAction = (() => new EncryptionKey(new byte[] {1, 2, 3, 4}));
            badAction.Should().Throw<ArgumentOutOfRangeException>("because the encryption key data must be 32 bytes");
            badAction = (() => new EncryptionKey("foo", new byte[] {1}, 200));
            badAction.Should().Throw<ArgumentOutOfRangeException>("because the salt must be at least 4 bytes");
            badAction = (() => new EncryptionKey("foo", new byte[] {1, 2, 3, 4, 5}, 5));
            badAction.Should().Throw<ArgumentOutOfRangeException>("because the rounds must be >= 200");
        }
#endif

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
                    var flValue = NativeRaw.FLValue_FromData((FLSlice) flData, FLTrust.Trusted);
                    var mArr = new FleeceMutableArray(new MValue(flValue), mRoot);
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

                    var mVal = new MValue();
                    mVal.Dispose();
                }
            } finally {
                Native.FLSliceResult_Release(flData);
            }

            var mroot = new MRoot();
        }

        [Fact]
        public void TestArrayObject()
        {
            var now = DateTimeOffset.UtcNow;
            var nowStr = now.ToString("o");
            var ao = new MutableArrayObject();
            var blob = new Blob("text/plain", Encoding.UTF8.GetBytes("Winter is coming"));
            var dict = new MutableDictionaryObject(new Dictionary<string, object> {["foo"] = "bar"});
            ao.AddFloat(1.1f);
            ao.AddBlob(blob);
            ao.AddDate(now);
            ao.AddDictionary(dict);
            
            var arr = new MutableArrayObject(new[] {5, 4, 3, 2, 1});
            ao.InsertInt(0, 42);
            ao.InsertLong(0, Int64.MaxValue);
            ao.InsertFloat(0, 3.14f);
            ao.InsertDouble(0, Math.PI);
            ao.InsertBoolean(0, true);
            ao.InsertBlob(0, blob);
            ao.InsertDate(0, now);
            ao.InsertArray(0, arr);
            ao.InsertDictionary(0, dict);
            ao.Should().Equal(dict, arr, nowStr, blob, true, Math.PI, 3.14f, Int64.MaxValue, 42, 1.1f, blob,
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
            ao.Should().Equal(Int64.MaxValue, 3.14f, Math.PI, true, blob, arr, dict, nowStr, 42, 1.1f, blob,
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
                    var flValue = NativeRaw.FLValue_FromData((FLSlice) flData, FLTrust.Trusted);
                    var mDict = new FleeceMutableDictionary(new MValue(flValue), mRoot);
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
                    dict["dict"].As<IDictionary<string, object>>().Should().BeEquivalentTo(nestedDict);
                    var isContain = mDict.Contains("");
                    isContain.Should().BeFalse();
                }
            } finally {
                Native.FLSliceResult_Release(flData);
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
            parser.Headers["X-XSS-Protection"].Should().Be("1; mode=block");
            parser.Headers["X-Frame-Options"].Should().Be("SAMEORIGIN");
            parser.Headers["Cache-Control"].Should().Be("private, max-age=0");
            parser.Headers["P3P"].Should().Be("CP=\"This is not a P3P policy! See g.co/p3phelp for more info.\"");
            parser.Headers["Set-Cookie"].Should().Be(
                "1P_JAR=2017-10-13-05; expires=Fri, 20-Oct-2017 05:54:52 GMT; path=/; domain=.google.co.jp,NID=114=Vzr79B7ISI0vlP54dhHQ1lyoyqxePhvy_k3w2ofp1oce73oG3m9ltBiUgdQNj4tSMkp-oWtzmhUi3rf314Fcrjy6J2DxtyEdA_suJlgfdN9973V2HO32OG9D3svImEJf; expires=Sat, 14-Apr-2018 05:54:52 GMT; path=/; domain=.google.co.jp; HttpOnly");
            parser.Headers["foo"].Should().Be("bar");

            parser = new HttpMessageParser("HTTP/1.1 200 OK");
            parser.StatusCode.Should().Be(HttpStatusCode.OK);
            parser.Reason.Should().Be("OK");
        }

        [Fact]
        public void TestHttpMessageParserWithString()
        {
            var parser = new HttpMessageParser("HTTP/1.1 200 OK");
            parser.StatusCode.Should().Be(HttpStatusCode.OK);
            parser.Reason.Should().Be("OK");
        }

        [Fact]
        public void TestAppenNullToHttpMessageParser()
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
            Exception e = null;
            var parser = new HttpMessageParser(Encoding.ASCII.GetBytes(httpResponse));
            try {
                parser.Append(null);
            } catch (Exception ex) {
                e = ex;
            }
            e.Should().NotBeNull("because an exception is expected");
        }

        #if !CBL_NO_EXTERN_FILES
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
                        FLValueConverter.ToCouchbaseObject(NativeRaw.FLValue_FromData((FLSlice) flData, FLTrust.Trusted), Db,
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
        #endif

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
                .Should().Throw<InvalidOperationException>("because the type is invalid");
            dict.Invoking(d => d.Add(new KeyValuePair<string, object>("type", "Bogus")))
                .Should().Throw<InvalidOperationException>("because the type is invalid");
            dict.Invoking(d => d["type"] = "Bogus")
                .Should().Throw<InvalidOperationException>("because the type is invalid");
            dict.Invoking(d => d.Remove("type"))
                .Should().Throw<InvalidOperationException>("because the type key is required");
            dict.Invoking(d => d.Remove(new KeyValuePair<string, object>("type", "Basic")))
                .Should().Throw<InvalidOperationException>("because the type key is required");
            dict.Clear();
            dict.Count.Should().Be(0);
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
        public void TestGettingPortFromHTTPLogic()
        {
            var logic = new HTTPLogic(new Uri("ws://192.168.0.1:59840"));
            logic.Port.Should().Be(59840);
            logic.Credential = new NetworkCredential("user", "password");
            logic.Credential.UserName.Should().Be("user");
            logic.Credential.Password.Should().Be("password");
            logic.Credential.UserName = "newuser";
            logic.Credential.Password = "newpassword";
            logic.Credential.UserName.Should().Be("newuser");
            logic.Credential.Password.Should().Be("newpassword");
            var proxyRequest = logic.ProxyRequest("", "");
            logic.HasProxy = false;
        }

        [Fact]
        public void TestBasicAuthenticator()
        {
            ReplicatorOptionsDictionary options = new ReplicatorOptionsDictionary();
            var auth = new BasicAuthenticator("user", "password");
            auth.Username.Should().Be("user");
            auth.Password.Should().Be("password");
            auth.Authenticate(options);
            options.Auth.Username.Should().Be("user");
            options.Auth.Password.Should().Be("password");
            options.Auth.Type.Should().Be(AuthType.HttpBasic);
        }

        [Fact]
        public void TestSessionAuthenticator()
        {
            ReplicatorOptionsDictionary options = new ReplicatorOptionsDictionary();
            var auth = new SessionAuthenticator("justSessionID");
            var auth2 = new SessionAuthenticator("sessionId", "myNameIsCookie");
            auth.SessionID.Should().Be("justSessionID");
            auth2.SessionID.Should().Be("sessionId");
            auth2.CookieName.Should().Be("myNameIsCookie");
            auth2.Authenticate(options);
            options.Cookies.Count.Should().BeGreaterThan(0);
            options.Cookies.First().Name.Should().Be("myNameIsCookie");
        }

        [Fact]
        public void TestFLEncode()
        {
            TestRoundTrip(42);
            TestRoundTrip(Int64.MinValue);
            TestRoundTrip((ulong)Int64.MaxValue);
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
        public unsafe void TestConvertError()
        {
            var exceptions = new Exception[]
            {
                new SocketException((int) SocketError.HostNotFound),
                new SocketException((int) SocketError.HostUnreachable),
                new SocketException((int) SocketError.TimedOut),
                new SocketException((int) SocketError.ConnectionAborted),
                new SocketException((int) SocketError.ConnectionRefused),
                #if !WINDOWS_UWP
                new AuthenticationException("The remote certificate is invalid according to the validation procedure."),
                #endif
                new InvalidOperationException("Test message")
            };

            var errors = new[]
            {
                new C4Error(C4NetworkErrorCode.UnknownHost),
                new C4Error(C4NetworkErrorCode.DNSFailure),
                new C4Error(C4NetworkErrorCode.Timeout),
                new C4Error(C4ErrorDomain.POSIXDomain, PosixBase.GetCode(nameof(PosixWindows.ECONNRESET))),
                new C4Error(C4ErrorDomain.POSIXDomain, PosixBase.GetCode(nameof(PosixWindows.ECONNREFUSED))),
                #if !WINDOWS_UWP
                new C4Error(C4NetworkErrorCode.TLSCertUntrusted),
                #endif
                new C4Error(C4ErrorDomain.LiteCoreDomain, (int)C4ErrorCode.UnexpectedError) 
            };

            foreach (var pair in exceptions.Zip(errors, (a, b) => new { a, b })) {
                C4Error tmp;
                Status.ConvertNetworkError(pair.a, &tmp);
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
                ["dict"] = new MutableDictionaryObject().SetString("foo", "bar"),
                ["array"] = new MutableArrayObject().AddInt(42)
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

        [Fact]
        public async Task TestSerialQueue()
        {
            var queue = new SerialQueue();
            var now = DateTime.Now;
            var then = now;
            var ignore = queue.DispatchAfter(() => then = DateTime.Now, TimeSpan.FromSeconds(1));
            await Task.Delay(250);
            then.Should().Be(now);
            await Task.Delay(800);
            then.Should().NotBe(now);

            var testBool = false;
            queue.DispatchSync(() => Volatile.Read(ref testBool)).Should().BeFalse();

            var t = queue.DispatchAfter(() => Volatile.Read(ref testBool), TimeSpan.FromMilliseconds(500));
            Volatile.Write(ref testBool, true);
            (await t).Should().BeTrue();
        }

        [Fact]
        public void TestTransientAndNetworkDependent()
        {
            foreach (var err in new[]
                { "ENETRESET", "ECONNABORTED", "ECONNRESET", "ETIMEDOUT", "ECONNREFUSED" }) {
                var code = PosixBase.GetCode(err);
                Native.c4error_mayBeTransient(new C4Error(C4ErrorDomain.POSIXDomain, code)).Should().BeTrue($"because {err} should be transient");
            }

            foreach (var err in new[]
                { "ENETDOWN", "ENETUNREACH", "ENOTCONN", "ETIMEDOUT", "EHOSTUNREACH", "EADDRNOTAVAIL" }) {
                var code = PosixBase.GetCode(err);
                Native.c4error_mayBeNetworkDependent(new C4Error(C4ErrorDomain.POSIXDomain, code)).Should().BeTrue($"because {err} should be network dependent");
            }
        }

        [Fact]
        public void TestAutoconvertJson()
        {
            var jVal = new JValue("test");
            var jArray = new JArray(jVal);
            var jObj = new JObject { ["test"] = jVal };

            DataOps.ToCouchbaseObject(jVal).Should().Be("test");
            DataOps.ToCouchbaseObject(jArray).As<MutableArrayObject>()[0].String.Should().Be("test");
            DataOps.ToCouchbaseObject(jObj).As<MutableDictionaryObject>()["test"].String.Should().Be("test");

            var jsonString = "{\"level1\":{\"foo\":\"bar\"},\"level2\":{\"list\":[1, 3.14, \"s\"]}, \"$type\":\"JSON .NET Object\"}";
            var json = JsonConvert.DeserializeObject<IDictionary<string, object>>(jsonString);
            var converted = DataOps.ToCouchbaseObject(json) as MutableDictionaryObject;
            converted.Should().NotBeNull();

            converted["level1"]["foo"].String.Should().Be("bar");
            converted["level2"]["list"][0].Int.Should().Be(1);
            converted["level2"]["list"][1].Double.Should().Be(3.14);
            converted["level2"]["list"][2].String.Should().Be("s");
            converted["$type"].String.Should().Be("JSON .NET Object");
        }

        [Fact]
        public void TestCreateExceptions()
        {
            var fleeceException = CouchbaseException.Create(new C4Error(FLError.EncodeError)) as CouchbaseFleeceException;
            fleeceException.Should().NotBeNull();
            fleeceException.Error.Should().Be((int) FLError.EncodeError);
            fleeceException.Domain.Should().Be(CouchbaseLiteErrorType.Fleece);
            fleeceException = new CouchbaseFleeceException(FLError.JSONError);
            fleeceException = new CouchbaseFleeceException(FLError.JSONError, "json error");

            var sqliteException =
                CouchbaseException.Create(new C4Error(C4ErrorDomain.SQLiteDomain, (int) SQLiteStatus.Misuse)) as CouchbaseSQLiteException;
            sqliteException.Should().NotBeNull();
            sqliteException.BaseError.Should().Be(SQLiteStatus.Misuse);
            sqliteException.Error.Should().Be((int) SQLiteStatus.Misuse);
            sqliteException.Domain.Should().Be(CouchbaseLiteErrorType.SQLite);
            sqliteException = new CouchbaseSQLiteException(999991);
            sqliteException = new CouchbaseSQLiteException(999991, "new sql lite exception");

            var webSocketException = CouchbaseException.Create(new C4Error(C4ErrorDomain.WebSocketDomain, 1003)) as CouchbaseWebsocketException;
            webSocketException.Error.Should().Be(CouchbaseLiteError.WebSocketDataError);
            webSocketException.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite);
            webSocketException = new CouchbaseWebsocketException(10404);
            webSocketException = new CouchbaseWebsocketException(10404, "HTTP Not Found");

            var posixException = CouchbaseException.Create(new C4Error(C4ErrorDomain.POSIXDomain, PosixBase.EACCES)) as CouchbasePosixException;
            posixException.Error.Should().Be(PosixBase.EACCES);
            posixException.Domain.Should().Be(CouchbaseLiteErrorType.POSIX);
            posixException = new CouchbasePosixException(999992);
            posixException = new CouchbasePosixException(999992, "new posix lite exception");

            var networkException =
                CouchbaseException.Create(new C4Error(C4NetworkErrorCode.InvalidURL)) as CouchbaseNetworkException;
            networkException.Error.Should().Be(CouchbaseLiteError.InvalidUrl);
            networkException.Domain.Should().Be(CouchbaseLiteErrorType.CouchbaseLite);
            networkException = new CouchbaseNetworkException(HttpStatusCode.BadRequest);
            networkException = new CouchbaseNetworkException(C4NetworkErrorCode.InvalidURL);
            networkException = new CouchbaseNetworkException(C4NetworkErrorCode.InvalidURL, "You are trying to connect to an invalid url");

            var runtimeException = new RuntimeException("runtime exception");
        }

        [Fact]
        [ForIssue("couchbase-lite-net/1048")]
        public void TestDictionaryWithULong()
        {
            using (var doc = new MutableDocument("test_ulong")) {
                var dict = new MutableDictionaryObject();
                dict.SetValue("high_value", UInt64.MaxValue);
                doc.SetDictionary("nested", dict);
                Db.Save(doc);
            }

            using (var doc = Db.GetDocument("test_ulong")) {
                doc["nested"]["high_value"].Value.Should().Be(UInt64.MaxValue);
            }
        }

        #if !NETCOREAPP2_0

        [Fact]
        public async Task TestMainThreadScheduler()
        {
            var scheduler = Service.GetInstance<IMainThreadTaskScheduler>();
            var onMainThread = await Task.Factory.StartNew(() => scheduler.IsMainThread);
            onMainThread.Should().BeFalse();

            var t = new Task<bool>(() => scheduler.IsMainThread);
            t.Start(scheduler.AsTaskScheduler());
            onMainThread = await t;
            onMainThread.Should().BeTrue();
        }

#else

        #endif

        [Fact]
        public void TestCBDebugItemsMustNotBeNull()
        {
            List<object> list = new List<object>();
            list.Add("couchbase");
            list.Add(null);
            list.Add("debug");
            Action badAction = (() =>
            CBDebug.ItemsMustNotBeNull(
                WriteLog.To.Query, 
                nameof(CSharpTest), 
                nameof(TestCBDebugItemsMustNotBeNull), 
                list));
            badAction.Should().Throw<ArgumentNullException>("because the item in enumeration cannot be null.");

            list.RemoveAt(1);
            var items = CBDebug.ItemsMustNotBeNull(
                WriteLog.To.Query,
                nameof(CSharpTest),
                nameof(TestCBDebugItemsMustNotBeNull),
                list);

            items.Count().Should().Be(2);
            items.ElementAt(1).Should().Be("debug");
        }

        private unsafe void TestRoundTrip<T>(T item)
        {
            using (var encoded = item.FLEncode()) {
                var flValue = NativeRaw.FLValue_FromData((FLSlice) encoded, FLTrust.Trusted);
                ((IntPtr) flValue).Should().NotBe(IntPtr.Zero);
                if (item is IEnumerable enumerable && !(item is string)) {
                    ((IEnumerable) FLSliceExtensions.ToObject(flValue)).Should().BeEquivalentTo(enumerable);
                } else {
                    Extensions.CastOrDefault<T>(FLSliceExtensions.ToObject(flValue)).Should().Be(item);
                }
            }
        }
    }
}
