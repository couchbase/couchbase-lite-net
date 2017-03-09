using Couchbase.Lite;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Test
{
    public sealed class SubdocumentTest : TestCase
    {
        private IDocument _doc;

        public SubdocumentTest(ITestOutputHelper helper)
            : base(helper)
        {
            _doc = Db["doc1"];
        }

        [Fact]
        public void TestNewSubdocument()
        {
            _doc.GetSubdocument("address").Should().BeNull("because no subdocument is set yet");
            _doc["address"].Should().BeNull("because not properties are set on the document yet");

            var address = SubdocumentFactory.Create();
            address.Exists.Should().BeFalse("beacuse the subdocument has not been saved yet");
            address.Document.Should().BeNull("beacuse the subdocument has not been saved yet");
            address.ToConcrete().Parent.Should().BeNull("beacuse the subdocument has not been saved yet");
            address.Properties.Should().BeNull("beacuse no properties have been inserted yet");

            address["street"] = "1 Space Ave.";
            address["street"].Should().Be("1 Space Ave.", "because that is what was just inserted");
            address.Properties.Should().ContainKey("street").WhichValue.Should().Be("1 Space Ave.", "beacuse that is what was just inserted");

            _doc["address"] = address;
            _doc.GetSubdocument("address").Should().Be(address, "because that is what was just inserted");
            _doc["address"].Should().Be(address, "because that is what was just inserted");
            address.Document.Should().Be(_doc, "beacuse the subdocument now belongs to the document");
            address.ToConcrete().Parent.Should().Be(_doc, "beacuse the direct parent of the subdocument is the document");

            _doc.Save();
            address.Exists.Should().BeTrue("because the subdocument has been saved");
            address.Document.Should().Be(_doc, "beacuse the subdocument now belongs to the document");
            address.ToConcrete().Parent.Should().Be(_doc, "beacuse the direct parent of the subdocument is the document");
            address["street"].Should().Be("1 Space Ave.", "because that is what was saved");
            address.Properties.Should().ContainKey("street").WhichValue.Should().Be("1 Space Ave.", "beacuse that is what was saved");
            _doc.Properties.Should().Equal(new Dictionary<string, object>
            {
                ["address"] = address
            }, "because that is the property set that was just inserted");

            ReopenDB();

            address = _doc.GetSubdocument("address");
            address.Exists.Should().BeTrue("because the subdocument has been saved");
            address.Document.Should().Be(_doc, "beacuse the subdocument now belongs to the document");
            address.ToConcrete().Parent.Should().Be(_doc, "beacuse the direct parent of the subdocument is the document");
            address["street"].Should().Be("1 Space Ave.", "because that is what was saved");
            address.Properties.Should().ContainKey("street").WhichValue.Should().Be("1 Space Ave.", "beacuse that is what was saved");
            _doc.Properties.Should().Equal(new Dictionary<string, object>
            {
                ["address"] = address
            }, "because that is the property set that was saved");
        }

        [Fact]
        public void TestGetSubdocument()
        {
            var address = _doc.GetSubdocument("address");
            address.Should().BeNull("because nothing has been stored yet");

            _doc.Properties = new Dictionary<string, object> {
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Space Ave."
                }
            };

            address = _doc.GetSubdocument("address");
            address.Should().NotBeNull("because it was implicitly stored");
            _doc.GetSubdocument("address").Should().BeSameAs(address, "because the same object should be returned");
            address.Document.Should().Be(_doc, "because the document this object belongs to is _doc");
            address.ToConcrete().Parent.Should().Be(_doc, "beacuse the parent should be _doc");
            address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["street"] = "1 Space Ave."
            }, "because the properties were implicitly set");
            address["street"].Should().Be("1 Space Ave.", "because the properties should be retrievable");

            _doc.Save();
            address.Should().NotBeNull("because it was saved");
            _doc.GetSubdocument("address").Should().BeSameAs(address, "because the same object should be returned");
            address.Document.Should().Be(_doc, "because the document this object belongs to is _doc");
            address.ToConcrete().Parent.Should().Be(_doc, "beacuse the parent should be _doc");
            address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["street"] = "1 Space Ave."
            }, "because the properties were implicitly set");
            address["street"].Should().Be("1 Space Ave.", "because the properties should be retrievable");

            ReopenDB();

            address = _doc.GetSubdocument("address");
            address.Should().NotBeNull("because it was saved");
            _doc.GetSubdocument("address").Should().BeSameAs(address, "because the same object should be returned");
            address.Document.Should().Be(_doc, "because the document this object belongs to is _doc");
            address.ToConcrete().Parent.Should().Be(_doc, "beacuse the parent should be _doc");
            address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["street"] = "1 Space Ave."
            }, "because the properties were implicitly set");
            address["street"].Should().Be("1 Space Ave.", "because the properties should be retrievable");
        }

        [Fact]
        public void TestNestedSubdocuments()
        {
            _doc["level1"] = SubdocumentFactory.Create();
            _doc.GetSubdocument("level1")["name"] = "n1";

            _doc.GetSubdocument("level1")["level2"] = SubdocumentFactory.Create();
            _doc.GetSubdocument("level1").GetSubdocument("level2")["name"] = "n2";

            _doc.GetSubdocument("level1").GetSubdocument("level2")["level3"] = SubdocumentFactory.Create();
            _doc.GetSubdocument("level1").GetSubdocument("level2").GetSubdocument("level3")["name"] = "n3";

            var level1 = _doc.GetSubdocument("level1");
            var level2 = _doc.GetSubdocument("level1").GetSubdocument("level2");
            var level3 = _doc.GetSubdocument("level1").GetSubdocument("level2").GetSubdocument("level3");

            _doc.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["level1"] = level1
            }, "because the root only contains a subdocument");
            level1.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["level2"] = level2,
                ["name"] = "n1"
            }, "because level1 has a name and another subdocument");
            level2.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["level3"] = level3,
                ["name"] = "n2"
            }, "because level2 has a name and another subdocument");
            level3.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["name"] = "n3"
            }, "because level3 has a name only");
        }

        [Fact]
        public void TestSetDictionary()
        {
            _doc["address"] = new Dictionary<string, object> {
                ["street"] = "1 Space Ave."
            };

            var address = _doc.GetSubdocument("address");
            address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["street"] = "1 Space Ave."
            }, "beacuse those are the properties that were inserted");

            _doc.Save();
            ReopenDB();

            address = _doc["address"] as ISubdocument;
            _doc["address"].Should().BeSameAs(address, "because the same object should be returned");
            address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                ["street"] = "1 Space Ave."
            }, "beacuse those are the properties that were saved");
        }

        [Fact]
        public void TestSetDocumentProperties()
        {
            _doc.Properties = new Dictionary<string, object> {
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
            };

            var address = _doc.GetSubdocument("address");
            address.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
            address.ToConcrete().Parent.Should().Be(_doc, "because the subdocument belongs to _doc");
            address["street"].Should().Be("1 Star Way.", "because that is the address street that is stored");

            var phones = address.GetSubdocument("phones");
            phones.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
            phones.ToConcrete().Parent.Should().Be(address, "because the subdocument is directly inside of address");
            phones["mobile"].Should().Be("650-123-4567", "because that is the mobile number that is stored");

            var references = _doc.GetArray("references");
            references.Should().HaveCount(2, "because two elements were added");

            var r1 = references[0] as ISubdocument;
            r1.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
            r1.ToConcrete().Parent.Should().Be(_doc, "because the subdocument belongs to _doc");
            r1["name"].Should().Be("Scott", "because that is the name that was stored");

            var r2 = references[1] as ISubdocument;
            r2.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
            r2.ToConcrete().Parent.Should().Be(_doc, "because the subdocument belongs to _doc");
            r2["name"].Should().Be("Sam", "because that is the name that was stored");
        }

        [Fact]
        public void TestCopySubdocument()
        {
            _doc.Properties = new Dictionary<string, object> {
                ["name"] = "Jason",
                ["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Star Way.",
                    ["phones"] = new Dictionary<string, object> {
                        ["mobile"] = "650-123-4567"
                    }
                }
            };

            var address = _doc.GetSubdocument("address");
            address.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
            address.ToConcrete().Parent.Should().Be(_doc, "because the subdocument belongs to _doc");

            var phones = address.GetSubdocument("phones");
            phones.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
            phones.ToConcrete().Parent.Should().Be(address, "because the subdocument is directly inside of address");

            var address2 = SubdocumentFactory.Create(address);
            address2.Should().NotBeSameAs(address, "because the factory should create a copy");
            address2.Document.Should().BeNull("beacuse it is not added to a document yet");
            address2.ToConcrete().Parent.Should().BeNull("beacuse it is not added to a document yet");
            address2["street"].Should().Be(address["street"], "because the copy's contents should be the same");

            var phones2 = address2.GetSubdocument("phones");
            phones2.Should().NotBeSameAs(phones, "because the factory should create a copy");
            phones2.Document.Should().BeNull("because it is not added to a document yet");
            phones2.ToConcrete().Parent.Should().Be(address2, "beacuse phones2 is nested in address2");
            phones2["mobile"].Should().Be(phones["mobile"], "because the copy's contents should be the same");
        }

        protected override void ReopenDB()
        {
            base.ReopenDB();

            _doc = Db["doc1"];
        }

        protected override void Dispose(bool disposing)
        {
            _doc.Revert();

            base.Dispose(disposing);
        }
    }
}
