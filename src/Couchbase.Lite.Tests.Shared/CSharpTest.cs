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

// Jenkins is so slow that the tests often fail just because they took
// so long to run.  If running locally on a reasonable system, comment
// this out to make the tests run faster.
#define EXTRA_LONG_WRAPPER_TEST

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite;
using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Serialization;
using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;

using Shouldly;
using LiteCore.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Extensions = Couchbase.Lite.Util.Extensions;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Fleece;
using Dispatch;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Couchbase.Lite.Support;

namespace Test
{
    // NOTE: This tests classes specific to CSharp binding to LiteCore, it does not
    // need to be ported as is.  They exist to get coverage without making the other
    // tests too off track.
    public sealed class CSharpTest : TestCase
    {
#if EXTRA_LONG_WRAPPER_TEST
        private static readonly TimeSpan WrapperThreadBlockTime = TimeSpan.FromSeconds(2);
#else
        private static readonly TimeSpan WrapperThreadBlockTime = TimeSpan.FromMilliseconds(500);
#endif

        public CSharpTest(ITestOutputHelper output) : base(output)
        {
        }

#if COUCHBASE_ENTERPRISE
        [Fact]
        public void TestEncryptionKey()
        {
            byte[] derivedData;
            using (var key = new EncryptionKey("test")) {
                key.KeyData.ShouldNotBeEmpty();
                derivedData = key.KeyData;
            }

            using (var key = new EncryptionKey(derivedData)) {
                key.HexData.ShouldBe(BitConverter.ToString(derivedData).Replace("-", String.Empty).ToLower());
                key.KeyData.ShouldBe(derivedData);
            }

            Should.Throw<ArgumentOutOfRangeException>(() => new EncryptionKey([1, 2, 3, 4]), "because the encryption key data must be 32 bytes");
            Should.Throw<ArgumentOutOfRangeException>(() => new EncryptionKey("foo", [1], 200), "because the salt must be at least 4 bytes");
            Should.Throw<ArgumentOutOfRangeException>(() => new EncryptionKey("foo", [1, 2, 3, 4, 5], 5), "because the rounds must be >= 200");
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
                    mRoot.Context.ShouldBeSameAs(context);
                    FLDoc* fleeceDoc = Native.FLDoc_FromResultData(flData,
                        FLTrust.Trusted,
                        NativeSafe.c4db_getFLSharedKeys(Db.c4db), FLSlice.Null);
                    var flValue = Native.FLDoc_GetRoot(fleeceDoc);
                    var mArr = new FleeceMutableArray(new MValue(flValue), mRoot);
                    var deserializedArray = new ArrayObject(mArr, false);
                    deserializedArray.GetArray(2).ShouldBeEquivalentToFluent(new[] { 1L, 2L, 3L });
                    deserializedArray.GetArray(3).ShouldBeNull();
                    deserializedArray.GetBlob(1).ShouldBeNull();
                    deserializedArray.GetDate(3).ShouldBe(now);
                    deserializedArray.GetDate(4).ShouldBe(DateTimeOffset.MinValue);
                    deserializedArray[1].ToString().ShouldBe("str");
                    deserializedArray.GetString(2).ShouldBeNull();
                    deserializedArray.GetDictionary(4).ShouldBeEquivalentToFluent(nestedDict);
                    deserializedArray[0].Dictionary.ShouldBeNull();

                    var list = deserializedArray.ToList();
                    list[2].ShouldBeAssignableTo<IList<object>>();
                    list[4].ShouldBeAssignableTo<IDictionary<string, object>>();

                    var mVal = new MValue();
                    Native.FLDoc_Release(fleeceDoc);
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
            var dict = new MutableDictionaryObject(new Dictionary<string, object?> {["foo"] = "bar"});
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
            ao.ShouldBe([ dict, arr, nowStr, blob, true, Math.PI, 3.14f, Int64.MaxValue, 42, 1.1f, blob,
                nowStr,
                dict ]);

            ao.SetLong(0, Int64.MaxValue);
            ao.SetFloat(1, 3.14f);
            ao.SetDouble(2, Math.PI);
            ao.SetBoolean(3, true);
            ao.SetBlob(4, blob);
            ao.SetArray(5, arr);
            ao.SetDictionary(6, dict);
            ao.SetDate(7, now);
            ao.ShouldBe([Int64.MaxValue, 3.14f, Math.PI, true, blob, arr, dict, nowStr, 42, 1.1f, blob,
                nowStr,
                dict ]);
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
                    mRoot.Context.ShouldBeSameAs(context);
                    FLDoc* fleeceDoc = Native.FLDoc_FromResultData(flData,
                        FLTrust.Trusted,
                        NativeSafe.c4db_getFLSharedKeys(Db.c4db), FLSlice.Null);
                    var flValue = Native.FLDoc_GetRoot(fleeceDoc);
                    var mDict = new MDict(new MValue(flValue), mRoot);
                    var deserializedDict = new DictionaryObject(mDict, false);

                    deserializedDict["bogus"].Blob.ShouldBeNull();
                    deserializedDict["date"].Date.ShouldBe(now);
                    deserializedDict.GetDate("bogus").ShouldBe(DateTimeOffset.MinValue);
                    deserializedDict.GetArray("array").ShouldBeEquivalentToFluent(new[] { 1L, 2L, 3L });
                    deserializedDict.GetArray("bogus").ShouldBeNull();
                    deserializedDict.GetDictionary("dict").ShouldBeEquivalentToFluent(nestedDict);
                    deserializedDict.GetDictionary("bogus").ShouldBeNull();

                    var dict = deserializedDict.ToDictionary();
                    (dict["array"] as IEnumerable<object?>).ShouldBeEquivalentToFluent(new[] { 1L, 2L, 3L });
                    (dict["dict"] as IDictionary<string, object>).ShouldBeEquivalentToFluent(nestedDict);
                    var isContain = mDict.Contains("");
                    isContain.ShouldBeFalse();
                    Native.FLDoc_Release(fleeceDoc);
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
            parser.StatusCode.ShouldBe(HttpStatusCode.OK);
            parser.Reason.ShouldBe("OK");
            parser.Headers["X-XSS-Protection"].ShouldBe("1; mode=block");
            parser.Headers["X-Frame-Options"].ShouldBe("SAMEORIGIN");
            parser.Headers["Cache-Control"].ShouldBe("private, max-age=0");
            parser.Headers["P3P"].ShouldBe("CP=\"This is not a P3P policy! See g.co/p3phelp for more info.\"");
            parser.Headers["Set-Cookie"].ShouldBe(
                "1P_JAR=2017-10-13-05; expires=Fri, 20-Oct-2017 05:54:52 GMT; path=/; domain=.google.co.jp,NID=114=Vzr79B7ISI0vlP54dhHQ1lyoyqxePhvy_k3w2ofp1oce73oG3m9ltBiUgdQNj4tSMkp-oWtzmhUi3rf314Fcrjy6J2DxtyEdA_suJlgfdN9973V2HO32OG9D3svImEJf; expires=Sat, 14-Apr-2018 05:54:52 GMT; path=/; domain=.google.co.jp; HttpOnly");
            parser.Headers["foo"].ShouldBe("bar");

            parser = new HttpMessageParser("HTTP/1.1 200 OK");
            parser.StatusCode.ShouldBe(HttpStatusCode.OK);
            parser.Reason.ShouldBe("OK");
        }

