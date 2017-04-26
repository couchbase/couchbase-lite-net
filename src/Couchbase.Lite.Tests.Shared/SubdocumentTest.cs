using Couchbase.Lite;
using FluentAssertions;
using System.Collections.Generic;
using System;
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
    public sealed class SubdocumentTest : TestCase
    {
        private Document _doc;

#if !WINDOWS_UWP
        public SubdocumentTest(ITestOutputHelper helper)
            : base(helper)
#else
        public SubdocumentTest()
#endif
        {
            _doc = new Document("doc1");
        }

        [Fact]
        public void TestNewSubdocument()
        {
            var address = new Subdocument();
            address.Set("street", "1 Space Ave.");
            address["street"].ToString().Should().Be("1 Space Ave.", "because that is what was just inserted");

            _doc.Set("address", address);
            _doc.GetSubdocument("address").Should().BeSameAs(address, "because that is what was just inserted");

            Db.Save(_doc);
            ReopenDB();

            address = _doc.GetSubdocument("address");
            address["street"].ToString().Should().Be("1 Space Ave.", "because that is what was saved");
        }

        [Fact]
        public void TestGetSubdocument()
        {
            _doc.Set(new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Space Ave."
                }
            });

            var address = _doc.GetSubdocument("address");
            address.Should().NotBeNull("because it was implicitly stored");
            _doc.GetSubdocument("address").Should().BeSameAs(address, "because the same object should be returned");
            address["street"].ToString().Should().Be("1 Space Ave.", "because the properties should be retrievable");

            Db.Save(_doc);
            ReopenDB();

            address = _doc.GetSubdocument("address");
            address.Should().NotBeNull("because it was implicitly stored");
            _doc.GetSubdocument("address").Should().BeSameAs(address, "because the same object should be returned");
            address["street"].ToString().Should().Be("1 Space Ave.", "because the properties should be retrievable");
        }

        [Fact]
        public void TestNestedSubdocuments()
        {
            var level1 = new Subdocument();
            level1.Set("name", "n1");
            _doc.Set("level1", level1);

            var level2 = new Subdocument();
            level2.Set("name", "n2");
            level1.Set("level2", level2);

            var level3 = new Subdocument();
            level3.Set("name", "n3");
            level2.Set("level3", level3);

            _doc.GetSubdocument("level1").Should().BeSameAs(level1);
            _doc["level1"]["level2"].ToSubdocument().Should().BeSameAs(level2);
            _doc["level1"]["level2"]["level3"].ToSubdocument().Should().BeSameAs(level3);
        }

        [Fact]
        public void TestSetDocumentDictionary()
        {
            _doc.Set(new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Star Way.",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                },
                ["references"] = new List<object> {
                    new Dictionary<string, object> {["name"] = "Scott"},
                    new Dictionary<string, object> {["name"] = "Sam"}
                }
            });

            var address = _doc.GetSubdocument("address");
            address.GetString("street").Should().Be("1 Star Way.", "because that is what was stored");

            var phones = address.GetSubdocument("phones");
            phones.GetString("mobile").Should().Be("650-123-4567", "because that is what was stored");

            var references = _doc.GetArray("references");
            references.Count.Should().Be(2, "because there are two elements in the array");

            references[0]["name"].ToString().Should().Be("Scott", "because that is what was stored");
            references[1]["name"].ToString().Should().Be("Sam", "because that is what was stored");
        }

        [Fact]
        public void TestSetSubdocumentToAnotherKey()
        {
            _doc.Set(new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Star Way.",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                }
            });

            var address = _doc.GetSubdocument("address");
            _doc.Set("address2", address);
            var address2 = _doc.GetSubdocument("address2");
            address2.Should().BeSameAs(address, "because a copy should not be made");
        }

        [Fact]
        public void TestSubdocumentArray()
        {
            var dicts = new List<object> {
                new Dictionary<string, object> {
                    ["name"] = "1"
                },
                new Dictionary<string, object> {
                    ["name"] = "2"
                },
                new Dictionary<string, object> {
                    ["name"] = "3"
                },
                new Dictionary<string, object> {
                    ["name"] = "4"
                }
            };

            _doc.Set(new Dictionary<string, object> {
                ["subdocs"] = dicts
            });

            var subdocs = _doc.GetArray("subdocs");
            subdocs.Count.Should().Be(4, "because there are four subdocuments in the array");

            var s1 = subdocs[0].ToSubdocument();
            var s2 = subdocs[1].ToSubdocument();
            var s3 = subdocs[2].ToSubdocument();
            var s4 = subdocs[3].ToSubdocument();

            s1["name"].ToString().Should().Be("1", "because that is the name that was inserted");
            s2["name"].ToString().Should().Be("2", "because that is the name that was inserted");
            s3["name"].ToString().Should().Be("3", "because that is the name that was inserted");
            s4["name"].ToString().Should().Be("4", "because that is the name that was inserted");
        }

        [Fact]
        public void TestSetSubdocumentPropertiesNull()
        {
            _doc.Set(new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Star Way.",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                },
                ["references"] = new List<object> {
                    new Dictionary<string, object> {
                        ["name"] = "Scott"
                    },
                    new Dictionary<string, object> {
                        ["name"] = "Sam"
                    }
                }
            });

            var address = _doc.GetSubdocument("address");
            address.Should().NotBeNull("because it was just inserted");

            var phones = address.GetSubdocument("phones");
            phones.Should().NotBeNull("because it was just inserted");

            var references = _doc.GetArray("references");
            references.Count.Should().Be(2, "because there are two elements inside");
            var r1 = references[0].ToSubdocument();
            var r2 = references[1].ToSubdocument();
            r1.Should().NotBeNull("because the elements should remain inside");
            r2.Should().NotBeNull("because the elements should remain inside");

            _doc.Set("address", null);
            _doc.Set("references", null);

            // Check address:
            address["street"].ToString().Should().Be("1 Star Way.", "because the subdocument should not be affected");
            address["phones"].ToSubdocument().Should().BeSameAs(phones, "because the subdocument should not be affected");

            // Check phones:
            phones["mobile"].ToString().Should().Be("650-123-4567", "because the subdocument should not be affected");

            // Check references:
            references.Count.Should().Be(2, "because the subdocument should not be affected");
            r1["name"].ToString().Should().Be("Scott", "because the subdocument should not be affected");
            r2["name"].ToString().Should().Be("Sam", "because the subdocument should not be affected");

            _doc.GetSubdocument("address").Should().BeNull("because the subdocument was removed");
            _doc.GetArray("references").Should().BeNull("because the array was removed");
        }

        [Fact]
        public void TestDeleteDocument()
        {
            _doc.Set(new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Star Way.",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                },
                ["references"] = new List<object> {
                    new Dictionary<string, object> {
                        ["name"] = "Scott"
                    },
                    new Dictionary<string, object> {
                        ["name"] = "Sam"
                    }
                }
            });

            Db.Save(_doc);
            var address = _doc.GetSubdocument("address");
            address.Should().NotBeNull("because it was just inserted");

            var phones = address.GetSubdocument("phones");
            phones.Should().NotBeNull("because it was just inserted");

            var references = _doc.GetArray("references");
            references.Should().HaveCount(2, "because there are two elements inside");
            var r1 = references[0].ToSubdocument();
            var r2 = references[1].ToSubdocument();
            r1.Should().NotBeNull("because the elements should remain inside");
            r2.Should().NotBeNull("because the elements should remain inside");

            Db.Delete(_doc);

            // Check address:
            address["street"].ToString().Should().Be("1 Star Way.", "because the subdocument should not be affected");
            address["phones"].ToSubdocument().Should().BeSameAs(phones, "because the subdocument should not be affected");

            // Check phones:
            phones["mobile"].ToString().Should().Be("650-123-4567", "because the subdocument should not be affected");

            // Check references:
            references.Count.Should().Be(2, "because the subdocument should not be affected");
            r1["name"].ToString().Should().Be("Scott", "because the subdocument should not be affected");
            r2["name"].ToString().Should().Be("Sam", "because the subdocument should not be affected");
        }

        protected override void ReopenDB()
        {
            base.ReopenDB();

            _doc = Db["doc1"] ?? new Document("doc1");
        }

    }
}
