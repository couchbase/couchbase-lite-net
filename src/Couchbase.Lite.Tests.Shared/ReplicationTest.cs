﻿//
//  ReplicationTest.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using Couchbase.Lite.Logging;

using Couchbase.Lite.Sync;
using Couchbase.Lite.Util;
using Couchbase.Lite.Query;
using Constants = Couchbase.Lite.Info.Constants;

using Shouldly;
using LiteCore;
using LiteCore.Interop;

using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Reflection;

using Test.Util;
#if COUCHBASE_ENTERPRISE
using Couchbase.Lite.P2P;
using ProtocolType = Couchbase.Lite.P2P.ProtocolType;
#endif

using Xunit;
using Xunit.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace Test
{
    public abstract class ReplicatorTestBase : TestCase
    {
        private const string OtherDbName = "otherdb";

        private static int Counter;

        protected Replicator? _repl;
        protected WaitAssert? _waitAssert;
        protected TimeSpan _timeout;

        public Database OtherDb { get; internal set; }

        public Collection OtherDefaultCollection => OtherDb.GetDefaultCollection();

        public bool DisableDefaultServerCertPinning { get; set; }

        public X509Certificate2 DefaultServerCert
        {
            get {
                #if NET6_0_WINDOWS10 || NET_ANDROID || NET_APPLE
                using (var cert =  FileSystem.OpenAppPackageFileAsync("SelfSigned.cer").Result)
                #else
                using (var cert = typeof(ReplicatorTestBase).GetTypeInfo().Assembly.GetManifestResourceStream("SelfSigned.cer"))
                #endif
                using (var ms = new MemoryStream()) {
                    cert!.CopyTo(ms);
                    return new X509Certificate2(ms.ToArray());
                }
            }
        }

        protected ReplicatorTestBase(ITestOutputHelper output) : base(output)
        {
            ReopenDB();
            ReopenOtherDb();
            _timeout = TimeSpan.FromSeconds(15);

            //uncomment the code below when you need to see more detail log
            //Database.Log.Console.Level = LogLevel.Debug;
        }

        [MemberNotNull("OtherDb")]
        protected void OpenOtherDb()
        {
            var nextCounter = Interlocked.Increment(ref Counter);
            var nextDbName = $"{OtherDbName}{nextCounter}";
            Database.Delete(nextDbName, Directory);
            OtherDb = OpenDB(nextDbName);
        }

        [MemberNotNull("OtherDb")]
        protected void ReopenOtherDb()
        {
            OtherDb?.Close();
            OpenOtherDb();
        }

        protected ReplicatorConfiguration CreateConfig(IEndpoint target, ReplicatorType type, bool continuous,
            Authenticator? authenticator = null, X509Certificate2? serverCert = null, Database? sourceDb = null)
        {
            var c = new ReplicatorConfiguration(target)
            {
                ReplicatorType = type,
                Continuous = continuous,
                Authenticator = authenticator
            };
            c.AddCollection(sourceDb?.GetDefaultCollection() ?? DefaultCollection);

            if ((target as URLEndpoint)?.Url?.Scheme == "wss") {
                if (serverCert != null) {
                    c.PinnedServerCertificate = serverCert;
                } else if (!DisableDefaultServerCertPinning) {
                    c.PinnedServerCertificate = DefaultServerCert;
                }
            }

            if (continuous) {
                c.CheckpointInterval = TimeSpan.FromSeconds(1);
            }

            return c;
        }

        #if COUCHBASE_ENTERPRISE
        protected ReplicatorConfiguration CreateConfig(IEndpoint target, ReplicatorType type, bool continuous,
            bool acceptOnlySelfSignedServerCertificate, Authenticator? authenticator = null,
            X509Certificate2? serverCert = null)
        {
            var c = CreateConfig(target, type, continuous, authenticator, serverCert);
            c.AcceptOnlySelfSignedServerCertificate = acceptOnlySelfSignedServerCertificate;
            return c;
        }
#endif

        // Can't convince the compiler that safeswap will set _repl to non null
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
        [MemberNotNull(nameof(_repl))]
        protected void RunReplication(ReplicatorConfiguration config, int expectedErrCode, CouchbaseLiteErrorType expectedErrDomain, bool reset = false,
            Action<Replicator>? onReplicatorReady = null)
        {
            Misc.SafeSwap(ref _repl, new Replicator(config));
            onReplicatorReady?.Invoke(_repl!);

            RunReplication(_repl!, expectedErrCode, expectedErrDomain, reset);
        }
#pragma warning restore CS8774 // Member must have a non-null value when exiting.

        protected void RunReplication(IEndpoint target, ReplicatorType type, bool continuous,
            Authenticator? authenticator, X509Certificate2? serverCert, int expectedErrCode,
            CouchbaseLiteErrorType expectedErrorDomain)
        {
            var config = CreateConfig(target, type, continuous, authenticator, serverCert);
            RunReplication(config, expectedErrCode, expectedErrorDomain);
        }

        #if COUCHBASE_ENTERPRISE
        protected void RunReplication(IEndpoint target, ReplicatorType type, bool continuous,
            Authenticator? authenticator, bool acceptOnlySelfSignedServerCertificate,
            X509Certificate2? serverCert, int expectedErrCode, CouchbaseLiteErrorType expectedErrorDomain)
        {
            var config = CreateConfig(target, type, continuous, acceptOnlySelfSignedServerCertificate, authenticator,
                serverCert);
            RunReplication(config, expectedErrCode, expectedErrorDomain);
        }
        #endif

        protected void RunReplication(Replicator replicator, int expectedErrCode,
            CouchbaseLiteErrorType expectedErrDomain, bool reset = false)
        {
            _waitAssert = new WaitAssert();
            var token = replicator.AddChangeListener((sender, args) => {
                _waitAssert.RunConditionalAssert(() => {
                    VerifyChange(args, expectedErrCode, expectedErrDomain);
                    if (replicator.Config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator?)sender)!.Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            replicator.Start(reset);
            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(20));
            } catch {
                replicator.Stop();
                throw;
            } finally {
                token.Remove();
            }
        }

        protected void VerifyChange(ReplicatorStatusChangedEventArgs change, int errorCode, CouchbaseLiteErrorType domain)
        {
            var s = change.Status;
            WriteLine($"---Status: {s.Activity} ({s.Progress.Completed} / {s.Progress.Total}), lastError = {s.Error}");
            if (s.Activity == ReplicatorActivityLevel.Stopped) {
                if (errorCode != 0) {
                    s.Error.ShouldNotBeNull();
                    s.Error.ShouldBeAssignableTo<CouchbaseException>();
                    var error = (s.Error as CouchbaseException);
                    error?.Error.ShouldBe(errorCode);
                    if (domain != 0) {
                        error?.Domain.ShouldBe(domain);
                    }
                } else {
                    s.Error.ShouldBeNull("because otherwise an unexpected error occurred");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            _waitAssert?.Dispose();
            Exception? ex = null;
            var name = OtherDb.Name;
            OtherDb.Close();

            base.Dispose(disposing);
            
            _repl = null;

            var success = Try.Condition(() => {
                try {
                    if (!string.IsNullOrEmpty(name))
                        Database.Delete(name, Directory);
                } catch (Exception e) {
                    ex = e;
                    return false;
                }

                return true;
            }).Times(5).Delay(TimeSpan.FromSeconds(1)).WriteProgress(WriteLine).Go();

            if (!success) {
                throw ex!;
            }
        }
    }

    public sealed class ReplicatorTest : ReplicatorTestBase
    {
        private bool _isFilteredCallback;
        private List<DocumentReplicationEventArgs> _replicationEvents = new List<DocumentReplicationEventArgs>();

        public ReplicatorTest(ITestOutputHelper output) : base(output)
        {
        }

#if COUCHBASE_ENTERPRISE

        [Fact]
        public void TestReplicatorHeartbeatGetSet()
        {
            Action? badAction = null;
            var config = CreateConfig(true, false, false);
            using (var repl = new Replicator(config)) {
                badAction = (() => repl.Config.Heartbeat = TimeSpan.FromSeconds(2));
                Should.Throw < InvalidOperationException>(badAction, "Cannot modify a ReplicatorConfiguration (Heartbeat) that is in use.");

                repl.Config.Heartbeat.ShouldBe(Constants.DefaultReplicatorHeartbeat, $"Because default Heartbeat Interval is 300 sec and {Constants.DefaultReplicatorHeartbeat} is returned.");
            }

            config.Heartbeat = TimeSpan.FromSeconds(60);
            using (var repl = new Replicator(config)) {
                repl.Config.Heartbeat.ShouldBe(TimeSpan.FromSeconds(60));
            }

            config.Heartbeat = null;
            using (var repl = new Replicator(config)) {
                repl.Config.Heartbeat.ShouldBe(null, "Because default Heartbeat Interval is 300 sec and null is returned.");
            }

            badAction = (() => config.Heartbeat = TimeSpan.FromSeconds(0));
            Should.Throw<ArgumentException>(badAction, "Assigning Heartbeat to an invalid value (<= 0).");

            badAction = (() => config.Heartbeat = TimeSpan.FromMilliseconds(800));
            Should.Throw<ArgumentException>(badAction, "Assigning Heartbeat to an invalid value.");

            using (var repl = new Replicator(config)) {
                repl.Config.Heartbeat.ShouldBe(null, "Because default Heartbeat Interval is 300 sec and null is returned.");
            }
        }

        [Fact]
        public void TestReplicatorMaxRetryWaitTimeGetSet()
        {
            Action? badAction = null;
            var config = CreateConfig(true, false, false);
            using (var repl = new Replicator(config)) {
                badAction = (() => repl.Config.MaxAttemptsWaitTime = TimeSpan.FromSeconds(2));
                Should.Throw<InvalidOperationException>(badAction, "Cannot modify a ReplicatorConfiguration (MaxAttemptsWaitTime) that is in use.");

                repl.Config.MaxAttemptsWaitTime.ShouldBe(Constants.DefaultReplicatorMaxAttemptsWaitTime, "Because default Max Retry Wait Time is 300 sec and null is returned.");
            }

            config.MaxAttemptsWaitTime = TimeSpan.FromSeconds(60);
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttemptsWaitTime.ShouldBe(TimeSpan.FromSeconds(60));
            }

            config.MaxAttemptsWaitTime = null;
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttemptsWaitTime.ShouldBe(null, "Because default Max Retry Wait Time is 300 sec and null is returned.");
            }

            badAction = (() => config.MaxAttemptsWaitTime = TimeSpan.FromSeconds(0));
            Should.Throw<ArgumentException>(badAction, "Assigning Max Retry Wait Time to an invalid value (<= 0).");

            badAction = (() => config.MaxAttemptsWaitTime = TimeSpan.FromMilliseconds(800));
            Should.Throw<ArgumentException>(badAction, "Assigning Max Retry Wait Time to an invalid value");

            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttemptsWaitTime.ShouldBe(null, "Because default Max Retry Wait Time is 300 sec and null is returned.");
            }
        }

        [Fact]
        public void TestReplicatorMaxAttemptsGetSet()
        {
            Action? badAction = null;
            var config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb));
            config.AddCollection(DefaultCollection);
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttempts.ShouldBe(Constants.DefaultReplicatorMaxAttemptsSingleShot, $"Because default Max Attempts is 10 times for a Single Shot Replicator and {Constants.DefaultReplicatorMaxAttemptsSingleShot} is returned.");

                badAction = (() => repl.Config.MaxAttempts = 2);
                Should.Throw<InvalidOperationException>(badAction, "Cannot modify a ReplicatorConfiguration (MaxAttempts) that is in use.");
            }

            config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb)) { Continuous = true };
            config.AddCollection(DefaultCollection);
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttempts.ShouldBe(Constants.DefaultReplicatorMaxAttemptsContinuous, $"Because default Max Attempts is Max int times for a Continuous Replicator and {Constants.DefaultReplicatorMaxAttemptsContinuous} is returned.");
            }
            
            var attempts = 5;
            config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb)) { MaxAttempts = attempts };
            config.AddCollection(DefaultCollection);
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttempts.ShouldBe(attempts, $"Because {attempts} is the value set for MaxAttempts.");
            }

            config.MaxAttempts = 0;
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttempts.ShouldBe(Constants.DefaultReplicatorMaxAttemptsSingleShot, $"Because default Max Attempts is 10 times for a Single Shot Replicator and {Constants.DefaultReplicatorMaxAttemptsSingleShot} is returned.");
            }

            config = new ReplicatorConfiguration(new DatabaseEndpoint(OtherDb)) { MaxAttempts = attempts, Continuous = true };
            config.AddCollection(DefaultCollection);
            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttempts.ShouldBe(attempts, $"Because {attempts} is the value set for MaxAttempts.");
            }

            badAction = (() => config.MaxAttempts = -1);
            Should.Throw<ArgumentException>(badAction, "Assigning Max Retries to an invalid value (< 0).");

            using (var repl = new Replicator(config)) {
                repl.Config.MaxAttempts.ShouldBe(attempts, $"Because {attempts}  is the value set for MaxAttempts.");
            }
        }

#if !SANITY_ONLY
        [Fact]
        public void TestReplicatorMaxAttempts() => ReplicatorMaxAttempts(3);

        [Fact]
        public void TestReplicatorOneMaxAttempts() => ReplicatorMaxAttempts(1);
