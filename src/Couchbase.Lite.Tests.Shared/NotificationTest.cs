//
//  NotificationTest.cs
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


#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Lite;
using FluentAssertions;

using Newtonsoft.Json;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public class NotificationTest : TestCase
    {
        private HashSet<string> _expectedDocumentChanges;
        private HashSet<string> _unexpectedDocumentChanges;
        private WaitAssert _wa;
        private bool _docCallbackShouldThrow;

#if !WINDOWS_UWP
        public NotificationTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestDatabaseChange()
        {
            var wa = new WaitAssert();
            DefaultCollection.AddChangeListener(null, (sender, args) =>
            {
                var docIDs = args.DocumentIDs;
                wa.RunAssert(() =>
                {
                    args.Database.Should().Be(Db);
                    docIDs.Should().HaveCount(10, "because that is the number of expected rows");
                });
            });

            Db.InBatch(() =>
            {
                for (uint i = 0; i < 10; i++) {
                    var doc = new MutableDocument($"doc-{i}");
                    doc.SetString("type", "demo");
                    DefaultCollection.Save(doc);
                }
            });

            wa.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestCollectionChange()
        {
            var wa = new WaitAssert();
            var colA = Db.CreateCollection("colA", "scopeA");

            colA.AddChangeListener(null, (sender, args) =>
            {
                var docIDs = args.DocumentIDs;
                wa.RunAssert(() =>
                {
                    args.Collection.Should().Be(colA);
                    docIDs.Should().HaveCount(10, "because that is the number of expected rows");
                });
            });

            Db.InBatch(() =>
            {
                for (uint i = 0; i < 10; i++) {
                    var doc = new MutableDocument($"doc-{i}");
                    doc.SetString("type", "demo");
                    colA.Save(doc);
                }
            });

            wa.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestDocumentChange()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            SaveDocument(doc1);

            var doc2 = new MutableDocument("doc2");
            doc2.SetString("name", "Daniel");
            SaveDocument(doc2);

            DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);
            DefaultCollection.AddDocumentChangeListener("doc2", DocumentChanged);
            DefaultCollection.AddDocumentChangeListener("doc3", DocumentChanged);

            _expectedDocumentChanges = new HashSet<string> {
                "doc1",
                "doc2",
                "doc3"
            };
            _wa = new WaitAssert();

            doc1.SetString("name", "Scott Tiger");
            DefaultCollection.Save(doc1);

            DefaultCollection.Delete(doc2);

            var doc3 = new MutableDocument("doc3");
            doc3.SetString("name", "Jack");
            DefaultCollection.Save(doc3);

            _wa.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestCollectionDocumentChange()
        {
            using (var colA = Db.CreateCollection("colA", "scopeA"))
            using (var colB = Db.CreateCollection("colB", "scopeA")) {
                _expectedDocumentChanges = new HashSet<string> {
                    "doc1",
                    "doc2",
                    "doc4"
                };
                colA.AddDocumentChangeListener("doc1", DocumentChanged);
                colA.AddDocumentChangeListener("doc2", DocumentChanged);
                colB.AddDocumentChangeListener("doc4", DocumentChanged);
                
                _wa = new WaitAssert();

                var doc1 = new MutableDocument("doc1");
                doc1.SetString("name", "Scott");
                colA.Save(doc1);

                var doc2 = new MutableDocument("doc2");
                doc2.SetString("name", "Daniel");
                colA.Save(doc2);

                var doc4 = new MutableDocument("doc4");
                doc4.SetString("name", "Peter");
                colB.Save(doc4);

                _wa.WaitForResult(TimeSpan.FromSeconds(5));
                _expectedDocumentChanges.Count.Should().Be(0);
                
                _wa = new WaitAssert();

                _expectedDocumentChanges.Add("doc1");
                _expectedDocumentChanges.Add("doc4");
                doc1.SetString("name", "Scott Tiger");
                colA.Save(doc1);
                doc4.SetString("name", "Peter Tiger");
                colB.Save(doc4);

                _wa.WaitForResult(TimeSpan.FromSeconds(5));
                _expectedDocumentChanges.Count.Should().Be(0);

                _wa = new WaitAssert();

                _expectedDocumentChanges.Add("doc2");
                colA.Delete(doc2);

                _wa.WaitForResult(TimeSpan.FromSeconds(5));
                _expectedDocumentChanges.Count.Should().Be(0);

                _expectedDocumentChanges.Add("doc3");
                var doc3 = new MutableDocument("doc3");
                doc3.SetString("name", "Jack");
                colA.Save(doc3);

                Thread.Sleep(1000);

                _expectedDocumentChanges.Count.Should().Be(1, "Because there is no listener to observe doc3 change.");
                _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");
                _expectedDocumentChanges.Clear();
            }
        }

        [Fact]
        public async Task TestAddSameChangeListeners()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            SaveDocument(doc1);

            DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);
            DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);
            DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);
            DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);
            DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);

            _wa = new WaitAssert();
            _expectedDocumentChanges = new HashSet<string> {
                "doc1"
            };
            doc1.SetString("name", "Scott Tiger");
            DefaultCollection.Save(doc1);

            await Task.Delay(500);
            _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");
        }

        [Fact]
        public async Task TestCollectionAddSameChangeListeners()
        {
            var colA = Db.CreateCollection("colA", "scopeA");

            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            colA.Save(doc1);

            colA.AddDocumentChangeListener("doc1", DocumentChanged);
            colA.AddDocumentChangeListener("doc1", DocumentChanged);
            colA.AddDocumentChangeListener("doc1", DocumentChanged);
            colA.AddDocumentChangeListener("doc1", DocumentChanged);
            colA.AddDocumentChangeListener("doc1", DocumentChanged);

            _wa = new WaitAssert();
            _expectedDocumentChanges = new HashSet<string> {
                "doc1"
            };
            doc1.SetString("name", "Scott Tiger");
            colA.Save(doc1);

            await Task.Delay(500);
            _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");
        }

        [Fact]
        public async Task TestRemoveDocumentChangeListener()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            DefaultCollection.Save(doc1);

            var token = DefaultCollection.AddDocumentChangeListener("doc1", DocumentChanged);

            _wa = new WaitAssert();
            _expectedDocumentChanges = new HashSet<string> {
                "doc1"
            };

            doc1.SetString("name", "Scott Tiger");
            DefaultCollection.Save(doc1);
            _wa.WaitForResult(TimeSpan.FromSeconds(5));
            DefaultCollection.RemoveChangeListener(token);

            _wa = new WaitAssert();
            _docCallbackShouldThrow = true;
            doc1.SetString("name", "Scott Pilgrim");
            DefaultCollection.Save(doc1);

            await Task.Delay(500);
            _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");

            // Remove again
            DefaultCollection.RemoveChangeListener(token);
        }

        [Fact]
        public async Task TestCollectionRemoveDocumentChangeListener()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            colA.Save(doc1);

            var token = colA.AddDocumentChangeListener("doc1", DocumentChanged);

            _wa = new WaitAssert();
            _expectedDocumentChanges = new HashSet<string> {
                "doc1"
            };

            doc1.SetString("name", "Scott Tiger");
            colA.Save(doc1);
            _wa.WaitForResult(TimeSpan.FromSeconds(5));
            token.Remove();

            _wa = new WaitAssert();
            _docCallbackShouldThrow = true;
            doc1.SetString("name", "Scott Pilgrim");
            colA.Save(doc1);

            await Task.Delay(500);
            _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");

            // Remove again
            token.Remove();
        }

        [Fact]
        public void TestExternalChanges()
        {
            using (var db2 = new Database(Db)) {
                var countdownDB = new CountdownEvent(1);
                db2.GetDefaultCollection().AddChangeListener((sender, args) =>
                {
                    args.Should().NotBeNull();
                    args.DocumentIDs.Count.Should().Be(10);
                    countdownDB.CurrentCount.Should().Be(1);
                    countdownDB.Signal();
                });

                var countdownDoc = new CountdownEvent(1);
                db2.GetDefaultCollection().AddDocumentChangeListener("doc-6", (sender, args) =>
                {
                    args.Should().NotBeNull();
                    args.DocumentID.Should().Be("doc-6");
                    using (var doc = db2.GetDefaultCollection().GetDocument(args.DocumentID)) {
                        doc.GetString("type").Should().Be("demo");
                        countdownDoc.CurrentCount.Should().Be(1);
                        countdownDoc.Signal();
                    }
                });

                db2.InBatch(() =>
                {
                    for (var i = 0; i < 10; i++) {
                        using (var doc = new MutableDocument($"doc-{i}")) {
                            doc.SetString("type", "demo");
                            db2.GetDefaultCollection().Save(doc);
                        }
                    }
                });

                countdownDB.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                countdownDoc.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            }
        }

        [Fact]
        public void TestCollectionExternalChanges()
        {
            using (var db2 = new Database(Db)) {
                var colB = db2.CreateCollection("colB", "scopeA");
                var countdownDB = new CountdownEvent(1);
                colB.AddChangeListener((sender, args) =>
                {
                    args.Should().NotBeNull();
                    args.DocumentIDs.Count.Should().Be(10);
                    countdownDB.CurrentCount.Should().Be(1);
                    countdownDB.Signal();
                });

                var countdownDoc = new CountdownEvent(1);
                colB.AddDocumentChangeListener("doc-6", (sender, args) =>
                {
                    args.Should().NotBeNull();
                    args.DocumentID.Should().Be("doc-6");
                    using (var doc = colB.GetDocument(args.DocumentID)) {
                        doc.GetString("type").Should().Be("demo");
                        countdownDoc.CurrentCount.Should().Be(1);
                        countdownDoc.Signal();
                    }
                });

                db2.InBatch(() =>
                {
                    for (var i = 0; i < 10; i++) {
                        using (var doc = new MutableDocument($"doc-{i}")) {
                            doc.SetString("type", "demo");
                            colB.Save(doc);
                        }
                    }
                });

                countdownDB.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                countdownDoc.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            }
        }

        [Fact]
        public async Task TestCollectionChangeListener()
        {
            var colA = Db.CreateCollection("colA", "scopeA");
            var colB = Db.CreateCollection("colB", "scopeA");

            using (var doc1 = new MutableDocument("doc1"))
            using (var doc2 = new MutableDocument("doc2"))
            using (var doc1b = new MutableDocument("doc1b"))
            using (var doc2b = new MutableDocument("doc2b")) {
                doc1.SetString("str1", "string1");
                doc2.SetString("str2", "string2");
                colA.Save(doc1);
                colA.Save(doc2);
                colB.Save(doc1b);
                colB.Save(doc2b);

                var t1 = colA.AddChangeListener(CollectionChanged);
                var t2 = colA.AddChangeListener(CollectionChanged);

                _expectedDocumentChanges = new HashSet<string> { "doc1", "doc2", "doc3", "doc1", "doc2", "doc3" };
                _unexpectedDocumentChanges = new HashSet<string> { "doc1b", "doc2b", "doc3b" };
                _wa = new WaitAssert();

                doc1.SetString("name", "Scott Tiger");
                colA.Save(doc1);

                colA.Delete(doc2);

                var doc3 = new MutableDocument("doc3");
                doc3.SetString("name", "Jack");
                colA.Save(doc3);

                doc1b.SetString("name", "Scott Tiger");
                colB.Save(doc1b);

                colB.Delete(doc2b);

                var doc3b = new MutableDocument("doc3b");
                doc3b.SetString("name", "Jack");
                colB.Save(doc3b);

                await Task.Delay(800);
                _expectedDocumentChanges.Count.Should().Be(0);
                _unexpectedDocumentChanges.Count.Should().Be(3);

                //will notify 2 times since there are 2 observers
                _expectedDocumentChanges.Add("doc6"); 
                _expectedDocumentChanges.Add("doc6");
                var doc6 = new MutableDocument("doc6");
                doc6.SetString("name", "Jack");
                colA.Save(doc6);
                await Task.Delay(500);
                _expectedDocumentChanges.Count.Should().Be(0);

                t1.Remove();

                //will only notify 1 time since one of the two observers is removed
                _expectedDocumentChanges.Add("doc4");
                var doc4 = new MutableDocument("doc4");
                doc4.SetString("name", "Jack");
                colA.Save(doc4);
                await Task.Delay(500);
                _expectedDocumentChanges.Count.Should().Be(0);

                t2.Remove();

                //will not notify since all observers are removed
                _expectedDocumentChanges.Add("doc5");
                var doc5 = new MutableDocument("doc5");
                doc5.SetString("name", "Jack");
                colA.Save(doc5);
                await Task.Delay(500);

                _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");
                _expectedDocumentChanges.Count.Should().Be(1);
                _unexpectedDocumentChanges.Count.Should().Be(3);
            }
        }

        private void CollectionChanged(object sender, CollectionChangedEventArgs args)
        {
            if (_docCallbackShouldThrow) {
                _wa.RunAssert(() => throw new InvalidOperationException("Unexpected doc change notification"));
            } else {
                WriteLine($"Received {args.Collection}");
                _wa.RunConditionalAssert(() =>
                {
                    lock (_expectedDocumentChanges) {
                        foreach (var docId in args.DocumentIDs) {
                            _expectedDocumentChanges.Should()
                                .Contain(docId, "because otherwise a rogue notification came");
                            _unexpectedDocumentChanges.Should()
                                .NotContain(docId, "because otherwise a rogue notification came");
                            _expectedDocumentChanges.Remove(docId);
                        }

                        WriteLine(
                            $"Expecting {_expectedDocumentChanges.Count} more changes ({JsonConvert.SerializeObject(_expectedDocumentChanges)})");
                        return _expectedDocumentChanges.Count == 0;
                    }
                });
            }
        }

        private void DocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            if (_docCallbackShouldThrow) {
                _wa.RunAssert(() => throw new InvalidOperationException("Unexpected doc change notification"));
            } else {
                WriteLine($"Received {args.DocumentID}");
                _wa.RunConditionalAssert(() =>
                {
                    lock (_expectedDocumentChanges) {
                        _expectedDocumentChanges.Should()
                            .Contain(args.DocumentID, "because otherwise a rogue notification came");
                        _expectedDocumentChanges.Remove(args.DocumentID);

                        WriteLine(
                            $"Expecting {_expectedDocumentChanges.Count} more changes ({JsonConvert.SerializeObject(_expectedDocumentChanges)})");
                        return _expectedDocumentChanges.Count == 0;
                    }
                });
            }
        }
    }
}
