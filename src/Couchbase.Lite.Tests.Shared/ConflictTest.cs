//
//  ConflictTest.cs
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
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite;
using FluentAssertions;
using LiteCore;
using LiteCore.Interop;
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
    public sealed class ConflictTest : TestCase
    {
#if !WINDOWS_UWP
        public ConflictTest(ITestOutputHelper output) : base(output)
#else
        public ConflictTest()
#endif
        {
            ConflictResolver = new DoNotResolve();
        }

        [Fact]
        public void TestConflict()
        {
            ConflictResolver = new TheirsWins();
            ReopenDB();
            var doc = SetupConflict();
            Db.Save(doc);
            doc["name"].ToString().Should().Be("Scotty", "because the 'theirs' version should win");

            doc = new MutableDocument("doc2");
            ConflictResolver = new MergeThenTheirsWins();
            ReopenDB();
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            Db.Save(doc);

            // Force a conflict again
            var properties = doc.ToDictionary();
            properties["type"] = "bio";
            properties["gender"] = "male";
            SaveProperties(properties, doc.Id);

            // Save and make sure that the correct conflict resolver won
            doc.Set("type", "bio");
            doc.Set("age", 31);
            Db.Save(doc);

            doc["age"].ToLong().Should().Be(31L, "because 'age' was changed by 'mine' and not 'theirs'");
            doc["type"].ToString().Should().Be("bio", "because 'type' was changed by 'mine' and 'theirs' so 'theirs' should win");
            doc["gender"].ToString().Should().Be("male", "because 'gender' was changed by 'theirs' but not 'mine'");
            doc["name"].ToString().Should().Be("Scott", "because 'name' was unchanged");
        }

        [Fact]
        public void TestConflictResolverGivesUp()
        {
            ConflictResolver = new GiveUp();
            ReopenDB();
            var doc = SetupConflict();
            Db.Invoking(d => d.Save(doc))
                .ShouldThrow<LiteCoreException>()
                .Which.Error.code.Should()
                .Be((int)C4ErrorCode.Conflict, "because the conflict resolver gave up");
        }

        [Fact]
        public void TestDeletionConflict()
        {
            ConflictResolver = new DoNotResolve();
            ReopenDB();
            var doc = SetupConflict();
            Db.Delete(doc);
            doc.IsDeleted.Should().BeFalse("because there was a conflict in place of the deletion");
            doc["name"].ToString().Should().Be("Scotty", "because that was the pre-deletion value");
        }

        [Fact]
        public void TestConflictMineIsDeeper()
        {
            ConflictResolver = null;
            ReopenDB();
            var doc = SetupConflict();
            Db.Save(doc);
            doc["name"].ToString().Should().Be("Scott Pilgrim", "because the current in memory document has a longer history");
        }

        [Fact]
        public void TestConflictTheirsIsDeeper()
        {
            ConflictResolver = null;
            ReopenDB();
            var doc = SetupConflict();

            // Add another revision to the conflict, so it'll have a higher generation
            var properties = doc.ToDictionary();
            properties["name"] = "Scott of the Sahara";
            SaveProperties(properties, doc.Id);
            Db.Save(doc);

            doc["name"].ToString().Should().Be("Scott of the Sahara", "because the conflict has a longer history");
        }

        // https://github.com/couchbase/couchbase-lite-android/issues/1293
        [Fact]
        public void TestConflictWithoutCommonAncestor()
        {
            ConflictResolver = new NoCommonAncestorValidator();
            ReopenDB();

            var props = new Dictionary<string, object> {
                ["hello"] = "world"
            };

            var doc = new MutableDocument("doc1", props);
            Db.Save(doc);

            doc = Db.GetDocument(doc.Id).ToMutable();
            doc.Set("university", 1);
            Db.Save(doc);

            // Create a conflict
            doc = new MutableDocument(doc.Id, props);
            doc.Set("university", 2);

            Db.Invoking(d => d.Save(doc)).ShouldThrow<LiteCoreException>().Which.Error.Should()
                .Be(new C4Error(C4ErrorCode.Conflict));
        }

        private MutableDocument SetupConflict()
        {
            var doc = new MutableDocument("doc1");
            doc.Set("type", "profile");
            doc.Set("name", "Scott");
            Db.Save(doc);

            // Force a conflict
            var properties = doc.ToDictionary();
            properties["name"] = "Scotty";
            SaveProperties(properties, doc.Id);

            doc.Set("name", "Scott Pilgrim");
            return doc;
        }

        private unsafe void SaveProperties(IDictionary<string, object> props, string docID)
        {
            Db.InBatch(() =>
            {
                var tricky =
                    (C4Document*)LiteCoreBridge.Check(err => Native.c4doc_get(Db.c4db, docID, true, err));
                var put = new C4DocPutRequest {
                    docID = tricky->docID,
                    history = &tricky->revID,
                    historyCount = 1,
                    save = true
                };

                var enc = Native.c4db_getSharedFleeceEncoder(Db.c4db);
                props.FLEncode(enc);
                var body = NativeRaw.FLEncoder_Finish(enc, null);
                put.body = (C4Slice)body;

                LiteCoreBridge.Check(err =>
                {
                    var localPut = put;
                    var retVal = Native.c4doc_put(Db.c4db, &localPut, null, err);
                    Native.FLSliceResult_Free(body);
                    return retVal;
                });
            });
        }
    }

    internal class NoCommonAncestorValidator : IConflictResolver
    {
        public Document Resolve(Conflict conflict)
        {
            conflict.Should().NotBeNull();
            conflict.Base.Should().BeNull();
            return conflict.Base; // null -> cause exception
        }
    }

    internal class TheirsWins : IConflictResolver
    {
        public Document Resolve(Conflict conflict)
        {
            return conflict.Theirs;
        }
    }

    internal class MergeThenTheirsWins : IConflictResolver
    {
        public bool RequireBaseRevision { get; set; }

        public Document Resolve(Conflict conflict)
        {
            if (RequireBaseRevision) {
                conflict.Base.Should().NotBeNull();
            }

            var resolved = new MutableDocument();
            if (conflict.Base != null) {
                foreach (var pair in conflict.Base) {
                    resolved.Set(pair.Key, pair.Value);
                }
            }

            var changed = new HashSet<string>();
            foreach (var pair in conflict.Theirs) {
                resolved.Set(pair.Key, pair.Value);
                changed.Add(pair.Key);
            }

            foreach (var pair in conflict.Mine) {
                if (!changed.Contains(pair.Key)) {
                    resolved.Set(pair.Key, pair.Value);
                }
            }

            return resolved;
        }
    }

    internal class GiveUp : IConflictResolver
    {
        public Document Resolve(Conflict conflict)
        {
            return null;
        }
    }

    internal class DoNotResolve : IConflictResolver
    {
        public Document Resolve(Conflict conflict)
        {
            throw new NotImplementedException();
        }
    }
}