#endif

        [Fact]
        public void TestReadOnlyConfiguration()
        {
            var config = CreateConfig(true, false, false);
            using (var repl = new Replicator(config)) {
                config = repl.Config;
                Should.Throw<InvalidOperationException>(() => config.ReplicatorType = ReplicatorType.PushAndPull, 
                    "because the configuration from a replicator should be read only");
            }
        }

        [Fact]
        public void TestEmptyPush()
        {
            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
        }

        [Fact]
        public void TestPushDocWithFilterOneShot() => TestPushDocWithFilter(false);

        [Fact]
        public void TestPushDocWithFilterContinuous() => TestPushDocWithFilter(true);

        [Fact]
        public void TestPushPullKeepsFilter()
        {
            var config = CreateConfig(true, true, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PushFilter = _replicator__filterCallback,
                PullFilter = _replicator__filterCallback
            });

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "donotpass");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "donotpass");
                OtherDefaultCollection.Save(doc2);
            }

            for (int i = 0; i < 2; i++) {
                RunReplication(config, 0, 0);
                DefaultCollection.Count.ShouldBe(1UL, "because the pull should have rejected the other document");
                OtherDefaultCollection.Count.ShouldBe(1UL, "because the push should have rejected the local document");
            }
        }

        [Fact]
        public void TestPushDeletedDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("pass")) {
                doc1.SetString("name", "pass");
                DefaultCollection.Save(doc1);

                doc2.SetString("name", "pass");
                DefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PushFilter = _replicator__filterCallback
            });

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue("Because the callback should be triggered when there are docs to be pushed to the other db.");
            OtherDefaultCollection.GetDocument("doc1").ShouldNotBeNull("because doc1 passes the filter");
            OtherDefaultCollection.GetDocument("pass").ShouldNotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = DefaultCollection.GetDocument("doc1"))
            using (var doc2 = DefaultCollection.GetDocument("pass")) {
                doc1.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc2.ShouldNotBeNull("because otherwise the database was missing 'pass'");
                DefaultCollection.Delete(doc1!);
                DefaultCollection.Delete(doc2!);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue("Because the callback should be triggered when the docs are deleted.");
            OtherDefaultCollection.GetDocument("doc1").ShouldNotBeNull("because doc1's deletion should be rejected");
            OtherDefaultCollection.GetDocument("pass").ShouldBeNull("because the next document's deletion is not rejected");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestRevisionIdInPushPullFilters()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetInt("One", 1);
                DefaultCollection.Save(doc1);
                doc2.SetInt("Two", 2);
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            var exceptions = new List<Exception>();
            Func<Document, DocumentFlags, bool> pullFilter = (doc, isPush) => {
                try {
                    doc.GetInt("Two").ShouldBe(2);
                    doc.RevisionID.ShouldNotBeNull();
                    Action act = () => doc.ToMutable();
                    Should.Throw<InvalidOperationException>(act).Message
                        .ShouldBe(CouchbaseLiteErrorMessage.NoDocEditInReplicationFilter);
                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };

            Func<Document, DocumentFlags, bool> pushFilter = (doc, isPush) => {
                try {
                    doc.GetInt("One").ShouldBe(1);
                    doc.RevisionID.ShouldNotBeNull();
                    Action act = () => doc.ToMutable();
                    Should.Throw<InvalidOperationException>(act).Message
                        .ShouldBe(CouchbaseLiteErrorMessage.NoDocEditInReplicationFilter);

                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PushFilter = pushFilter,
                PullFilter = pullFilter
            });

            RunReplication(config, 0, 0);
            exceptions.Count.ShouldBe(0);
        }

        [Fact]
        public void TestBlobAccessInFilter()
        {
            var content1 = new byte[] { 1, 2, 3 };
            var content2 = new byte[] { 4, 5, 6 };
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                var mutableDictionary = new MutableDictionaryObject();
                mutableDictionary.SetBlob("inner_blob", new Blob("text/plaintext", content1));
                doc1.SetDictionary("outer_dict", mutableDictionary);

                var mutableArray = new MutableArrayObject();
                mutableArray.AddBlob(new Blob("text/plaintext", content2));
                doc2.SetArray("outer_arr", mutableArray);
                DefaultCollection.Save(doc1);
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            var exceptions = new List<Exception>();
            Func<Document, DocumentFlags, bool> pullFilter = (doc, isPush) => {
                try {
                    var nestedBlob = doc.GetArray("outer_arr")?.GetBlob(0);
                    nestedBlob.ShouldNotBeNull("because the actual blob object should be intact");
                    var gotContent = nestedBlob!.Content;
                    gotContent.ShouldBeNull("because the blob is not yet available");
                    doc.RevisionID.ShouldNotBeNull();
                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };

            Func<Document, DocumentFlags, bool> pushFilter = (doc, isPush) => {
                try {
                    var gotContent = doc.GetDictionary("outer_dict")?.GetBlob("inner_blob")?.Content;
                    gotContent.ShouldNotBeNull("because the nested blob should be intact in the push");
                    gotContent.ShouldBeEquivalentToFluent(content1, "because the nested blob should be intact in the push");
                    doc.RevisionID.ShouldNotBeNull();
                } catch (Exception e) {
                    exceptions.Add(e);
                }

                return true;
            };
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PushFilter = pushFilter,
                PullFilter = pullFilter
            });

            RunReplication(config, 0, 0);
            exceptions.ShouldBeEmpty("because there should be no errors");
        }

        [Fact]
        public void TestPushDoc()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
                DefaultCollection.Count.ShouldBe(1UL);

                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeFalse();

            OtherDefaultCollection.Count.ShouldBe(2UL);
            using (var savedDoc1 = OtherDefaultCollection.GetDocument("doc1")) {
                savedDoc1.ShouldNotBeNull("because otherwise 'doc1' was missing");
                savedDoc1!.GetString("name").ShouldBe("Tiger");
            }
        }

        [Fact]
        public void TestPushDocContinuous()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
                DefaultCollection.Count.ShouldBe(1UL);

                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, false, true);
            config.CheckpointInterval = TimeSpan.FromSeconds(1);
            RunReplication(config, 0, 0);

            OtherDefaultCollection.Count.ShouldBe(2UL);
            using (var savedDoc1 = OtherDefaultCollection.GetDocument("doc1")) {
                savedDoc1.ShouldNotBeNull("because otherwise 'doc1' was missing");
                savedDoc1!.GetString("name").ShouldBe("Tiger");
            }
        }

        [Fact]
        public void TestPullDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "donotpass");
                OtherDefaultCollection.Save(doc1);

                doc2.SetString("name", "pass");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PullFilter = _replicator__filterCallback
            });

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue();
            DefaultCollection.GetDocument("doc1").ShouldBeNull("because doc1 is filtered out in the callback");
            DefaultCollection.GetDocument("doc2").ShouldNotBeNull("because doc2 is filtered in in the callback");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestPullDeletedDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("pass")) {
                doc1.SetString("name", "pass");
                OtherDefaultCollection.Save(doc1);

                doc2.SetString("name", "pass");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PullFilter = _replicator__filterCallback
            });

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue("Because the callback should be triggered when there are docs to be pulled from the other db.");
            DefaultCollection.GetDocument("doc1").ShouldNotBeNull("because doc1 passes the filter");
            DefaultCollection.GetDocument("pass").ShouldNotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = OtherDefaultCollection.GetDocument("doc1"))
            using (var doc2 = OtherDefaultCollection.GetDocument("pass")) {
                doc1.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc2.ShouldNotBeNull("because otherwise the database was missing 'pass'");
                OtherDefaultCollection.Delete(doc1!);
                OtherDefaultCollection.Delete(doc2!);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue("Because the callback should be triggered when the docs on the other db are deleted.");
            DefaultCollection.GetDocument("doc1").ShouldNotBeNull("because doc1's deletion should be rejected");
            DefaultCollection.GetDocument("pass").ShouldBeNull("because the next document's deletion is not rejected");
            _isFilteredCallback = false;
        }

        [Fact]
        public void TestPullRemovedDocWithFilter()
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("pass")) {
                doc1.SetString("name", "pass");
                OtherDefaultCollection.Save(doc1);

                doc2.SetString("name", "pass");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PullFilter = _replicator__filterCallback
            });

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue("Because the callback should be triggered when there are docs to be pulled from the other db.");
            DefaultCollection.GetDocument("doc1").ShouldNotBeNull("because doc1 passes the filter");
            DefaultCollection.GetDocument("pass").ShouldNotBeNull("because the next document passes the filter");
            _isFilteredCallback = false;

            using (var doc1 = OtherDefaultCollection.GetDocument("doc1"))
            using (var doc2 = OtherDefaultCollection.GetDocument("pass"))
            using (var doc1Mutable = doc1?.ToMutable())
            using (var doc2Mutable = doc2?.ToMutable()) {
                doc1Mutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc2Mutable.ShouldNotBeNull("because otherwise the database was missing 'pass'");
                doc1Mutable!.SetData(new Dictionary<string, object?> { ["_removed"] = true });
                doc2Mutable!.SetData(new Dictionary<string, object?> { ["_removed"] = true });
                OtherDefaultCollection.Save(doc1Mutable);
                OtherDefaultCollection.Save(doc2Mutable);
            }

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue("Because the callback should be triggered when the docs on the other db are removed.");
            DefaultCollection.GetDocument("doc1").ShouldNotBeNull("because doc1's removal should be rejected");
            DefaultCollection.GetDocument("pass").ShouldBeNull("because the next document's removal is not rejected");
            _isFilteredCallback = false;
        }

        [ForIssue("couchbase-lite-core/156")]
        [Fact]
        public void TestPullDoc()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
                DefaultCollection.Count.ShouldBe(1UL, "because only one document was saved so far");
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeFalse();

            DefaultCollection.Count.ShouldBe(2UL, "because the replicator should have pulled doc2 from the other DB");
            using (var doc2 = DefaultCollection.GetDocument("doc2")) {
                doc2.ShouldNotBeNull("because otherwise the database was missing 'doc2'");
                doc2!.GetString("name").ShouldBe("Cat");
            }
        }

        [ForIssue("couchbase-lite-core/156")]
        [Fact] //android
        public void TestPullDocContinuous()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
                DefaultCollection.Count.ShouldBe(1UL, "because only one document was saved so far");
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, true);
            config.CheckpointInterval = TimeSpan.FromSeconds(1);
            RunReplication(config, 0, 0);

            DefaultCollection.Count.ShouldBe(2UL, "because the replicator should have pulled doc2 from the other DB");
            using (var doc2 = DefaultCollection.GetDocument("doc2")) {
                doc2.ShouldNotBeNull("because otherwise the database was missing 'doc2'");
                doc2!.GetString("name").ShouldBe("Cat");
            }
        }

        [Fact]
        public void TestDocIDFilter()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("species", "Tiger");
            DefaultCollection.Save(doc1);
            doc1.SetString("name", "Hobbes");
            DefaultCollection.Save(doc1);

            var doc2 = new MutableDocument("doc2");
            doc2.SetString("species", "Tiger");
            DefaultCollection.Save(doc2);
            doc2.SetString("pattern", "striped");
            DefaultCollection.Save(doc2);

            var doc3 = new MutableDocument("doc3");
            doc3.SetString("species", "Tiger");
            OtherDefaultCollection.Save(doc3);
            doc3.SetString("name", "Hobbes");
            OtherDefaultCollection.Save(doc3);

            var doc4 = new MutableDocument("doc4");
            doc4.SetString("species", "Tiger");
            OtherDefaultCollection.Save(doc4);
            doc4.SetString("pattern", "striped");
            OtherDefaultCollection.Save(doc4);

            var config = CreateConfig(true, true, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                DocumentIDs =  new[] { "doc1", "doc3" }
            });

            RunReplication(config, 0, 0);
            DefaultCollection.Count.ShouldBe(3UL, "because only one document should have been pulled");
            DefaultCollection.GetDocument("doc3").ShouldNotBeNull();
            OtherDefaultCollection.Count.ShouldBe(3UL, "because only one document should have been pushed");
            OtherDefaultCollection.GetDocument("doc1").ShouldNotBeNull();
        }

#if !SANITY_ONLY
        [Fact]
        public async Task TestReplicatorStopWhenClosed()
        {
            var config = CreateConfig(true, true, true);
            using (var repl = new Replicator(config)) {
                repl.Start();
                while (repl.Status.Activity != ReplicatorActivityLevel.Idle) {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for idle...");
                    await Task.Delay(500);
                }

                repl.Stop();
                while (repl.Status.Activity != ReplicatorActivityLevel.Stopped) {
                    WriteLine($"Replication status still {repl.Status.Activity}, waiting for stopped...");
                    await Task.Delay(500);
                }
            }
        }

        [Fact] //This test is now flaky..
        public void TestStopContinuousReplicator()
        {
            var config = CreateConfig(true, false, true);
            using (var r = new Replicator(config)) {
                var stopWhen = new[]
                {
                    ReplicatorActivityLevel.Connecting, ReplicatorActivityLevel.Busy,
                    ReplicatorActivityLevel.Idle, ReplicatorActivityLevel.Idle
                };

                foreach (var when in stopWhen) {
                    var stopped = 0;
                    var waitAssert = new WaitAssert();
                    var token = r.AddChangeListener((sender, args) => {
                        waitAssert.RunConditionalAssert(() => {
                            VerifyChange(args, 0, 0);

                            // On Windows, at least, sometimes the connection is so fast that Connecting never gets called
                            if ((args.Status.Activity == when ||
                                (when == ReplicatorActivityLevel.Connecting && args.Status.Activity > when))
                                && Interlocked.Exchange(ref stopped, 1) == 0) {
                                WriteLine("***** Stop Replicator *****");
                                ((Replicator?)sender)!.Stop();
                            }

                            if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                                WriteLine("Stopped!");
                            }

                            return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                        });
                    });

                    WriteLine("***** Start Replicator *****");
                    r.Start();
                    try {
                        waitAssert.WaitForResult(TimeSpan.FromSeconds(5));
                    } finally {
                        token.Remove();
                    }

                    Task.Delay(500).Wait(); // increase delay time to prevent intermittent failures due to replicator ref might not completely dererf yet atm
                }
            }
        }