        [Fact]
        public void TestHttpMessageParserWithString()
        {
            var parser = new HttpMessageParser("HTTP/1.1 200 OK");
            parser.StatusCode.ShouldBe(HttpStatusCode.OK);
            parser.Reason.ShouldBe("OK");
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
                    var gotten = s.Deserialize<Dictionary<string, object?>>(reader);
                    gotten.ShouldNotBeNull("because otherwise the JSON on disk was corrupt");
                    masterList.Add(gotten!);
                }

                return true;
            });

            var retrieved = default(List<Dictionary<string, object?>>);
            Db.InBatch(() =>
            {
                using (var flData = masterList.FLEncode()) {
                    retrieved =
                        FLValueConverter.ToCouchbaseObject(NativeRaw.FLValue_FromData((FLSlice) flData, FLTrust.Trusted), Db,
                                true, typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(object))) as
                            List<Dictionary<string, object?>>;
                }
            });

            var i = 0;
            retrieved.ShouldNotBeNull("because otherwise the fleece conversion failed");
            foreach (var entry in retrieved!) {
                var entry2 = masterList[i];
                foreach (var key in entry.Keys) {
                    entry[key].ShouldBe(entry2[key]);
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

            dict["type"].ShouldBe("Basic");

            dict.Validate("type", "Basic").ShouldBeTrue();
            dict.Validate("type", "Bogus").ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => dict.Add("type", "Bogus"), "because the type is invalid");
            Should.Throw<InvalidOperationException>(() => dict.Add(new KeyValuePair<string, object?>("type", "Bogus")), 
                "because the type is invalid");
            Should.Throw<InvalidOperationException>(() => dict["type"] = "Bogus", "because the type is invalid");
            Should.Throw<InvalidOperationException>(() => dict.Remove("type"), "because the type key is required");
            Should.Throw<InvalidOperationException>(() => dict.Remove(new KeyValuePair<string, object?>("type", "Basic")), 
                "because the type key is required");
            dict.Clear();
            dict.Count.ShouldBe(0);
        }
        
        [Fact]
        public void TestNewlineInHeader()
        {
            var logic = new HTTPLogic(new Uri("http://www.couchbase.com"));
            logic["User-Agent"] = "BadUser/1.0\r\n";
            logic["Cookie"] = null;
            var dataString = Encoding.ASCII.GetString(logic.HTTPRequestData());
            dataString.IndexOf("\r\n\r\n").ShouldBe(dataString.Length - 4);
        }

        [Fact]
        public void TestGettingPortFromHTTPLogic()
        {
            var logic = new HTTPLogic(new Uri("ws://192.168.0.1:59840"));
            logic.Port.ShouldBeEquivalentToFluent(59840);
            logic.Credential = new NetworkCredential("user", "password");
            logic.Credential.UserName.ShouldBe("user");
            logic.Credential.Password.ShouldBe("password");
            logic.Credential.UserName = "newuser";
            logic.Credential.Password = "newpassword";
            logic.Credential.UserName.ShouldBe("newuser");
            logic.Credential.Password.ShouldBe("newpassword");
            var proxyRequest = logic.ProxyRequest();
            logic.HasProxy = false;
        }

        [Fact]
        public void TestBasicAuthenticator()
        {
            ReplicatorOptionsDictionary options = new ReplicatorOptionsDictionary();
            var auth = new BasicAuthenticator("user", "password");
            auth.Username.ShouldBe("user");
            auth.Password.ShouldBe("password");
            auth.Authenticate(options);
            options.Auth.ShouldNotBeNull("because the Authenticate method should have set the Auth dict");
            options.Auth!.Username.ShouldBe("user");
            options.Auth.Password.ShouldBe("password");
            options.Auth.Type.ShouldBe(AuthType.HttpBasic);
        }

        [Fact]
        public void TestSessionAuthenticator()
        {
            ReplicatorOptionsDictionary options = new ReplicatorOptionsDictionary();
            var auth = new SessionAuthenticator("justSessionID");
            var auth2 = new SessionAuthenticator("sessionId", "myNameIsCookie");
            auth.SessionID.ShouldBe("justSessionID");
            auth2.SessionID.ShouldBe("sessionId");
            auth2.CookieName.ShouldBe("myNameIsCookie");
            auth2.Authenticate(options);
            options.Cookies.Count.ShouldBeGreaterThan(0);
            options.Cookies.First().Name.ShouldBe("myNameIsCookie");
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

            idict.TryGetValue("value", out int tmpInt).ShouldBeTrue();
            tmpInt.ShouldBe(1);
            idict.TryGetValue("bogus", out tmpInt).ShouldBeFalse();

            tmpInt = 0;
            roDict.TryGetValue("value", out tmpInt).ShouldBeTrue();
            tmpInt.ShouldBe(1);
            roDict.TryGetValue("bogus", out tmpInt).ShouldBeFalse();

            idict.TryGetValue("value", out DateTimeOffset date).ShouldBeFalse();
            roDict.TryGetValue("value", out date).ShouldBeFalse();

            idict.TryGetValue("value", out long tmpLong).ShouldBeTrue();
            tmpLong.ShouldBe(1L);

            tmpLong = 0L;
            idict.TryGetValue("value", out tmpLong).ShouldBeTrue();
            tmpLong.ShouldBe(1L);
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
                new InvalidOperationException("Test message")
            };

            var errors = new[]
            {
                new C4Error(C4NetworkErrorCode.UnknownHost),
                new C4Error(C4NetworkErrorCode.HostUnreachable),
                new C4Error(C4NetworkErrorCode.Timeout),
                new C4Error(C4ErrorDomain.NetworkDomain, (int)C4NetworkErrorCode.ConnectionAborted),
                new C4Error(C4ErrorDomain.NetworkDomain, (int)C4NetworkErrorCode.ConnectionRefused),
                new C4Error(C4ErrorDomain.LiteCoreDomain, (int)C4ErrorCode.UnexpectedError) 
            };

            foreach (var pair in exceptions.Zip(errors, (a, b) => new { a, b })) {
                C4Error tmp;
                Status.ConvertNetworkError(pair.a, &tmp);
                tmp.ShouldBe(pair.b);
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

            dict.RecursiveEqual(dict).ShouldBeTrue();
            foreach (var num in new object[] { (sbyte) 42, (short) 42, 42L }) {
                42.RecursiveEqual(num).ShouldBeTrue();
            }

            foreach (var num in new object[] { (byte) 42, (ushort) 42, 42UL }) {
                42U.RecursiveEqual(num).ShouldBeTrue();
            }

            foreach (var num in new object[] { 3.14f, 3.14 }) {
                3.14m.RecursiveEqual(num).ShouldBeTrue();
            }
        }

