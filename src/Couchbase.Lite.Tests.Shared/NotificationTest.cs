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
//

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
            Db.AddChangeListener(null, (sender, args) =>
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
                    Db.Save(doc);
                }
            });

            wa.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TestDocumentChange()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            doc1 = Db.Save(doc1).ToMutable();

            var doc2 = new MutableDocument("doc2");
            doc2.SetString("name", "Daniel");
            doc2 = Db.Save(doc2).ToMutable();

            Db.AddDocumentChangeListener("doc1", DocumentChanged);
            Db.AddDocumentChangeListener("doc2", DocumentChanged);
            Db.AddDocumentChangeListener("doc3", DocumentChanged);

            _expectedDocumentChanges = new HashSet<string> {
                "doc1",
                "doc2",
                "doc3"
            };
            _wa = new WaitAssert();

            doc1.SetString("name", "Scott Tiger");
            Db.Save(doc1);

            Db.Delete(doc2);

            var doc3 = new MutableDocument("doc3");
            doc3.SetString("name", "Jack");
            Db.Save(doc3);

            _wa.WaitForResult(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task TestAddSameChangeListeners()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            var saved = Db.Save(doc1);
            doc1.Dispose();
            doc1 = saved.ToMutable();

            Db.AddDocumentChangeListener("doc1", DocumentChanged);
            Db.AddDocumentChangeListener("doc1", DocumentChanged);
            Db.AddDocumentChangeListener("doc1", DocumentChanged);
            Db.AddDocumentChangeListener("doc1", DocumentChanged);
            Db.AddDocumentChangeListener("doc1", DocumentChanged);

            _wa = new WaitAssert();
            _expectedDocumentChanges = new HashSet<string> {
                "doc1"
            };
            doc1.SetString("name", "Scott Tiger");
            Db.Save(doc1);

            await Task.Delay(500);
            _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");
        }

        [Fact]
        public async Task TestRemoveDocumentChangeListener()
        {
            var doc1 = new MutableDocument("doc1");
            doc1.SetString("name", "Scott");
            var saved = Db.Save(doc1);
            doc1.Dispose();
            doc1 = saved.ToMutable();

            var token = Db.AddDocumentChangeListener("doc1", DocumentChanged);

            _wa = new WaitAssert();
            _expectedDocumentChanges = new HashSet<string> {
                "doc1"
            };

            doc1.SetString("name", "Scott Tiger");
            saved = Db.Save(doc1);
            doc1.Dispose();
            doc1 = saved.ToMutable();
            _wa.WaitForResult(TimeSpan.FromSeconds(5));

            Db.RemoveChangeListener(token);

            _wa = new WaitAssert();
            _docCallbackShouldThrow = true;
            doc1.SetString("name", "Scott Pilgrim");
            Db.Save(doc1);

            await Task.Delay(500);
            _wa.CaughtExceptions.Should().BeEmpty("because otherwise too many callbacks happened");

            // Remove again
            Db.RemoveChangeListener(token);
        }

        [Fact]
        public void TestExternalChanges()
        {
            using (var db2 = new Database(Db)) {
                var countdownDB = new CountdownEvent(1);
                db2.AddChangeListener((sender, args) =>
                {
                    args.Should().NotBeNull();
                    args.DocumentIDs.Count.Should().Be(10);
                    countdownDB.CurrentCount.Should().Be(1);
                    countdownDB.Signal();
                });

                
                var countdownDoc = new CountdownEvent(1);
                db2.AddDocumentChangeListener("doc-6", (sender, args) =>
                {
                    args.Should().NotBeNull();
                    args.DocumentID.Should().Be("doc-6");
                    using (var doc = Db.GetDocument(args.DocumentID)) {
                        doc.GetString("type").Should().Be("demo");
                        countdownDoc.CurrentCount.Should().Be(1);
                        countdownDoc.Signal();
                    }
                });

                Db.InBatch(() =>
                {
                    for (var i = 0; i < 10; i++) {
                        using (var doc = new MutableDocument($"doc-{i}")) {
                            doc.SetString("type", "demo");
                            Db.Save(doc);
                        }
                    }
                });

                countdownDB.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                countdownDoc.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
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