#endif

        [Fact]
        public void TestDocumentEndedEvent()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, true, true);//push n pull

            Misc.SafeSwap(ref _repl, new Replicator(config));
            _waitAssert = new WaitAssert();
            var token1 = _repl!.AddDocumentReplicationListener(DocumentEndedUpdate);
            var token = _repl.AddChangeListener((sender, args) => {
                _waitAssert.RunConditionalAssert(() => {
                    VerifyChange(args, 0, 0);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator?)sender)!.Stop();
                    }
                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            _repl.Start();
            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(30));
            } catch {
                _repl.Stop();
                throw;
            } finally {
                token.Remove();
                token1.Remove();
            }

            _replicationEvents.Count.ShouldBe(2);
            var push = _replicationEvents.FirstOrDefault(g => g.IsPush);
            push?.Documents.First().Id.ShouldBe("doc1");
            var pull = _replicationEvents.FirstOrDefault(g => !g.IsPush);
            pull?.Documents.First().Id.ShouldBe("doc2");
        }

        [Fact]
        public void TestDocumentErrorEvent()
        {
            // NOTE: Only push, need to think of a case that can force an error
            // for pull
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                OtherDefaultCollection.Save(doc1);
            }


            // Force a conflict
            using (var doc1a = DefaultCollection.GetDocument("doc1"))
            using (var doc1aMutable = doc1a?.ToMutable()) {
                doc1aMutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1aMutable!.SetString("name", "Liger");
                DefaultCollection.Save(doc1aMutable);
            }

            using (var doc1b = OtherDefaultCollection.GetDocument("doc1"))
            using (var doc1bMutable = doc1b?.ToMutable()) {
                doc1bMutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1bMutable!.SetString("name", "Lion");
                OtherDefaultCollection.Save(doc1bMutable);
            }

            var config = CreateConfig(true, false, false);
            using (var repl = new Replicator(config)) {
                using var wa = new WaitAssert();
                repl.AddDocumentReplicationListener((sender, args) => {
                    if (args.Documents[0].Id == "doc1") {
                        wa.RunAssert(() => {
                            args.Documents[0].Error.ShouldNotBeNull();
                            args.Documents[0].Error!.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite);
                            args.Documents[0].Error!.Error.ShouldBe((int)CouchbaseLiteError.HTTPConflict);
                        });
                    }
                });

                repl.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(10));
                repl.Stop();
                Try.Condition(() => repl.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().ShouldBeTrue();
            }
        }

        [Fact]
        public void TestDocumentDeletedEvent()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "test1");
                DefaultCollection.Save(doc1);
                DefaultCollection.Delete(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "test2");
                OtherDefaultCollection.Save(doc2);
                OtherDefaultCollection.Delete(doc2);
            }

            var config = CreateConfig(true, true, false);
            using var pullWait = new WaitAssert();
            using var pushWait = new WaitAssert();
            RunReplication(config, 0, 0, onReplicatorReady: (r) =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    pushWait.RunConditionalAssert(() =>
                        args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted)));
                    pullWait.RunConditionalAssert(() =>
                        !args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.Deleted)));
                });
            });

            pushWait.WaitForResult(TimeSpan.FromSeconds(5));
            pullWait.WaitForResult(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void TestChannelRemovedEvent()
        {
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "test2");
                OtherDefaultCollection.Save(doc2);
                doc2.SetData(new Dictionary<string, object?> { ["_removed"] = true });
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, true, false);
            using var pullWait = new WaitAssert();
            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    pullWait.RunConditionalAssert(() =>
                        !args.IsPush && args.Documents.Any(x => x.Flags.HasFlag(DocumentFlags.AccessRemoved)));
                });
            });

            pullWait.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestDocumentIDs()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("species", "Tiger");
                doc1.SetString("name", "Hobbes");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("species", "Tiger");
                doc2.SetString("pattern", "striped");
                DefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                DocumentIDs = new[] { "doc1" }
            });

            RunReplication(config, 0, 0);

            OtherDefaultCollection.Count.ShouldBe(1UL);
            using (var doc1 = OtherDefaultCollection.GetDocument("doc1")) {
                doc1.ShouldNotBeNull();
                doc1!.GetString("species").ShouldBe("Tiger");
                doc1.GetString("name").ShouldBe("Hobbes");
            }
        }

        [Fact]
        [ForIssue("couchbase-lite-core/447")]
        public void TestResetCheckpoint()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("species", "Tiger");
                doc1.SetString("name", "Hobbes");
                DefaultCollection.Save(doc1);
            }

            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("species", "Tiger");
                doc2.SetString("pattern", "striped");
                DefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0);
            config = CreateConfig(false, true, false);
            RunReplication(config, 0, 0);

            OtherDefaultCollection.Count.ShouldBe(2UL);
            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                DefaultCollection.Purge(doc!);
            }

            DefaultCollection.Purge("doc2");

            DefaultCollection.Count.ShouldBe(0UL, "because the documents were purged");
            RunReplication(config, 0, 0);

            DefaultCollection.Count.ShouldBe(0UL, "because the documents were purged and the replicator is already past them");
            RunReplication(config, 0, 0, true);

            DefaultCollection.Count.ShouldBe(2UL, "because the replicator was reset");
        }

        [Fact]
        public void TestPushAndForget()
        {
            for (int i = 0; i < 10; i++) {
                using (var mdoc = new MutableDocument()) {
                    mdoc.SetInt("id", i);
                    DefaultCollection.Save(mdoc);
                }
            }

            var config = CreateConfig(true, false, false);
            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    foreach (var docID in args.Documents.Select(x => x.Id)) {
                        DefaultCollection.Purge(docID);
                    }
                });
            });

            var success = Try.Condition(() => DefaultCollection.Count == 0).Times(5).Go();
            success.ShouldBeTrue("because push and forget should purge docs");
            OtherDefaultCollection.Count.ShouldBe(10UL, "because the documents should have been pushed");
        }

        [Fact]
        public void TestExpiredNotPushed()
        {
            const string docId = "byebye";
            using (var doc1 = new MutableDocument(docId)) {
                doc1.SetString("expire_me", "now");
                DefaultCollection.Save(doc1);
            }

            DefaultCollection.SetDocumentExpiration(docId, DateTimeOffset.Now);
            var config = CreateConfig(true, false, false);
            var callbackCount = 0;
            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((status, args) => { callbackCount++; });
            });
            OtherDefaultCollection.Count.ShouldBe(0UL);
            callbackCount.ShouldBe(0);
            _repl.Status.Progress.Total.ShouldBe(0UL);
        }

        //conflict resolving tests

        [Fact]
        public void TestConflictResolverBothRemoteLocalDelete()
        {
            int resolveCnt = 0;
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                OtherDefaultCollection.Save(doc1);
            }

            // Force a conflict
            using (var doc1a = DefaultCollection.GetDocument("doc1")?.ToMutable()) {
                doc1a.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1a!.SetString("name", "Cat");
                DefaultCollection.Save(doc1a);
            }

            DefaultCollection.Count.ShouldBe(1UL);

            OtherDefaultCollection.Delete(OtherDefaultCollection.GetDocument("doc1")!);

            var config = CreateConfig(false, true, false);
            var resolver = new TestConflictResolver((conflict) => {
                using (var doc1 = DefaultCollection.GetDocument("doc1")) {
                    doc1.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                    DefaultCollection.Delete(doc1!);
                }
                resolveCnt++;
                return conflict.LocalDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);
            resolveCnt.ShouldBe(1);
            DefaultCollection.Count.ShouldBe(0UL);
        }

        [Fact]
        public void TestConflictResolverPropertyInReplicationConfig()
        {
            var config = CreateConfig(false, true, false);

            var resolver = new TestConflictResolver((conflict) => {
                return conflict.RemoteDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            using (var replicator = new Replicator(config)) {

                Action badAction = (() => {
                    replicator.Config.GetCollectionConfig(DefaultCollection)!.ConflictResolver = new FakeConflictResolver();
                });
                Should.Throw<InvalidOperationException>(badAction, "Attempt to modify a frozen object is prohibited.");
            }
        }

        [Fact]
        public void TestConflictResolverRemoteWins()
        {
            var returnRemoteDoc = true;
            TestConflictResolverWins(returnRemoteDoc);
            TestConflictResolverWins(!returnRemoteDoc);
        }

        [Fact]
        public void TestConflictResolverMergeDoc()
        {
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Jim");
                DefaultCollection.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Jim");
                doc1.SetString("location", "Japan");
                OtherDefaultCollection.Save(doc1);
            }

            var config = CreateConfig(true, true, false);
            var resolver = new TestConflictResolver((conflict) => {
                var localDoc = conflict.LocalDocument;
                var remoteDoc = conflict.RemoteDocument;
                localDoc.ShouldNotBeNull();
                remoteDoc.ShouldNotBeNull();

                var updateDocDict = localDoc!.ToDictionary();
                var curDocDict = remoteDoc!.ToDictionary();

                foreach (var value in curDocDict) {
                    if (updateDocDict.ContainsKey(value.Key) && !value.Value!.Equals(updateDocDict[value.Key])) {
                        updateDocDict[value.Key] = value.Value + ", " + updateDocDict[value.Key];
                    } else if (!updateDocDict.ContainsKey(value.Key)) {
                        updateDocDict.Add(value.Key, value.Value);
                    }
                }

                WriteLine($"Resulting merge: {JsonConvert.SerializeObject(updateDocDict)}");

                var doc1 = new MutableDocument(conflict.DocumentID);
                doc1.SetData(updateDocDict);
                return doc1;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            using (var doc1a = DefaultCollection.GetDocument("doc1"))
            using (var doc1aMutable = doc1a?.ToMutable()) {
                doc1aMutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1aMutable!.SetString("name", "Jim");
                doc1aMutable.SetString("language", "English");
                DefaultCollection.Save(doc1aMutable);
            }

            using (var doc1a = OtherDefaultCollection.GetDocument("doc1"))
            using (var doc1aMutable = doc1a?.ToMutable()) {
                doc1aMutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1aMutable!.SetString("name", "Jim");
                doc1aMutable.SetString("language", "C#");
                OtherDefaultCollection.Save(doc1aMutable);
            }

            RunReplication(config, 0, 0);

            using (var doc1 = DefaultCollection.GetDocument("doc1")) {
                doc1.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1!.GetString("name").ShouldBe("Jim");
                var lanStr = doc1.GetString("language");
                lanStr?.Contains("English").ShouldBeTrue();
                lanStr?.Contains("C#").ShouldBeTrue();
                doc1.GetString("location").ShouldBe("Japan");
            }

            RunReplication(config, 0, 0);

            using (var doc1 = OtherDefaultCollection.GetDocument("doc1")) {
                doc1.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1!.GetString("name").ShouldBe("Jim");
                var lanStr = doc1.GetString("language");
                lanStr?.Contains("English").ShouldBeTrue();
                lanStr?.Contains("C#").ShouldBeTrue();
                doc1.GetString("location").ShouldBe("Japan");
            }
        }

        [Fact]
        public void TestConflictResolverNullDoc()
        {
            bool conflictResolved = false;
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            var resolver = new TestConflictResolver((conflict) => {
                conflictResolved = true;
                return null;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    conflictResolved.ShouldBe(true,
                        "Because the DocumentReplicationEvent be notified after the conflict has being resolved.");
                });
            });

            DefaultCollection.GetDocument("doc1").ShouldBeNull(); //Because conflict resolver returns null means return a deleted document.
        }

        [Fact]
        public void TestConflictResolverDeletedLocalWin()
        {
            Document? localDoc = null, remoteDoc = null;
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                OtherDefaultCollection.Save(doc1);
            }


            DefaultCollection.Delete(DefaultCollection.GetDocument("doc1")!);

            DefaultCollection.Count.ShouldBe(0UL);

            using (var doc1 = OtherDefaultCollection.GetDocument("doc1")?.ToMutable()) {
                doc1.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1!.SetString("name", "Lion");
                OtherDefaultCollection.Save(doc1);
            }

            var config = CreateConfig(false, true, false);
            var resolver= new TestConflictResolver((conflict) => {
                localDoc = conflict.LocalDocument;
                remoteDoc = conflict.RemoteDocument;
                return null;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            localDoc.ShouldBeNull();
            remoteDoc.ShouldNotBeNull();

            DefaultCollection.Count.ShouldBe(0UL);
        }

        [Fact]
        public void TestConflictResolverDeletedRemoteWin()
        {
            Document? localDoc = null, remoteDoc = null;
            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                DefaultCollection.Save(doc1);
            }

            using (var doc1 = new MutableDocument("doc1")) {
                doc1.SetString("name", "Tiger");
                OtherDefaultCollection.Save(doc1);
            }

            // Force a conflict
            using (var doc1a = DefaultCollection.GetDocument("doc1")?.ToMutable()) {
                doc1a.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc1a!.SetString("name", "Cat");
                DefaultCollection.Save(doc1a);
            }

            DefaultCollection.Count.ShouldBe(1UL);

            OtherDefaultCollection.Delete(OtherDefaultCollection.GetDocument("doc1")!);

            var config = CreateConfig(false, true, false);
            var resolver = new TestConflictResolver((conflict) => {
                localDoc = conflict.LocalDocument;
                remoteDoc = conflict.RemoteDocument;
                return null;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);
            remoteDoc.ShouldBeNull();
            localDoc.ShouldNotBeNull();
            DefaultCollection.Count.ShouldBe(0UL);
        }

        [Fact]
        public void TestConflictResolverWrongDocID()
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);
            var resolver = new TestConflictResolver((conflict) => {
                var doc = new MutableDocument("wrong_id");
                doc.SetString("wrong_id_key", "wrong_id_value");
                return doc;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            using (var db = DefaultCollection.GetDocument("doc1")) {
                db.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                db!.GetString("wrong_id_key").ShouldBe("wrong_id_value");
            }
        }

        [Fact]
        public void TestConflictResolverCalledTwice()
        {
            int resolveCnt = 0;
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            var resolver = new TestConflictResolver((conflict) => {
                if (resolveCnt == 0) {
                    using (var d = DefaultCollection.GetDocument("doc1"))
                    using (var doc = d?.ToMutable()) {
                        doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                        doc!.SetString("name", "Cougar");
                        DefaultCollection.Save(doc);
                    }
                }

                resolveCnt++;
                return conflict.LocalDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            using (var repl = new Replicator(config)){
                repl.Start();

                while (resolveCnt < 2){
                    Thread.Sleep(500); //wait for 2 conflict resolving action to complete
                }

                repl.Stop();
            }

            resolveCnt.ShouldBe(2);
            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc!.GetString("name").ShouldBe("Cougar");
            }
        }

        [Fact]
        public void TestNonBlockingDatabaseOperationConflictResolver()
        {
            int resolveCnt = 0;
            CreateReplicationConflict("doc1");
            var config = CreateConfig(false, true, false);
            var resolver = new TestConflictResolver((conflict) => {
                if (resolveCnt == 0) {
                    using (var d = DefaultCollection.GetDocument("doc1"))
                    using (var doc = d?.ToMutable()) {
                        doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                        d!.GetString("name").ShouldBe("Cat");
                        doc!.SetString("name", "Cougar");
                        DefaultCollection.Save(doc);
                        using (var docCheck = DefaultCollection.GetDocument("doc1")) {
                            docCheck.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                            docCheck!.GetString("name").ShouldBe("Cougar", "Because database save operation was not blocked");
                        }
                    }
                }

                resolveCnt++;
                return null;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            // This will be 0 if the test resolver threw an exception
            resolveCnt.ShouldNotBe(0, "because otherwise the conflict resolver didn't complete");

            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldBeNull();
            }
        }

        [Fact]
        public void TestNonBlockingConflictResolver()
        {
            CreateReplicationConflict("doc1");
            CreateReplicationConflict("doc2");
            var config = CreateConfig(false, true, false);
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            Queue<string> q = new Queue<string>();
            using var wa = new WaitAssert();
            var resolver = new TestConflictResolver((conflict) => {
                var cnt = 0;
                lock (q) {
                    q.Enqueue(conflict.LocalDocument!.Id);
                    cnt = q.Count;
                }

                if (cnt == 1) {
                    manualResetEvent.WaitOne();
                }

                q.Enqueue(conflict.LocalDocument.Id);
                wa.RunConditionalAssert(() => q.Count.Equals(4));

                if (cnt != 1) {
                    manualResetEvent.Set();
                }

                return conflict.RemoteDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            wa.WaitForResult(TimeSpan.FromMilliseconds(5000));

            // make sure, first doc starts resolution but finishes last.
            // in between second doc starts and finishes it.
            q.ElementAt(0).ShouldBe(q.ElementAt(3));
            q.ElementAt(1).ShouldBe(q.ElementAt(2));

            q.Clear();
        }

#if !SANITY_ONLY
        [Fact]
        public void TestDoubleConflictResolutionOnSameConflicts()
        {
            CreateReplicationConflict("doc1");

            var firstReplicatorStart = new ManualResetEventSlim();
            var secondReplicatorFinish = new ManualResetEventSlim();
            int resolveCnt = 0;

            var config = CreateConfig(false, true, false);
            var resolver = new TestConflictResolver((conflict) => {
                firstReplicatorStart.Set();
                secondReplicatorFinish.Wait();
                Thread.Sleep(500);
                resolveCnt++;
                return conflict.LocalDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            Replicator replicator = new Replicator(config);

            var config1 = CreateConfig(false, true, false);
            resolver = new TestConflictResolver((conflict) => {
                resolveCnt++;
                Task.Delay(500).ContinueWith(t => secondReplicatorFinish.Set()); // Set after return
                return conflict.RemoteDocument;
            });

            config1.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            Replicator replicator1 = new Replicator(config1);

            _waitAssert = new WaitAssert();
            var token = replicator.AddChangeListener((sender, args) => {
                _waitAssert.RunConditionalAssert(() => {
                    VerifyChange(args, 0, 0);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator?)sender)!.Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            var _waitAssert1 = new WaitAssert();
            var token1 = replicator1.AddChangeListener((sender, args) => {
                _waitAssert1.RunConditionalAssert(() => {
                    VerifyChange(args, 0, 0);
                    if (config.Continuous && args.Status.Activity == ReplicatorActivityLevel.Idle
                                          && args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator?)sender)!.Stop();
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            replicator.Start();
            firstReplicatorStart.Wait();
            replicator1.Start();

            try {
                _waitAssert.WaitForResult(TimeSpan.FromSeconds(10));
                _waitAssert1.WaitForResult(TimeSpan.FromSeconds(10));
            } catch {
                replicator1.Stop();
                replicator.Stop();
                throw;
            } finally {
                token.Remove();
                token1.Remove();
                replicator.Dispose();
                replicator1.Dispose();
            }

            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc!.GetBlob("blob")?.Content?.ShouldBeEquivalentToFluent(new byte[] { 7, 7, 7 });
            }
        }
#endif

        [Fact]
        public void TestConflictResolverExceptionWhenDocumentIsPurged()
        {
            int resolveCnt = 0;
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            var resolver = new TestConflictResolver((conflict) => {
                if (resolveCnt == 0) {
                    DefaultCollection.Purge(conflict.DocumentID);
                }
                resolveCnt++;
                return conflict.RemoteDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0, onReplicatorReady: r =>
            {
                r.AddDocumentReplicationListener((sender, args) =>
                {
                    if (!args.IsPush) {
                        args.Documents[0].Error.ShouldNotBeNull();
                        args.Documents[0].Error!.Error.ShouldBe((int) CouchbaseLiteError.NotFound);
                    }
                });
            });
        }

#if !SANITY_ONLY
        [Fact]
        public void TestConflictResolverExceptionsReturnDocFromOtherDBThrown()
        {
            var tmpDoc = new MutableDocument("doc1");
            using (var thirdDb = new Database("different_db")) {
                tmpDoc.SetString("foo", "bar");
                thirdDb.GetDefaultCollection().Save(tmpDoc);

                var differentDbResolver = new TestConflictResolver((conflict) => tmpDoc);

                TestConflictResolverExceptionThrown(differentDbResolver, true);
                DefaultCollection.GetDocument("doc1")?.GetString("name").ShouldBe("Human");

                thirdDb.Delete();
            }
        }
#endif

        [Fact]
        public void TestConflictResolverExceptionThrownInConflictResolver()
        {
            var resolverWithException = new TestConflictResolver((conflict) => {
                throw new Exception("Customer side exception");
            });

            TestConflictResolverExceptionThrown(resolverWithException, false);
        }

        [Fact]
        public void TestConflictResolverReturningBlob()
        {
            var returnRemoteDoc = true;
            TestConflictResolverWins(returnRemoteDoc);
            TestConflictResolverWins(!returnRemoteDoc);

            //return new doc with a blob object
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            var resolver = new TestConflictResolver((conflict) => {
                var winByteArray = new byte[] { 8, 8, 8 };

                var doc = new MutableDocument();
                doc.SetBlob("blob", new Blob("text/plaintext", winByteArray));
                return doc;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc!.GetBlob("blob")?.Content.ShouldBeEquivalentToFluent(new byte[] { 8, 8, 8 });
            }
        }

        [Fact]
        public void TestConflictResolverReturningBlobWithFlagChecking()
        {
            C4DocumentFlags flags = (C4DocumentFlags)0;
            unsafe 
            {
                using (var doc1 = new MutableDocument("doc1")) {
                    doc1.SetString("name", "Tiger");
                    DefaultCollection.Save(doc1);
                    doc1.c4Doc.ShouldNotBeNull("because otherwise there is a serious internal bug");
                    flags = doc1.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocExists).ShouldBeTrue();
                }

                using (var doc1 = new MutableDocument("doc1")) {
                    doc1.SetString("name", "Tiger");
                    OtherDefaultCollection.Save(doc1);
                    flags = doc1.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocExists).ShouldBeTrue();
                }

                // Force a conflict
                using (var doc1a = DefaultCollection.GetDocument("doc1"))
                using (var doc1aMutable = doc1a?.ToMutable()) {
                    doc1aMutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                    doc1aMutable!.SetString("name", "Cat");
                    DefaultCollection.Save(doc1aMutable);
                    flags = doc1aMutable.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocExists).ShouldBeTrue();
                }

                using (var doc1a = OtherDefaultCollection.GetDocument("doc1"))
                using (var doc1aMutable = doc1a?.ToMutable()) {
                    doc1aMutable.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                    doc1aMutable!.SetString("name", "Lion");
                    OtherDefaultCollection.Save(doc1aMutable);
                    flags = doc1aMutable.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocExists).ShouldBeTrue();
                }


                var config = CreateConfig(false, true, false);

                var resolver = new TestConflictResolver((conflict) =>
                {
                    var evilByteArray = new byte[] { 6, 6, 6 };
                    var dict = new MutableDictionaryObject();
                    dict.SetBlob("blob", new Blob("text/plaintext", evilByteArray));
                    var doc = new MutableDocument();
                    doc.SetValue("nestedBlob", dict);
                    flags = conflict.LocalDocument!.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocConflicted).ShouldBeTrue();

                    return doc;
                });

                config.AddCollection(DefaultCollection, new CollectionConfiguration
                {
                    ConflictResolver = resolver
                });

                RunReplication(config, 0, 0);

                using (var doc = DefaultCollection.GetDocument("doc1")) {
                    doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                    var dict = doc!.GetValue("nestedBlob");
                    ((DictionaryObject?)dict)!.GetBlob("blob")?.Content.ShouldBeEquivalentToFluent(new byte[] { 6, 6, 6 });
                    flags = doc.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
                }
            }
        }

#if !SANITY_ONLY
        [Fact]
        public void TestConflictResolverReturningBlobFromDifferentDB()
        {
            var blobFromOtherDbResolver = new TestConflictResolver((conflict) => {
                var md = conflict.LocalDocument!.ToMutable();
                using (var otherDbDoc = OtherDefaultCollection.GetDocument("doc1")) {
                    otherDbDoc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                    md.SetBlob("blob", otherDbDoc!.GetBlob("blob"));
                }

                return md;
            });

            TestConflictResolverExceptionThrown(blobFromOtherDbResolver, false, true);
        }
#endif

        //CBL-623: Revision flags get cleared while saving resolved document
        [Fact]
        [ForIssue("CBL-623")]
        public void TestConflictResolverPreservesFlags()
        {
            //force conflicts and check flags
            CreateReplicationConflict("doc1", true);

            var config = CreateConfig(false, true, false);
            C4DocumentFlags flags = (C4DocumentFlags)0;
            var resolver = new TestConflictResolver((conflict) => {
                unsafe {
                    flags = conflict.LocalDocument!.c4Doc!.RawDoc->flags;
                    flags.HasFlag(C4DocumentFlags.DocConflicted).ShouldBeTrue();
                    flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
                    return conflict.LocalDocument;
                }
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldNotBeNull("because otherwise the database was missing 'doc1'");
                doc!.GetBlob("blob")?.Content.ShouldBeEquivalentToFluent(new byte[] { 6, 6, 6 });
                unsafe {
                    flags = doc.c4Doc!.RawDoc->flags;
                }
            }

            flags.HasFlag(C4DocumentFlags.DocConflicted).ShouldBeFalse();
            flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
        }

        //end conflict resolveing tests

        [Fact]
        public void TestCloseWithActiveReplications() => WithActiveReplications(true);

        [Fact]
        public void TestDeleteWithActiveReplications() => WithActiveReplications(false);

#if !SANITY_ONLY
        [Fact]
        public void TestCloseWithActiveReplicationAndQuery() => WithActiveReplicationAndQuery(true);

        [Fact]
        public void TestDeleteWithActiveReplicationAndQuery() => WithActiveReplicationAndQuery(false);
#endif

        // Pending Doc Ids unit tests

        [Fact]
        public void TestPendingDocIDsPullOnlyException()
        {
            LoadDocs();
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            using (var replicator = new Replicator(config)) {
                using var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    wa.RunConditionalAssert(() =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Busy) {
                            Action badAct = () => ((Replicator?) sender)!.GetPendingDocumentIDs(DefaultCollection);
                            Should.Throw<CouchbaseLiteException>(badAct).Message.ShouldBe(CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs);
                        }

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                replicator.Start();

                try {
                    wa.WaitForResult(TimeSpan.FromSeconds(10));
                    replicator.Status.Activity.ShouldBe(ReplicatorActivityLevel.Stopped);
                } finally {
                    token.Remove();
                }
            }
        }

        [Fact]
        public void TestPendingDocIDsWithCreate() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.CREATE);

        [Fact]
        public void TestPendingDocIDsWithUpdate() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.UPDATE);

        [Fact]
        public void TestPendingDocIDsWithDelete() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.DELETE);

        [Fact]
        public void TestPendingDocIDsWithPurge() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.PURGE);

        [Fact]
        public void TestPendingDocIDsWithFilter() => ValidatePendingDocumentIds(PENDING_DOC_ID_SEL.FILTER);

#if !SANITY_ONLY
        [Fact]
        public void TestIsDocumentPendingPullOnlyException()
        {
            LoadDocs();
            using (var doc2 = new MutableDocument("doc2")) {
                doc2.SetString("name", "Cat");
                OtherDefaultCollection.Save(doc2);
            }

            var config = CreateConfig(false, true, false);
            using (var replicator = new Replicator(config)) {
                using var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    wa.RunConditionalAssert(() =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Busy) {
                            Action badAct = () => ((Replicator?) sender)!.IsDocumentPending("doc-001", DefaultCollection);
                            Should.Throw<CouchbaseLiteException>(badAct).Message.ShouldBe(CouchbaseLiteErrorMessage.PullOnlyPendingDocIDs);
                        }

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });

                });

                replicator.Start();

                try {
                    wa.WaitForResult(TimeSpan.FromSeconds(100));
                    replicator.Status.Activity.ShouldBe(ReplicatorActivityLevel.Stopped);
                } finally {
                    token.Remove();
                }

            }
        }
#endif

        [Fact]
        public void TestIsDocumentPendingWithCreate() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.CREATE);

        [Fact]
        public void TestIsDocumentPendingWithUpdate() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.UPDATE);

        [Fact]
        public void TestIsDocumentPendingWithDelete() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.DELETE);

        [Fact]
        public void TestIsDocumentPendingWithPurge() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.PURGE);

        [Fact]
        public void TestIsDocumentPendingWithFilter() => ValidateIsDocumentPending(PENDING_DOC_ID_SEL.FILTER);

        [Fact]
        public void TestGetPendingDocIdsWithCloseDb()
        {
            var config = CreateConfig(true, false, false);
            using (var replicator = new Replicator(config)) {
                Db.Close();
                Action badAct = () => replicator.GetPendingDocumentIDs(DefaultCollection);
                Should.Throw<InvalidOperationException>(badAct).Message.ShouldBe(CouchbaseLiteErrorMessage.DBClosed);
            }

            ReopenDB();
            using (var replicator = new Replicator(config)) {
                OtherDb.Close();
                Action badAct = () => replicator.GetPendingDocumentIDs(DefaultCollection);
                Should.Throw<InvalidOperationException>(badAct).Message.ShouldBe(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        [Fact]
        public void TestIsDocumentPendingWithCloseDb()
        {
            var config = CreateConfig(true, false, false);
            using (var replicator = new Replicator(config)) {
                Db.Close();
                Action badAct = () => replicator.IsDocumentPending("doc1", DefaultCollection);
                Should.Throw<InvalidOperationException>(badAct).Message.ShouldBe(CouchbaseLiteErrorMessage.DBClosed);
            }

            ReopenDB();
            using (var replicator = new Replicator(config)) {
                OtherDb.Close();
                Action badAct = () => replicator.IsDocumentPending("doc1", DefaultCollection);
                Should.Throw<InvalidOperationException>(badAct).Message.ShouldBe(CouchbaseLiteErrorMessage.DBClosed);
            }
        }

        [Fact]
        public void TestDisposeRunningReplicator()
        {
            var config = CreateConfig(true, false, true);
            var replicator = new Replicator(config);
            var stoppedWait = new ManualResetEventSlim();
            replicator.AddChangeListener((sender, args) =>
            {
                if(args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    stoppedWait.Set();
                }
            });
            replicator.Start();

            Thread.Sleep(500);
            replicator.Dispose();
            stoppedWait.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue("because otherwise the replicator didn't stop");
        }

        #if __IOS__ && !SANITY_ONLY
        [SkippableFact]
        public void TestSwitchBackgroundForeground()
        {
            Skip.If(ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.SIMULATOR, "Functionality not supported on simulator");

            var target = new DatabaseEndpoint(OtherDb);
            var config = new ReplicatorConfiguration(target)
            {
                Continuous = true
            };
            
            config.AddCollection(DefaultCollection);

            using var r = new Replicator(config);

            const int NUM_ROUNDS = 10;
            using var foregroundEvent = new AutoResetEvent(false);
            using var backgroundEvent = new AutoResetEvent(false);
            using var stoppedEvent = new ManualResetEventSlim(false);

            var foregroundCount = 0;
            var backgroundCount = 0;

            // ReSharper disable AccessToDisposedClosure
            var token = r.AddChangeListener((sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Idle) {
                    foregroundCount++;
                    foregroundEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                    backgroundCount++;
                    backgroundEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    stoppedEvent.Set();
                }
            });
            // ReSharper restore AccessToDisposedClosure
            
            r.Start();
            foregroundEvent.WaitOne(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never went idle");
            for (var i = 0; i < NUM_ROUNDS; i++) {
                r.AppBackgrounding(null, EventArgs.Empty);
                backgroundEvent.WaitOne(TimeSpan.FromSeconds(5)).Should()
                    .BeTrue("because otherwise the replicator never suspended ({count})", i);
                r.ConflictResolutionSuspended.Should()
                    .BeTrue("because otherwise conflict resolution was not suspended ({count})", i);
                
                r.AppForegrounding(null, EventArgs.Empty);
                foregroundEvent.WaitOne(TimeSpan.FromSeconds(5)).Should()
                    .BeTrue("because otherwise the replicator never unsuspended ({count})", i);
                r.ConflictResolutionSuspended.Should()
                    .BeFalse("because otherwise conflict resolution was not unsuspended ({count})", i);
            }
            
            r.Stop();
            stoppedEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator didn't stop");
            foregroundCount.ShouldBe(NUM_ROUNDS + 1,
                "because otherwise an incorrect number of foreground events happened");
            backgroundCount.Should()
                .Be(NUM_ROUNDS, "because otherwise an incorrect number of background events occurred");

            token.Remove();
        }

        [SkippableFact]
        public void TestSwitchToForegroundImmediately()
        {
            Skip.If(ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.SIMULATOR, "Functionality not supported on simulator");

            var target = new DatabaseEndpoint(OtherDb);
            var config = new ReplicatorConfiguration(target)
            {
                Continuous = true,
                AllowReplicatingInBackground = true
            };
            
            config.AddCollection(DefaultCollection);

            using var r = new Replicator(config);
            
            using var idleEvent = new AutoResetEvent(false);
            using var stoppedEvent = new ManualResetEventSlim(false);

            // ReSharper disable AccessToDisposedClosure
            var token = r.AddChangeListener((sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Idle) {
                    idleEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    stoppedEvent.Set();
                }
            });
            // ReSharper restore AccessToDisposedClosure
            
            r.Start();
            idleEvent.WaitOne(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never went idle");

            // Switch to background and immediately come back
            r.Suspended = true;
            r.Suspended = false;

            idleEvent.WaitOne(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator didn't go back to idle");
            
            r.Stop();
            stoppedEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never stopped");
            
            token.Remove();
        }

        [SkippableFact]
        public void TestBackgroundingWhenStopping()
        {
            Skip.If(ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.SIMULATOR, "Functionality not supported on simulator");

            var target = new DatabaseEndpoint(OtherDb);
            var config = new ReplicatorConfiguration(target)
            {
                Continuous = true
            };
            
            config.AddCollection(DefaultCollection);

            using var r = new Replicator(config);
            
            using var idleEvent = new ManualResetEventSlim(false);
            using var stoppedEvent = new ManualResetEventSlim(false);
            var foregrounding = false;
            var foregroundingAssert = new WaitAssert();
            
            // ReSharper disable AccessToDisposedClosure
            var token = r.AddChangeListener((sender, args) =>
            {
                // ReSharper disable AccessToModifiedClosure
                foregroundingAssert.RunAssert(() => foregrounding.ShouldBeFalse());
                // ReSharper restore AccessToModifiedClosure
                if (args.Status.Activity == ReplicatorActivityLevel.Idle) {
                    idleEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    stoppedEvent.Set();
                }
            });
            // ReSharper restore AccessToDisposedClosure
            
            r.Start();
            idleEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never went idle");

            r.Stop();
            
            // This shouldn't prevent the replicator stopping
            r.AppBackgrounding(null, EventArgs.Empty);
            stoppedEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never stopped");
            
            // This shouldn't wake up the replicator
            foregrounding = true;
            r.AppForegrounding(null, EventArgs.Empty);
            
            // Wait a bit to catch any straggling notifications that might contain
            // foregrounding == true
            Thread.Sleep(300);
            foregroundingAssert.WaitForResult(TimeSpan.FromMilliseconds(500));
            
            token.Remove();
        }

        [SkippableFact]
        public void TestBackgroundDuringDataTransfer()
        {
            Skip.If(ObjCRuntime.Runtime.Arch == ObjCRuntime.Arch.SIMULATOR, "Functionality not supported on simulator");

            var target = new DatabaseEndpoint(OtherDb);
            var config = new ReplicatorConfiguration(target)
            {
                Continuous = true,
                ReplicatorType = ReplicatorType.Push
            };

            config.AddCollection(DefaultCollection);

            using var r = new Replicator(config);

            using var idleEvent = new ManualResetEventSlim(false);
            using var busyEvent = new ManualResetEventSlim(false);
            using var offlineEvent = new ManualResetEventSlim(false);
            using var stoppedEvent = new ManualResetEventSlim(false);
            // ReSharper disable AccessToDisposedClosure
            var token = r.AddChangeListener((sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Idle) {
                    if (args.Status.Progress.Total == 0) {
                        idleEvent.Set();
                    } else if (args.Status.Progress.Completed == args.Status.Progress.Total) {
                        ((Replicator)sender)!.Stop();
                    }
                } else if (args.Status.Activity == ReplicatorActivityLevel.Busy) {
                    busyEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                    offlineEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    stoppedEvent.Set();
                }
            });
            // ReSharper restore AccessToDisposedClosure

            OtherDefaultCollection.Count.ShouldBe(0, "because nothing was replicated yet");
            r.Start();
            idleEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never went idle");

            using var doc1 = new MutableDocument("doc1");
            var blob = new Blob("image/jpeg", GetTestAsset("C/tests/data/for#354.jpg"));
            doc1.SetBlob("blob", blob);
            DefaultCollection.Save(doc1);
            busyEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator is stuck in idle");

            r.Suspended = true;
            offlineEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the suspension didn't work");

            Thread.Sleep(200);
            r.Suspended = false;

            stoppedEvent.Wait(TimeSpan.FromSeconds(10)).Should()
                .BeTrue("because otherwise the replicator never finished");
            token.Remove();

            OtherDefaultCollection.Count.ShouldBe(1, "because one document should have replicated");
            using var doc = OtherDefaultCollection.GetDocument("doc1");
            var blob2 = doc?.GetBlob("blob");
            blob2?.Digest.ShouldBe(blob.Digest, "because the blobs should match");
        }

        [Fact]
        public void TestSuspendConflictResolution()
        {
            const int NUM_DOCS = 1000;
            for (var i = 0; i < NUM_DOCS; i++) {
                var docID = $"doc-{i}";
                using var doc1a = new MutableDocument(docID);
                doc1a.SetString("name", Db.Name);
                DefaultCollection.Save(doc1a);
                
                using var doc1b = new MutableDocument(docID);
                doc1b.SetString("name", OtherDb.Name);
                OtherDefaultCollection.Save(doc1b);
            }

            uint resolvingCount = 0;

            var target = new DatabaseEndpoint(OtherDb);
            var config = new ReplicatorConfiguration(target)
            {
                Continuous = true,
                ReplicatorType = ReplicatorType.Pull
            };
            
            using var resolvingEvent = new ManualResetEventSlim(false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                // ReSharper disable AccessToDisposedClosure
                ConflictResolver = new BlockConflictResolver(conflict =>
                {
                    Interlocked.Increment(ref resolvingCount);
                    resolvingEvent.Set();
                    return conflict.RemoteDocument;
                })
                // ReSharper restore AccessToDisposedClosure
            });

            using var r = new Replicator(config);
            using var offlineEvent = new ManualResetEventSlim(false);
            using var stoppedEvent = new ManualResetEventSlim(false);
            
            // ReSharper disable AccessToDisposedClosure
            var token = r.AddChangeListener((sender, args) =>
            {
                if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                    offlineEvent.Set();
                } else if (args.Status.Activity == ReplicatorActivityLevel.Stopped) {
                    stoppedEvent.Set();
                }
            });
            // ReSharper restore AccessToDisposedClosure
            
            r.Start();

            resolvingEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise conflict resolution never started");
            r.Suspended = true;

            int retryCounter = 0;
            while (r.PendingConflictCount > 0) {
                retryCounter++.ShouldBeLessThan(20, "because otherwise the pending conflicts never got suspended");
                Thread.Sleep(500);
            }

            resolvingCount.ShouldBeLessThan(NUM_DOCS, "because the conflicts should have been suspended");
            offlineEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never suspended");
            r.Stop();

            stoppedEvent.Wait(TimeSpan.FromSeconds(5)).Should()
                .BeTrue("because otherwise the replicator never stopped");
            token.Remove();
        }
        
        #endif

        /*
         SG Config
        ===========
         {
            "disable_persistent_config": true,
            "logging": {
                "console": { "log_level": "info", "color_enabled": true, "log_keys": ["*"] }
            },
            "SSLCert": "./certs.pem",
            "SSLKey": "./certs.key",
            "databases": {
                "db": {
                    "server": "walrus:app",
                    "users": { "GUEST": { "disabled": false, "admin_channels": ["*"] } }
                }
            }
        }

        certs.pem
        ==========
        -----BEGIN CERTIFICATE-----
MIICoDCCAYgCCQDOqeOThcl0DTANBgkqhkiG9w0BAQsFADAQMQ4wDAYDVQQDDAVJ
bnRlcjAeFw0yMjA0MDgwNDE2MjNaFw0zMjA0MDUwNDE2MjNaMBQxEjAQBgNVBAMM
CWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMt7VQ0j
74/GJVnTfC0YQZHeCFoZbZyJ/4KPOpe1UoqRQ1xNtllPMHf4ukIeNd3tS4CHQDqK
83a7uGXEOzY3JFaVRnTpMcHRMnpmZQLWZs+WMCP5fzI4EcaJjFmqQSUjfZiocdh/
n5vKc64bhKyUStE2CSObMnJ/L5mPY1JUAgxQrXtK4lw1T/ppV2m4hiutr+gkhXjc
Sam4DheuMg7hSUZSwh7VI253ev1Hp4JdSmndQHvle99S+N5jJ11NZnEuQxcImmOI
MBVfRFpREFPOH+JrqsnYSic2GQvv31nAJsXzYX2t/VT0a3TUes3B9OZfAVA7nMFA
r3E9mjVGYVtn7skCAwEAATANBgkqhkiG9w0BAQsFAAOCAQEADbjYO9VxOGZT5LAv
ON+U+2FPG5Tons1ubWslThROqml7CCfNKPVhZCwe0BUQLWc35NYvqVjoSAenCHu6
EUANfqtuNxQAoeDCaP1epGYZ8fakJXvuyTjek3RV2PeiuFUIZQP/HWGfI640kh4V
xvUBa3joelnt+KjDB/yJemmf0dIXJ0dLtFBTN+YVp4aSFTtzcbqh50H6BSAgSiWR
ocTu5YpDXHZ6ufaMTRa2HUcSmFeWi75sS6ySgECTbeld1/mFZcSf1zXHU9WFg39D
knQNR2i1cJMbMZ3GCRyB6y3SxFb7/9BS70DV3p4n5BjYMlhNnHJx4u1JUTLWgybV
qrV+HA==
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIDFTCCAf2gAwIBAgIJANZ8gSANI5jNMA0GCSqGSIb3DQEBCwUAMA8xDTALBgNV
BAMMBFJvb3QwHhcNMjIwNDA4MDQxNjIzWhcNMzIwNDA1MDQxNjIzWjAQMQ4wDAYD
VQQDDAVJbnRlcjCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAOm1MUNQ
xZKOCXw93eB/pmyCk5kEV3+H8RQC5Nq7orHvnHL6D/YVfVsobZyHkMSP3FVzl0bo
s1s+8kCjJ7O+M3TpzuSL8y4uLSEPmZF5qY2N7QobabrKVYueFxFmOD7+ypILx2QC
+hWd3J3XiLiiXqOO2jtjtwwy2+pD21DjmcPHGC4GKyv8/jp7hH4MFF6ux1wRQej1
on5jJQNFERUFdfX3wAmZgjww8bfyCEkHxnyIfJjEhyOtMLGGNUu8Hms7az+uYT6I
S4Q6VeBJ5WTKyhk7aJB1Rl6zZbROvTIq+ZaxAJNwsIzd/HiaoTwFUe3EFilIeGFK
w3vnPwiq99tDBHsCAwEAAaNzMHEwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQU
WXW5x/ufCrRKhv3F5wBqY0JVUEswPwYDVR0jBDgwNoAUefIiQi9GC9aBspej7UJT
zQzs/mKhE6QRMA8xDTALBgNVBAMMBFJvb3SCCQD1tOzs5zPQ/zANBgkqhkiG9w0B
AQsFAAOCAQEAEJhO1fA0d8Hu/5IHTlsGfmtcXOyXDcQQVz/3FKWrTPgDOYeMMNbG
WqvuG4YxmXt/+2OC1IYK/slrIK5XXldfRu90UM4wVXeD3ATLS3AG0Z/+yPRGbUbF
y5+11nXySGyKdV1ik0KgLGeYf0cuJ/vu+/7mkj4mGDfmTQv+8/HYKNaOqgKuVRlf
LHBh/RlbHMBn2nwL79vbrIeDaQ0zq9srt9F3CEy+SvlxX63Txmrym3fqTQjPUi5s
rEsy+eNr4N+aDWqGRcUkbP/C/ktGGNBHYG1NaPJq7CV1tdLe+usIcRWRR9vOBWbr
EkBGJMvCdhlWRv2FnrQ+VUQ+mhYHBS2Kng==
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIDFDCCAfygAwIBAgIJAPW07OznM9D/MA0GCSqGSIb3DQEBCwUAMA8xDTALBgNV
BAMMBFJvb3QwHhcNMjIwNDA4MDQxNjIzWhcNMzIwNDA1MDQxNjIzWjAPMQ0wCwYD
VQQDDARSb290MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvJV+Ptou
R1BS/0XXN+JImdNesaBJ2tcHrFHq2yK9V4qu2iUX8LgOcBpPg8yR0zJlzjwF+SLE
R8jBhD79YF8kF+r7cqBhsvy+e/ri0AaBiGsdP7NFPFEUCOukhnMIvLt10BvsRoCd
+eFrDZO0ZJer3ylp2GeB01rTgngWfrenhZdyGR8ISn+ijtN+J2IhAxsoLGDWiAL/
XWX55agSuAGi6zlomkReTMuyfkidLfrejUQCnrcDQQ7xqjdCB1QYBt6o1U1oHN3F
D6ICXirXJyVDJ2Ry6q+FrGJbJDUPlNwlPqAyukFFbeOINPKWiFQUw8nSo3i3DFMG
UZ3HhkQ/xfboZQIDAQABo3MwcTAPBgNVHRMBAf8EBTADAQH/MB0GA1UdDgQWBBR5
8iJCL0YL1oGyl6PtQlPNDOz+YjA/BgNVHSMEODA2gBR58iJCL0YL1oGyl6PtQlPN
DOz+YqETpBEwDzENMAsGA1UEAwwEUm9vdIIJAPW07OznM9D/MA0GCSqGSIb3DQEB
CwUAA4IBAQANxGwoeEBaibMQAqSWPnDBISiwk9uKy3buateXOtLlBSpM9ohE4iPG
GDFZ+9LoKJGy4vWmv6XD4zBeoqZ9hOgnvdEu0P+JITffjXCsfb0JPsOOjwbcJ+5+
TnfoXCyPRTEi/6OG1sKO2ibav5vMTUuUDdVYbPA2hfEAdn/n0GrN4fQ1USMKk+Ld
KWgWGZto+l0fKIXdHHpxr01V9Q/+6kzbpZOSxw41m/o1TwJxYSuRXZfK67YpBYGO
N4X2c7Qsvjd52vcZdRra+bkS0BJXwEDZZdmrZOlRAYIhE7lZ5ojqcZ+/UJztyPZq
Dbr9kMLDVeMuJfGyebdZ0zeMhVSv0PlD
-----END CERTIFICATE-----

        certs.key
        ==========
        -----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEAy3tVDSPvj8YlWdN8LRhBkd4IWhltnIn/go86l7VSipFDXE22
WU8wd/i6Qh413e1LgIdAOorzdru4ZcQ7NjckVpVGdOkxwdEyemZlAtZmz5YwI/l/
MjgRxomMWapBJSN9mKhx2H+fm8pzrhuErJRK0TYJI5sycn8vmY9jUlQCDFCte0ri
XDVP+mlXabiGK62v6CSFeNxJqbgOF64yDuFJRlLCHtUjbnd6/Uengl1Kad1Ae+V7
31L43mMnXU1mcS5DFwiaY4gwFV9EWlEQU84f4muqydhKJzYZC+/fWcAmxfNhfa39
VPRrdNR6zcH05l8BUDucwUCvcT2aNUZhW2fuyQIDAQABAoIBABkmMSjinCVE9DDZ
9qsMKG6C5r1cDzQdyjq0wSUnAACoW474++Sl6POrBjpNyZKYVZCZJtMVgWnXYb8S
Nc9JmXAqGv8wIzo1ROvj4/Ap16MoiOKpX5MxYuEK9xHN/Sc977QCfa+odm2m5A1M
0WUTHvwklZSVBfMozRVJp5jxUR98UL89wy8b6acuhqCMPLwmNi0+JuRx4eCLa0eK
Hn0OKCuil2xR0iA+3mno4G9BoxCIX5p+ZPiBHJazFoqM/ld3hq2jc3/1e5OUeIvC
yJ4DEj/7bseXbYE5wQJq93VofxLUZJn7JsW5RqzSr4o/OWQjuihpklNCsKp1rg3E
6NfKitECgYEA7RrI7itQS6LJTLZchOJEBD4CLJu0yfh3SXeuT48Tps8m8lNYJRa2
AV0E207KZhB4o55ZFVrd5uZm0mxtb5IxAIYeyj2WL1y6eUazaWJT7DdyTX960B/P
FaZw2VJMtNfiZBcgI5+QwPUQdn5PPLQoMx+bScGL2EKjEau5lssj6VcCgYEA27KZ
5aALzcHwQtw6lvSeUGgh9q4aBmEP2WQISnE+nXp/R0vD1nSMS3VM9qyeUqUi3sQT
LkuhB5u1n7rIK1IZQeWsdqlkDFF71ExF56UhMazRM7quutyRzIZdt0H8Fu3UlEVQ
Qpv6OCY3re+pN8YXAF/KFSc+YzgC0DwWYTvWNN8CgYEAjjf4udNFMHkOIXNo/1Pw
1FKVX0huIo9kja654YLCmNW8WLHhNy3uMdcnqJwUDzBmDE5YxNRiMbOVjTX4Vmnr
9pJ8OXbDdNk8CK59wwJ1sER5zT5f3iKWRjg1jEUvOXIcm0i7wGJfoz3biBevU4wE
aNXWgWEUjr05rsnAYlCq07UCgYB1ehdY9i/Zom67EdAykDNng4dFxOsdpiE3eYoF
ZHC6/Jm6ogTiVAgBAYRaEwvF3Y+71tT62G4PB3AjLVmD8K6Y0htaiFF7VMcGSpLG
v0H0shhbtONiADfmSaMrLxmBmUMm1bmJJKa0B5uWYqT1sfLyvEXu8cEmhPjcKIU/
ESQFuQKBgQDP7fFUpqTbidPOLHa/bznIftj81mJp8zXt3Iv9g5pW2/QqYOk7v/DQ
7qx19fVGXW4S6GQEVyssj4KusiNpPItEfacY3NQaByBsOsVrUcOTbWH9NjP+6q17
3uz5P9XvLMiqYBXia52n3Nz7mFzXUeN/E/IyQtHMQxndKr9tatP17w==
-----END RSA PRIVATE KEY-----


         */

#if false
        //[Fact] //Manul test with SG 3.0 official release 
        public void TestVerifiedPinnedInvalidCertificate()
        {
            var pinnedCert = @"-----BEGIN CERTIFICATE-----
        MIICpDCCAYwCCQCskbhc/nbA5jANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAls
        b2NhbGhvc3QwHhcNMjIwNDA4MDEwNDE1WhcNMzIwNDA1MDEwNDE1WjAUMRIwEAYD
        VQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDQ
        vl0M5D7ZglW76p428x7iQoSkhNyRBEjZgSqvQW3jAIsIElWu7mVIIAm1tpZ5i5+Q
        CHnFLha1TDACb0MUa1knnGj/8EsdOADvBfdBq7AotypiqBayRUNdZmLoQEhDDsen
        pEHMDmBrDsWrgNG82OMFHmjK+x0RioYTOlvBbqMAX8Nqp6Yu/9N2vW7YBZ5ovsr7
        vdFJkSgUYXID9zw/MN4asBQPqMT6jMwlxR1bPqjsNgXrMOaFHT/2xXdfCvq2TBXu
        H7evR6F7ayNcMReeMPuLOSWxA6Fefp8L4yDMW23jizNIGN122BgJXTyLXFtvg7CQ
        tMnE7k07LLYg3LcIeamrAgMBAAEwDQYJKoZIhvcNAQELBQADggEBABdQVNSIWcDS
        sDPXk9ZMY3stY9wj7VZF7IO1V57n+JYV1tJsyU7HZPgSle5oGTSkB2Dj1oBuPqnd
        8XTS/b956hdrqmzxNii8sGcHvWWaZhHrh7Wqa5EceJrnyVM/Q4uoSbOJhLntLE+a
        FeFLQkPpJxdtjEUHSAB9K9zCO92UC/+mBUelHgztsTl+PvnRRGC+YdLy521ST8BI
        luKJ3JANncQ4pCTrobH/EuC46ola0fxF8G5LuP+kEpLAh2y2nuB+FWoUatN5FQxa
        +4F330aYRvDKDf8r+ve3DtchkUpV9Xa1kcDFyTcYGKBrINtjRmCIblA1fezw59ZT
        S5TnM2/TjtQ=
        -----END CERTIFICATE-----";

            var certPemString = pinnedCert
                    .Replace("-----BEGIN CERTIFICATE-----", null)
                    .Replace("-----END CERTIFICATE-----", null);
            var cert = new X509Certificate2(Convert.FromBase64String(certPemString));

            var targetEndpoint = new URLEndpoint(new Uri("wss://10.100.174.37:4984/db"));
            var config = new ReplicatorConfiguration(targetEndpoint)
            {
                Continuous = false,
                PinnedServerCertificate = cert
            };

            config.AddCollection(DefaultCollection);

            RunReplication(config, (int)CouchbaseLiteError.TLSCertUntrusted, CouchbaseLiteErrorType.CouchbaseLite);
        }

        [Fact] //Manul test with SG 3.0 official release 
        public void TestVerifiedPinnedValidCertificateInChain()
        {
            var chainCerts = new List<string>();

            var pinnedCertLeaf = @"-----BEGIN CERTIFICATE-----
        MIICoDCCAYgCCQDOqeOThcl0DTANBgkqhkiG9w0BAQsFADAQMQ4wDAYDVQQDDAVJ
        bnRlcjAeFw0yMjA0MDgwNDE2MjNaFw0zMjA0MDUwNDE2MjNaMBQxEjAQBgNVBAMM
        CWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMt7VQ0j
        74/GJVnTfC0YQZHeCFoZbZyJ/4KPOpe1UoqRQ1xNtllPMHf4ukIeNd3tS4CHQDqK
        83a7uGXEOzY3JFaVRnTpMcHRMnpmZQLWZs+WMCP5fzI4EcaJjFmqQSUjfZiocdh/
        n5vKc64bhKyUStE2CSObMnJ/L5mPY1JUAgxQrXtK4lw1T/ppV2m4hiutr+gkhXjc
        Sam4DheuMg7hSUZSwh7VI253ev1Hp4JdSmndQHvle99S+N5jJ11NZnEuQxcImmOI
        MBVfRFpREFPOH+JrqsnYSic2GQvv31nAJsXzYX2t/VT0a3TUes3B9OZfAVA7nMFA
        r3E9mjVGYVtn7skCAwEAATANBgkqhkiG9w0BAQsFAAOCAQEADbjYO9VxOGZT5LAv
        ON+U+2FPG5Tons1ubWslThROqml7CCfNKPVhZCwe0BUQLWc35NYvqVjoSAenCHu6
        EUANfqtuNxQAoeDCaP1epGYZ8fakJXvuyTjek3RV2PeiuFUIZQP/HWGfI640kh4V
        xvUBa3joelnt+KjDB/yJemmf0dIXJ0dLtFBTN+YVp4aSFTtzcbqh50H6BSAgSiWR
        ocTu5YpDXHZ6ufaMTRa2HUcSmFeWi75sS6ySgECTbeld1/mFZcSf1zXHU9WFg39D
        knQNR2i1cJMbMZ3GCRyB6y3SxFb7/9BS70DV3p4n5BjYMlhNnHJx4u1JUTLWgybV
        qrV+HA==                                                         
        -----END CERTIFICATE-----";

            chainCerts.Add(pinnedCertLeaf);

            var pinnedCertIntermediate = @"-----BEGIN CERTIFICATE-----
        MIIDFTCCAf2gAwIBAgIJANZ8gSANI5jNMA0GCSqGSIb3DQEBCwUAMA8xDTALBgNV
        BAMMBFJvb3QwHhcNMjIwNDA4MDQxNjIzWhcNMzIwNDA1MDQxNjIzWjAQMQ4wDAYD
        VQQDDAVJbnRlcjCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAOm1MUNQ
        xZKOCXw93eB/pmyCk5kEV3+H8RQC5Nq7orHvnHL6D/YVfVsobZyHkMSP3FVzl0bo
        s1s+8kCjJ7O+M3TpzuSL8y4uLSEPmZF5qY2N7QobabrKVYueFxFmOD7+ypILx2QC
        +hWd3J3XiLiiXqOO2jtjtwwy2+pD21DjmcPHGC4GKyv8/jp7hH4MFF6ux1wRQej1
        on5jJQNFERUFdfX3wAmZgjww8bfyCEkHxnyIfJjEhyOtMLGGNUu8Hms7az+uYT6I
        S4Q6VeBJ5WTKyhk7aJB1Rl6zZbROvTIq+ZaxAJNwsIzd/HiaoTwFUe3EFilIeGFK
        w3vnPwiq99tDBHsCAwEAAaNzMHEwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQU
        WXW5x/ufCrRKhv3F5wBqY0JVUEswPwYDVR0jBDgwNoAUefIiQi9GC9aBspej7UJT
        zQzs/mKhE6QRMA8xDTALBgNVBAMMBFJvb3SCCQD1tOzs5zPQ/zANBgkqhkiG9w0B
        AQsFAAOCAQEAEJhO1fA0d8Hu/5IHTlsGfmtcXOyXDcQQVz/3FKWrTPgDOYeMMNbG
        WqvuG4YxmXt/+2OC1IYK/slrIK5XXldfRu90UM4wVXeD3ATLS3AG0Z/+yPRGbUbF
        y5+11nXySGyKdV1ik0KgLGeYf0cuJ/vu+/7mkj4mGDfmTQv+8/HYKNaOqgKuVRlf
        LHBh/RlbHMBn2nwL79vbrIeDaQ0zq9srt9F3CEy+SvlxX63Txmrym3fqTQjPUi5s
        rEsy+eNr4N+aDWqGRcUkbP/C/ktGGNBHYG1NaPJq7CV1tdLe+usIcRWRR9vOBWbr
        EkBGJMvCdhlWRv2FnrQ+VUQ+mhYHBS2Kng==
        -----END CERTIFICATE-----";

            chainCerts.Add(pinnedCertIntermediate);

            var pinnedCertRoot = @"-----BEGIN CERTIFICATE-----
        MIIDFDCCAfygAwIBAgIJAPW07OznM9D/MA0GCSqGSIb3DQEBCwUAMA8xDTALBgNV
        BAMMBFJvb3QwHhcNMjIwNDA4MDQxNjIzWhcNMzIwNDA1MDQxNjIzWjAPMQ0wCwYD
        VQQDDARSb290MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvJV+Ptou
        R1BS/0XXN+JImdNesaBJ2tcHrFHq2yK9V4qu2iUX8LgOcBpPg8yR0zJlzjwF+SLE
        R8jBhD79YF8kF+r7cqBhsvy+e/ri0AaBiGsdP7NFPFEUCOukhnMIvLt10BvsRoCd
        +eFrDZO0ZJer3ylp2GeB01rTgngWfrenhZdyGR8ISn+ijtN+J2IhAxsoLGDWiAL/
        XWX55agSuAGi6zlomkReTMuyfkidLfrejUQCnrcDQQ7xqjdCB1QYBt6o1U1oHN3F
        D6ICXirXJyVDJ2Ry6q+FrGJbJDUPlNwlPqAyukFFbeOINPKWiFQUw8nSo3i3DFMG
        UZ3HhkQ/xfboZQIDAQABo3MwcTAPBgNVHRMBAf8EBTADAQH/MB0GA1UdDgQWBBR5
        8iJCL0YL1oGyl6PtQlPNDOz+YjA/BgNVHSMEODA2gBR58iJCL0YL1oGyl6PtQlPN
        DOz+YqETpBEwDzENMAsGA1UEAwwEUm9vdIIJAPW07OznM9D/MA0GCSqGSIb3DQEB
        CwUAA4IBAQANxGwoeEBaibMQAqSWPnDBISiwk9uKy3buateXOtLlBSpM9ohE4iPG
        GDFZ+9LoKJGy4vWmv6XD4zBeoqZ9hOgnvdEu0P+JITffjXCsfb0JPsOOjwbcJ+5+
        TnfoXCyPRTEi/6OG1sKO2ibav5vMTUuUDdVYbPA2hfEAdn/n0GrN4fQ1USMKk+Ld
        KWgWGZto+l0fKIXdHHpxr01V9Q/+6kzbpZOSxw41m/o1TwJxYSuRXZfK67YpBYGO
        N4X2c7Qsvjd52vcZdRra+bkS0BJXwEDZZdmrZOlRAYIhE7lZ5ojqcZ+/UJztyPZq
        Dbr9kMLDVeMuJfGyebdZ0zeMhVSv0PlD
        -----END CERTIFICATE-----";

            chainCerts.Add(pinnedCertRoot);

            foreach (var certStr in chainCerts)
            {
                var certPemString = certStr
                    .Replace("-----BEGIN CERTIFICATE-----", null)
                    .Replace("-----END CERTIFICATE-----", null);
                var cert = new X509Certificate2(Convert.FromBase64String(certPemString));
                
                var targetEndpoint = new URLEndpoint(new Uri("wss://10.100.174.37:4984/db"));
                var config = new ReplicatorConfiguration(Db, targetEndpoint)
                {
                    Continuous = false,
                    PinnedServerCertificate = cert
                };
                
                RunReplication(config, 0, 0);
            }
        }
#endif

#endif

        enum PENDING_DOC_ID_SEL { CREATE = 0, UPDATE, DELETE, PURGE, FILTER }

        private HashSet<string> LoadDocs()
        {
            var result = new HashSet<string>();
            var n = 0ul;
            while (n < 50) {
                var docID = $"doc-{++n:D3}";
                using (var doc = new MutableDocument(docID)) {
                    result.Add(docID);
                    doc.SetString(docID, docID);
                    DefaultCollection.Save(doc);
                }
            }

            return result;
        }


#if COUCHBASE_ENTERPRISE
        private void ReplicatorMaxAttempts(int attempts)
        {
            // If this IP address happens to exist, then change it.  It needs to be an address that does not
            // exist on the LAN
            var targetEndpoint = new URLEndpoint(new Uri("ws://192.168.0.11:4984/app"));
            var config = new ReplicatorConfiguration(targetEndpoint) {
                MaxAttempts = attempts, 
                Continuous = true,
            };
            config.AddCollection(DefaultCollection);

            var count = 0;
            var repl = new Replicator(config);
            using var waitAssert = new WaitAssert();
            var token = repl.AddChangeListener((sender, args) =>
            {
                waitAssert.RunConditionalAssert(() =>
                {
                    if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                        count++;
                    }

                    return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                });
            });

            repl.Start();

            // Wait for the replicator to be stopped
            waitAssert.WaitForResult(TimeSpan.FromSeconds(60));
            token.Remove();

            count.ShouldBe(attempts - 1);
            repl.Dispose();
        }

        private void ValidatePendingDocumentIds(PENDING_DOC_ID_SEL selection)
        {
            IImmutableSet<string> pendingDocIds;
            var idsSet = LoadDocs();
            var config = CreateConfig(true, false, false);
            var DocIdForTest = "doc-001";
            if (selection == PENDING_DOC_ID_SEL.UPDATE) {
                using (var d = DefaultCollection.GetDocument(DocIdForTest))
                using (var md = d?.ToMutable()) {
                    md.ShouldNotBeNull("because otherwise the database is missing 'doc-001'");
                    md!.SetString("addString", "This is a new string.");
                    DefaultCollection.Save(md);
                }
            } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                using (var d = DefaultCollection.GetDocument(DocIdForTest)) {
                    d.ShouldNotBeNull($"because otherwise the database is missing '{DocIdForTest}'");
                    DefaultCollection.Delete(d!);
                }
            } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                using (var d = DefaultCollection.GetDocument(DocIdForTest)) {
                    d.ShouldNotBeNull($"because otherwise the database is missing '{DocIdForTest}'");
                    DefaultCollection.Purge(d!);
                    idsSet.Remove(DocIdForTest);
                }
            } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                Func<Document, DocumentFlags, bool> pushFilter = (doc, isPush) =>
                {
                    if (doc.Id.Equals(DocIdForTest))
                        return true;
                    return false;
                };

                config.AddCollection(DefaultCollection, new CollectionConfiguration
                {
                    PushFilter = pushFilter
                });
            }

            using (var replicator = new Replicator(config)) {
                using var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    wa.RunConditionalAssert(() =>
                    {
                        if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                            pendingDocIds = replicator.GetPendingDocumentIDs(DefaultCollection);
                            pendingDocIds.Count.ShouldBe(0);
                        }

                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                pendingDocIds = replicator.GetPendingDocumentIDs(DefaultCollection);
                if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    pendingDocIds.Count.ShouldBe(1);
                    pendingDocIds.ElementAt(0).ShouldBe(DocIdForTest);
                } else {
                    idsSet.ToImmutableSortedSet<string>().ShouldBeSubsetOf(pendingDocIds);
                    idsSet.Count.ShouldBe(pendingDocIds.Count);
                }

                replicator.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(50));

                Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().ShouldBeTrue();

                replicator.GetPendingDocumentIDs(DefaultCollection).Count.ShouldBe(0);
                token.Remove();
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void ValidateIsDocumentPending(PENDING_DOC_ID_SEL selection)
        {
            bool docIdIsPending;
            var DocIdForTest = "doc-001";
            var idsSet = LoadDocs();
            var config = CreateConfig(true, false, false);
            if (selection == PENDING_DOC_ID_SEL.UPDATE) {
                using (var d = DefaultCollection.GetDocument(DocIdForTest))
                using (var md = d?.ToMutable()) {
                    md.ShouldNotBeNull("because otherwise the database is missing 'doc-001'");
                    md!.SetString("addString", "This is a new string.");
                    DefaultCollection.Save(md);
                }
            } else if (selection == PENDING_DOC_ID_SEL.DELETE) {
                using (var d = DefaultCollection.GetDocument(DocIdForTest)) {
                    d.ShouldNotBeNull($"because otherwise the database is missing '{DocIdForTest}'");
                    DefaultCollection.Delete(d!);
                }
            } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                using (var d = DefaultCollection.GetDocument(DocIdForTest)) {
                    d.ShouldNotBeNull($"because otherwise the database is missing '{DocIdForTest}'");
                    DefaultCollection.Purge(d!);
                }
            } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                Func<Document, DocumentFlags, bool> pushFilter = (doc, isPush) =>
                {
                    if (doc.Id.Equals(DocIdForTest))
                        return true;
                    return false;
                };

                config.AddCollection(DefaultCollection, new CollectionConfiguration
                {
                    PushFilter = pushFilter
                });
            }

            using (var replicator = new Replicator(config)) {
                using var wa = new WaitAssert();
                var token = replicator.AddChangeListener((sender, args) =>
                {
                    if (args.Status.Activity == ReplicatorActivityLevel.Offline) {
                        docIdIsPending = replicator.IsDocumentPending(DocIdForTest, DefaultCollection);
                        docIdIsPending.ShouldBeFalse();
                    }

                    wa.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                docIdIsPending = replicator.IsDocumentPending(DocIdForTest, DefaultCollection);
                if (selection == PENDING_DOC_ID_SEL.CREATE || selection == PENDING_DOC_ID_SEL.UPDATE
                    || selection == PENDING_DOC_ID_SEL.DELETE) {
                    docIdIsPending.ShouldBeTrue();
                    docIdIsPending = replicator.IsDocumentPending("IdNotThere", DefaultCollection);
                    docIdIsPending.ShouldBeFalse();
                } else if (selection == PENDING_DOC_ID_SEL.FILTER) {
                    docIdIsPending.ShouldBeTrue();
                    docIdIsPending = replicator.IsDocumentPending("doc-002", DefaultCollection);
                    docIdIsPending.ShouldBeFalse();
                } else if (selection == PENDING_DOC_ID_SEL.PURGE) {
                    docIdIsPending.ShouldBeFalse();
                }

                replicator.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(50));

                Try.Condition(() => replicator.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().ShouldBeTrue();

                replicator.IsDocumentPending(DocIdForTest, DefaultCollection).ShouldBeFalse();
                token.Remove();
            }

            Thread.Sleep(500); //it takes a while to get the replicator to actually released...
        }

        private void WithActiveReplicationAndQuery(bool isCloseNotDelete)
        {
            using var waitIdleAssert = new WaitAssert();
            using var waitStoppedAssert = new WaitAssert();
            var config = CreateConfig(true, true, true, OtherDb);
            using (var repl = new Replicator(config)) {

                var query = QueryBuilder.Select(SelectResult.Expression(Meta.ID)).From(DataSource.Collection(DefaultCollection));
                using var doc1Listener = new WaitAssert();
                query.AddChangeListener(null, (sender, args) =>
                {
                    foreach (var row in args.Results) {
                        if (row.GetString("id") == "doc1") {
                            doc1Listener.Fulfill();
                        }
                    }
                });

                repl.AddChangeListener((sender, args) =>
                {
                    waitIdleAssert.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Idle;
                    });

                    waitStoppedAssert.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                repl.Start();

                using (var doc = new MutableDocument("doc1")) {
                    doc.SetString("value", "string");
                    DefaultCollection.Save(doc); // Should still trigger since it is pointing to the same DB
                }

                doc1Listener.WaitForResult(TimeSpan.FromSeconds(20));
                waitIdleAssert.WaitForResult(TimeSpan.FromSeconds(10));

                Db.ActiveStoppables.Count.ShouldBe(2);

                if (isCloseNotDelete)
                    Db.Close();
                else
                    Db.Delete();

                Db.ActiveStoppables.Count.ShouldBe(0);
                Db.IsClosedLocked.ShouldBe(true);

                waitStoppedAssert.WaitForResult(TimeSpan.FromSeconds(30));
            }
        }

        private void WithActiveReplications(bool isCloseNotDelete)
        {
            using var waitIdleAssert = new WaitAssert();
            using var waitStoppedAssert = new WaitAssert();
            using var waitIdleAssert1 = new WaitAssert();
            using var waitStoppedAssert1 = new WaitAssert();

            var config = CreateConfig(true, true, true, OtherDb);
            using (var repl = new Replicator(config))
            using (var repl1 = new Replicator(config)) {
                var token = repl.AddChangeListener((sender, args) =>
                {
                    waitIdleAssert.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Idle;
                    });

                    waitStoppedAssert.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                var token1 = repl1.AddChangeListener((sender, args) =>
                {
                    waitIdleAssert1.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Idle;
                    });

                    waitStoppedAssert1.RunConditionalAssert(() =>
                    {
                        return args.Status.Activity == ReplicatorActivityLevel.Stopped;
                    });
                });

                repl.Start();
                repl1.Start();

                using (var doc = new MutableDocument("doc1")) {
                    doc.SetString("value", "string");
                    OtherDefaultCollection.Save(doc); // Should still trigger since it is pointing to the same DB
                }

                waitIdleAssert.WaitForResult(TimeSpan.FromSeconds(10));
                waitIdleAssert1.WaitForResult(TimeSpan.FromSeconds(10));

                Db.ActiveStoppables.Count.ShouldBe(2);

                if (isCloseNotDelete)
                    Db.Close();
                else
                    Db.Delete();

                Db.ActiveStoppables.Count.ShouldBe(0);
                Db.IsClosedLocked.ShouldBe(true);

                waitStoppedAssert.WaitForResult(TimeSpan.FromSeconds(30));
                waitStoppedAssert1.WaitForResult(TimeSpan.FromSeconds(30));
            }
        }

        private void TestConflictResolverExceptionThrown(TestConflictResolver resolver, bool continueWithWorkingResolver = false, bool withBlob = false)
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(true, true, false);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            using (var repl = new Replicator(config)) {
                using var wa = new WaitAssert();
                var token = repl.AddDocumentReplicationListener((sender, args) => {
                    if (args.Documents[0].Id == "doc1" && !args.IsPush) {
                        wa.RunAssert(() => {
                            WriteLine($"Received document listener callback of size {args.Documents.Count}");
                            args.Documents[0].Error.ShouldNotBeNull();
                            args.Documents[0].Error!.Domain.ShouldBe(CouchbaseLiteErrorType.CouchbaseLite,
                                $"because otherwise the wrong error ({args.Documents[0].Error!.Error}) occurred");
                            args.Documents[0].Error!.Error.ShouldBe((int)CouchbaseLiteError.UnexpectedError);
                            var innerException = ((ReplicatedDocument[])args.Documents)[0].Error!.InnerException;
                            if (innerException is InvalidOperationException) {
                                if (withBlob) {
                                    innerException.Message.ShouldBe(CouchbaseLiteErrorMessage.BlobDifferentDatabase);
                                } else {
                                    innerException.Message.ShouldStartWith("Resolved document's database different_db is different from expected database");
                                }
                            } else if (innerException is Exception) {
                                innerException.Message.ShouldBe("Customer side exception");
                            }
                        });
                    }
                });

                repl.Start();

                wa.WaitForResult(TimeSpan.FromSeconds(10));

                Try.Condition(() => repl.Status.Activity == ReplicatorActivityLevel.Stopped)
                    .Times(5)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .Go().ShouldBeTrue();

                repl.Status.Activity.ShouldBe(ReplicatorActivityLevel.Stopped);
                token.Remove();

                if (!continueWithWorkingResolver)
                    return;

                var resolver2  = new TestConflictResolver((conflict) => {
                    var doc = new MutableDocument("doc1");
                    doc.SetString("name", "Human");
                    return doc;
                });

                config.AddCollection(DefaultCollection, new CollectionConfiguration
                {
                    ConflictResolver = resolver2
                });

                RunReplication(config, 0, 0);
            }
        }

        private void TestConflictResolverWins(bool returnRemoteDoc)
        {
            CreateReplicationConflict("doc1");

            var config = CreateConfig(false, true, false);

            var resolver = new TestConflictResolver((conflict) => {
                if (returnRemoteDoc) {
                    return conflict.RemoteDocument;
                } else
                    return conflict.LocalDocument;
            });

            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                ConflictResolver = resolver
            });

            RunReplication(config, 0, 0);

            using (var doc = DefaultCollection.GetDocument("doc1")) {
                doc.ShouldNotBeNull("because otherwise the database is missing 'doc1'");
                if (returnRemoteDoc) {
                    doc!.GetString("name").ShouldBe("Lion");
                    doc.GetBlob("blob")?.Content.ShouldBeEquivalentToFluent(new byte[] { 7, 7, 7 });
                } else {
                    doc!.GetString("name").ShouldBe("Cat");
                    doc.GetBlob("blob")?.Content.ShouldBeEquivalentToFluent(new byte[] { 6, 6, 6 });
                }
            }
        }