#if !SANITY_ONLY
        [Fact]
        public async Task TestSerialQueue()
        {
            var queue = new SerialQueue();
            var now = DateTime.Now;
            var then = now;
            using var ignore = queue.DispatchAfter(TimeSpan.FromSeconds(1), () => then = DateTime.Now);
            await Task.Delay(250);
            then.ShouldBe(now);
            await Task.Delay(800);
            then.ShouldNotBe(now);

            var testBool = false;
            queue.DispatchSync<bool>(() => Volatile.Read(ref testBool)).ShouldBeFalse();

            var t = queue.DispatchAfter<bool>(TimeSpan.FromMilliseconds(500), () => Volatile.Read(ref testBool));
            Volatile.Write(ref testBool, true);
            (await t).ShouldBeTrue();
        }
#endif

        [Fact]
        public void TestAutoconvertJson()
        {
            var jVal = new JValue("test");
            var jArray = new JArray(jVal);
            var jObj = new JObject { ["test"] = jVal };

            DataOps.ToCouchbaseObject(jVal).ShouldBe("test");
            (DataOps.ToCouchbaseObject(jArray) as MutableArrayObject)?[0].String.ShouldBe("test");
            (DataOps.ToCouchbaseObject(jObj) as MutableDictionaryObject)?["test"].String.ShouldBe("test");

            var jsonString = "{\"level1\":{\"foo\":\"bar\"},\"level2\":{\"list\":[1, 3.14, \"s\"]}, \"$type\":\"JSON .NET Object\"}";
            var json = JsonConvert.DeserializeObject<IDictionary<string, object>>(jsonString);
            var converted = DataOps.ToCouchbaseObject(json) as MutableDictionaryObject;
            converted.ShouldNotBeNull();

            converted!["level1"]["foo"].String.ShouldBe("bar");
            converted["level2"]["list"][0].Int.ShouldBe(1);
            converted["level2"]["list"][1].Double.ShouldBe(3.14);
            converted["level2"]["list"][2].String.ShouldBe("s");
            converted["$type"].String.ShouldBe("JSON .NET Object");
        }

        [Fact]
        public void TestCreateExceptions()
        {
            var fleeceException = CouchbaseException.Create(new C4Error(FLError.EncodeError)) as CouchbaseFleeceException;
            fleeceException.ShouldNotBeNull();
            fleeceException!.Error.ShouldBe((int) FLError.EncodeError);
            fleeceException.Domain.ShouldBe(CouchbaseLiteErrorType.Fleece);

            var sqliteException =
                CouchbaseException.Create(new C4Error(C4ErrorDomain.SQLiteDomain, (int) SQLiteStatus.Misuse)) as CouchbaseSQLiteException;
            sqliteException.ShouldNotBeNull();
            sqliteException!.BaseError.ShouldBe(SQLiteStatus.Misuse);
            sqliteException.Error.ShouldBe((int) SQLiteStatus.Misuse);
            sqliteException.Domain.ShouldBe(CouchbaseLiteErrorType.SQLite);

            var webSocketException = CouchbaseException.Create(new C4Error(C4ErrorDomain.WebSocketDomain, 1003)) as CouchbaseWebsocketException;
            webSocketException!.Error.ShouldBe(CouchbaseLiteError.WebSocketDataError);
            webSocketException.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);

            var networkException =
                CouchbaseException.Create(new C4Error(C4NetworkErrorCode.InvalidURL)) as CouchbaseNetworkException;
            networkException.ShouldNotBeNull();
            networkException!.Error.ShouldBe(CouchbaseLiteError.InvalidUrl);
            networkException.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact]
        [ForIssue("couchbase-lite-net/1048")]
        public void TestDictionaryWithULong()
        {
            using (var doc = new MutableDocument("test_ulong")) {
                var dict = new MutableDictionaryObject();
                dict.SetValue("high_value", UInt64.MaxValue);
                doc.SetDictionary("nested", dict);
                DefaultCollection.Save(doc);
            }

            using (var doc = DefaultCollection.GetDocument("test_ulong")) {
                doc.ShouldNotBeNull("because it was just saved into the database");
                doc!["nested"]["high_value"].Value.ShouldBe(UInt64.MaxValue);
            }
        }

