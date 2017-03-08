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