#endif

        private void CreateReplicationConflict(string id, bool checkFlags = false)
        {
            unsafe {
                var oddByteArray = new byte[] { 1, 3, 5 };
                C4DocumentFlags flags = (C4DocumentFlags)0;
                using (var doc1 = new MutableDocument(id)) {
                    doc1.SetString("name", "Tiger");
                    doc1.SetBlob("blob", new Blob("text/plaintext", oddByteArray));
                    DefaultCollection.Save(doc1);
                    if (checkFlags) {
                        flags = doc1.c4Doc!.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
                    }
                }

                using (var doc1 = new MutableDocument(id)) {
                    doc1.SetString("name", "Tiger");
                    doc1.SetBlob("blob", new Blob("text/plaintext", oddByteArray));
                    OtherDefaultCollection.Save(doc1);
                    if (checkFlags) {
                        flags = doc1.c4Doc!.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
                    }
                }

                // Force a conflict
                using (var doc1a = DefaultCollection.GetDocument(id))
                using (var doc1aMutable = doc1a?.ToMutable()) {
                    var evilByteArray = new byte[] { 6, 6, 6 };

                    doc1aMutable.ShouldNotBeNull($"because otherwise the database is missing '{id}'");
                    doc1aMutable!.SetString("name", "Cat");
                    doc1aMutable.SetBlob("blob", new Blob("text/plaintext", evilByteArray));
                    DefaultCollection.Save(doc1aMutable);
                    if (checkFlags) {
                        flags = doc1aMutable.c4Doc!.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
                    }
                }

                using (var doc1a = OtherDefaultCollection.GetDocument(id))
                using (var doc1aMutable = doc1a?.ToMutable()) {
                    doc1aMutable.ShouldNotBeNull($"because otherwise the database is missing '{id}'");
                    var luckyByteArray = new byte[] { 7, 7, 7 };

                    doc1aMutable!.SetString("name", "Lion");
                    doc1aMutable.SetBlob("blob", new Blob("text/plaintext", luckyByteArray));
                    OtherDefaultCollection.Save(doc1aMutable);
                    if (checkFlags) {
                        flags = doc1aMutable.c4Doc!.RawDoc->flags;
                        flags.HasFlag(C4DocumentFlags.DocExists | C4DocumentFlags.DocHasAttachments).ShouldBeTrue();
                    }
                }
            }
        }