#if __ANDROID__ || __IOS__ || WINUI

        [Fact]
        public async Task TestMainThreadScheduler()
        {
            var scheduler = Service.GetInstance<IMainThreadTaskScheduler>();
            if(scheduler == null) {
                return;
            }

            var onMainThread = await Task.Factory.StartNew(() => scheduler.IsMainThread);
            onMainThread.ShouldBeFalse();

            var t = new Task<bool>(() => scheduler.IsMainThread);
            t.Start(scheduler.AsTaskScheduler());
            onMainThread = await t;
            onMainThread.ShouldBeTrue();
        }

#endif

        [Fact]
        public void TestCBDebugItemsMustNotBeNull()
        {
            List<object?> list = ["couchbase", null, "debug"];
            Should.Throw<ArgumentNullException>(() =>
            CBDebug.ItemsMustNotBeNull(
                WriteLog.To.Query, 
                nameof(CSharpTest), 
                nameof(TestCBDebugItemsMustNotBeNull), 
                list), "because the item in enumeration cannot be null.");

            list.RemoveAt(1);
            var items = CBDebug.ItemsMustNotBeNull(
                WriteLog.To.Query,
                nameof(CSharpTest),
                nameof(TestCBDebugItemsMustNotBeNull),
                list);

            items.Count().ShouldBe(2);
            items.ElementAt(1).ShouldBe("debug");
        }