#if COUCHBASE_ENTERPRISE
        private void TestPushDocWithFilter(bool continuous)
        {
            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2")) {
                doc1.SetString("name", "donotpass");
                DefaultCollection.Save(doc1);

                doc2.SetString("name", "pass");
                DefaultCollection.Save(doc2);
            }

            var config = CreateConfig(true, false, continuous);
            config.AddCollection(DefaultCollection, new CollectionConfiguration
            {
                PushFilter = _replicator__filterCallback
            });

            RunReplication(config, 0, 0);
            _isFilteredCallback.ShouldBeTrue();
            OtherDefaultCollection.GetDocument("doc1").ShouldBeNull("because doc1 is filtered out in the callback");
            OtherDefaultCollection.GetDocument("doc2").ShouldNotBeNull("because doc2 is filtered in in the callback");
            _isFilteredCallback = false;
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous)
        {
            var target = OtherDb;
            return CreateConfig(push, pull, continuous, target);
        }

        private ReplicatorConfiguration CreateConfig(bool push, bool pull, bool continuous, Database target)
        {
            var retVal = new ReplicatorConfiguration(new DatabaseEndpoint(target));
            retVal.AddCollection(DefaultCollection);
            return ModifyConfig(retVal, push, pull, continuous);
        }
#endif

        private bool _replicator__filterCallback(Document document, DocumentFlags flags)
        {
            _isFilteredCallback = true;
            if (flags != 0) {
                return document.Id == "pass";
            }
            document.RevisionID.ShouldNotBeNull();
            var name = document.GetString("name");
            return name == "pass";
        }

        private ReplicatorConfiguration ModifyConfig(ReplicatorConfiguration config, bool push, bool pull, bool continuous)
        {
            var type = (ReplicatorType)0;
            if (push) {
                type |= ReplicatorType.Push;
            }

            if (pull) {
                type |= ReplicatorType.Pull;
            }

            config.ReplicatorType = type;
            config.Continuous = continuous;
            return config;
        }

        private void DocumentEndedUpdate(object? sender, DocumentReplicationEventArgs args)
        {
            _replicationEvents.Add(args);
        }

#if COUCHBASE_ENTERPRISE

        private class MockConnectionFactory : IMessageEndpointDelegate
        {
            private readonly IMockConnectionErrorLogic _errorLogic;

            public MockConnectionFactory(IMockConnectionErrorLogic errorLogic)
            {
                _errorLogic = errorLogic;
            }

            public IMessageEndpointConnection CreateConnection(MessageEndpoint endpoint)
            {
                var retVal =
                    new MockClientConnection(endpoint) {
                        ErrorLogic = _errorLogic,
                    };
                return retVal;
            }
        }

#endif
    }

    public class TestConflictResolver : IConflictResolver
    {
        Func<Conflict, Document?> ResolveFunc { get; set; }

        public TestConflictResolver(Func<Conflict, Document?> resolveFunc)
        {
            ResolveFunc = resolveFunc;
        }

        public Document? Resolve(Conflict conflict)
        {
            return ResolveFunc(conflict);
        }
    }

    public class FakeConflictResolver : IConflictResolver
    {
        public Document? Resolve(Conflict conflict)
        {
            throw new NotImplementedException();
        }
    }

    public class BlockConflictResolver(Func<Conflict, Document> resolveFunc) : IConflictResolver
    {
        public Document Resolve(Conflict conflict) => resolveFunc(conflict);
    }