#if !SANITY_ONLY && DEBUG
        [Fact]
        public unsafe void TestC4QueryWrapper()
        {
            int iteration = 1;

            void TestC4QueryWrapperInternal(C4QueryWrapper.ThreadSafetyLevel safetyLevel, bool blocking)
            {
                // This is a bit awkward to test, but my assertion is as follows.  If
                // I block inside of a callback to UseSafe then that keeps the relevant
                // thread safety locked, so then if I lock on it subsequently the logic
                // inside the follow up should not run until the former is done blocking.
                // DatabaseThreadSafety is not meant to be used externally like this, but
                // it's public so that I can pass it to other objects that need it in the
                // NativeSafe API.  I just take advantage of the fact that it is public.
                using var lockEvent = new AutoResetEvent(false);
                using var queryWrapper = new C4QueryWrapper(null, new ThreadSafety());
                var sw = Stopwatch.StartNew();
                Task.Run(() =>
                {
                    queryWrapper.UseSafe(q =>
                    {
                        lockEvent.Set();
                        Thread.Sleep(WrapperThreadBlockTime);
                    }, safetyLevel);
                });

                ThreadSafety threadSafety;
                if(blocking) {
                    // Choose the same lock as the wrapper is going to use
                    threadSafety = safetyLevel == C4QueryWrapper.ThreadSafetyLevel.Query 
                        ? queryWrapper.InstanceSafety 
                        : queryWrapper.DatabaseThreadSafety;
                } else {
                    // Choose the opposite lock from the wrapper
                    threadSafety = safetyLevel == C4QueryWrapper.ThreadSafetyLevel.Query
                        ? queryWrapper.DatabaseThreadSafety
                        : queryWrapper.InstanceSafety;
                }

                lockEvent.WaitOne(TimeSpan.FromMinutes(1)).ShouldBeTrue($"because otherwise UseSafe was not entered {iteration}");
                using (var threadSafetyScope = threadSafety.BeginLockedScope()) {
                    sw.Stop();
                }

                if (blocking) {
                    sw.Elapsed.ShouldBeGreaterThanOrEqualTo(WrapperThreadBlockTime, $"because otherwise the lock didn't block ({iteration})");
                } else {
                    sw.Elapsed.ShouldBeLessThan(WrapperThreadBlockTime, $"because otherwise the lock blocked ({iteration})");
                }

                iteration++;
            }

            TestC4QueryWrapperInternal(C4QueryWrapper.ThreadSafetyLevel.Query, true);
            TestC4QueryWrapperInternal(C4QueryWrapper.ThreadSafetyLevel.Database, true);
            TestC4QueryWrapperInternal(C4QueryWrapper.ThreadSafetyLevel.Query, false);
            TestC4QueryWrapperInternal(C4QueryWrapper.ThreadSafetyLevel.Database, false);
        }

        [Fact]
        public unsafe void TestC4DocumentWrapper()
        {
            int iteration = 1;

            void TestC4DocumentWrapperInternal(C4DocumentWrapper.ThreadSafetyLevel safetyLevel, bool blocking)
            {
                using var lockEvent = new AutoResetEvent(false);
                using var documentWrapper = new C4DocumentWrapper(null, new ThreadSafety());
                var sw = Stopwatch.StartNew();
                Task.Run(() =>
                {
                    documentWrapper.UseSafe(q =>
                    {
                        lockEvent.Set();
                        Thread.Sleep(WrapperThreadBlockTime);
                        return true;
                    }, safetyLevel);
                });

                ThreadSafety threadSafety;
                if (blocking) {
                    // Choose the same lock as the wrapper is going to use
                    threadSafety = safetyLevel == C4DocumentWrapper.ThreadSafetyLevel.Document
                        ? documentWrapper.InstanceSafety
                        : documentWrapper.DatabaseThreadSafety;
                } else {
                    // Choose the opposite lock from the wrapper
                    threadSafety = safetyLevel == C4DocumentWrapper.ThreadSafetyLevel.Document
                        ? documentWrapper.DatabaseThreadSafety
                        : documentWrapper.InstanceSafety;
                }

                lockEvent.WaitOne(TimeSpan.FromMinutes(1)).ShouldBeTrue($"because otherwise UseSafe was not entered {iteration}");
                using (var threadSafetyScope = threadSafety.BeginLockedScope()) {
                    sw.Stop();
                }

                if (blocking) {
                    sw.Elapsed.ShouldBeGreaterThanOrEqualTo(WrapperThreadBlockTime, $"because otherwise the lock didn't block ({iteration})");
                } else {
                    sw.Elapsed.ShouldBeLessThan(WrapperThreadBlockTime, $"because otherwise the lock blocked ({iteration})");
                }

                iteration++;
            }

            TestC4DocumentWrapperInternal(C4DocumentWrapper.ThreadSafetyLevel.Document, true);
            TestC4DocumentWrapperInternal(C4DocumentWrapper.ThreadSafetyLevel.Database, true);
            TestC4DocumentWrapperInternal(C4DocumentWrapper.ThreadSafetyLevel.Document, false);
            TestC4DocumentWrapperInternal(C4DocumentWrapper.ThreadSafetyLevel.Database, false);
        }

        [Fact]
        public unsafe void TestC4IndexWrapper()
        {
            int iteration = 1;

            void TestC4IndexUpdaterWrapperInternal(C4IndexUpdaterWrapper.ThreadSafetyLevel safetyLevel, bool blocking)
            {
                using var lockEvent = new AutoResetEvent(false);
                using var indexWrapper = new C4IndexUpdaterWrapper(null, new ThreadSafety());
                var sw = Stopwatch.StartNew();
                Task.Run(() =>
                {
                    indexWrapper.UseSafe(q =>
                    {
                        lockEvent.Set();
                        Thread.Sleep(WrapperThreadBlockTime);
                        return true;
                    }, safetyLevel);
                });

                ThreadSafety threadSafety;
                if (blocking) {
                    // Choose the same lock as the wrapper is going to use
                    threadSafety = safetyLevel == C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater
                        ? indexWrapper.InstanceSafety
                        : indexWrapper.DatabaseThreadSafety;
                } else {
                    // Choose the opposite lock from the wrapper
                    threadSafety = safetyLevel == C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater
                        ? indexWrapper.DatabaseThreadSafety
                        : indexWrapper.InstanceSafety;
                }

                lockEvent.WaitOne(TimeSpan.FromMinutes(1)).ShouldBeTrue($"because otherwise UseSafe was not entered {iteration}");
                using (var threadSafetyScope = threadSafety.BeginLockedScope()) {
                    sw.Stop();
                }

                if (blocking) {
                    sw.Elapsed.ShouldBeGreaterThanOrEqualTo(WrapperThreadBlockTime, $"because otherwise the lock didn't block ({iteration})");
                } else {
                    sw.Elapsed.ShouldBeLessThan(WrapperThreadBlockTime, $"because otherwise the lock blocked ({iteration})");
                }

                iteration++;
            }

            TestC4IndexUpdaterWrapperInternal(C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater, true);
            TestC4IndexUpdaterWrapperInternal(C4IndexUpdaterWrapper.ThreadSafetyLevel.Database, true);
            TestC4IndexUpdaterWrapperInternal(C4IndexUpdaterWrapper.ThreadSafetyLevel.Updater, false);
            TestC4IndexUpdaterWrapperInternal(C4IndexUpdaterWrapper.ThreadSafetyLevel.Database, false);
        }
#endif

        private unsafe void TestRoundTrip<T>(T item)
        {
            using (var encoded = item.FLEncode()) {
                var flValue = NativeRaw.FLValue_FromData((FLSlice) encoded, FLTrust.Trusted);
                ((IntPtr) flValue).ShouldNotBe(IntPtr.Zero);
                if (item is IEnumerable enumerable && item is not string) {
                    ((IEnumerable) FLSliceExtensions.ToObject(flValue)!).ShouldBeEquivalentToFluent(enumerable);
                } else {
                    Extensions.CastOrDefault<T>(FLSliceExtensions.ToObject(flValue)).ShouldBe(item);
                }
            }
        }
    }
}