#if COUCHBASE_ENTERPRISE
    public class TestErrorLogic : IMockConnectionErrorLogic
    {
        private readonly MockConnectionLifecycleLocation _locations;
        private MessagingException? _exception;
        private int _current, _total;

        private TestErrorLogic(MockConnectionLifecycleLocation locations)
        {
            _locations = locations;
        }

        public static TestErrorLogic FailWhen(MockConnectionLifecycleLocation locations)
        {
            return new TestErrorLogic(locations);
        }

        public TestErrorLogic WithRecoverableException(int count = 1)
        {
            _exception = new MessagingException("Test recoverable exception",
                new SocketException((int)SocketError.ConnectionReset), true);
            _total = count;
            return this;
        }

        public TestErrorLogic WithPermanentException()
        {
            _exception = new MessagingException("Test permanent exception",
                new SocketException((int)SocketError.AccessDenied), false);
            _total = Int32.MaxValue;
            return this;
        }

        public bool ShouldClose(MockConnectionLifecycleLocation location)
        {
            return _current < _total && _locations.HasFlag(location);
        }

        public MessagingException CreateException()
        {
            if(_exception == null) {
                throw new InvalidOperationException();
            }

            _current++;
            return _exception!;
        }
    }

    public class ReconnectErrorLogic : IMockConnectionErrorLogic
    {
        public bool ErrorActive { get; set; }

        public bool ShouldClose(MockConnectionLifecycleLocation location)
        {
            return ErrorActive;
        }

        public MessagingException CreateException()
        {
            return new MessagingException("Server no longer listening", null, false);
        }
    }

    public class ListenerAwaiter
    {
        private readonly ListenerToken _token;
        private readonly ManualResetEventSlim _mre = new ManualResetEventSlim();
        private readonly List<Exception> _exceptions = new List<Exception>();
        private readonly MessageEndpointListener _listener;

        public WaitHandle WaitHandle => _mre.WaitHandle;

        public ListenerAwaiter(MessageEndpointListener listener)
        {
            _token = listener.AddChangeListener(CheckForStopped);
            _listener = listener;
        }

        public void Validate()
        {
            _mre.Dispose();
            _exceptions.ShouldBeEmpty("because otherwise an unexpected error occurred");
        }

        private void CheckForStopped(object? sender, MessageEndpointListenerChangedEventArgs e)
        {
            if (e.Status.Error != null) {
                _exceptions.Add(e.Status.Error);
            }

            if (e.Status.Activity == ReplicatorActivityLevel.Stopped) {
                _token.Remove();
                _mre.Set();
            }
        }
    }

#endif
}

