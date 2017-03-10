using Couchbase.Lite;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System;

namespace Test
{
    public sealed class SubdocumentTest : TestCase
    {
        private IDocument _doc;

        public SubdocumentTest(ITestOutputHelper helper)
            : base(helper)
        {
            _doc = Db.DoSync(() => Db["doc1"]);
        }

        [Fact]
        public async Task TestNewSubdocument()
        {
            await _doc.DoAsync(() =>
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
                address.Properties.Should()
                    .ContainKey("street")
                    .WhichValue.Should()
                    .Be("1 Space Ave.", "beacuse that is what was just inserted");

                _doc["address"] = address;
                _doc.GetSubdocument("address").Should().Be(address, "because that is what was just inserted");
                _doc["address"].Should().Be(address, "because that is what was just inserted");
                address.Document.Should().Be(_doc, "beacuse the subdocument now belongs to the document");
                address.ToConcrete()
                    .Parent.Should()
                    .Be(_doc, "beacuse the direct parent of the subdocument is the document");

                _doc.Save();
                address.Exists.Should().BeTrue("because the subdocument has been saved");
                address.Document.Should().Be(_doc, "beacuse the subdocument now belongs to the document");
                address.ToConcrete()
                    .Parent.Should()
                    .Be(_doc, "beacuse the direct parent of the subdocument is the document");
                address["street"].Should().Be("1 Space Ave.", "because that is what was saved");
                address.Properties.Should()
                    .ContainKey("street")
                    .WhichValue.Should()
                    .Be("1 Space Ave.", "beacuse that is what was saved");
                _doc.Properties.Should()
                    .Equal(new Dictionary<string, object> {
                        ["address"] = address
                    }, "because that is the property set that was just inserted");
            });

            ReopenDB();

            await _doc.DoAsync(() => { 
                var address = _doc.GetSubdocument("address");
                address.Exists.Should().BeTrue("because the subdocument has been saved");
                address.Document.Should().Be(_doc, "beacuse the subdocument now belongs to the document");
                address.ToConcrete()
                    .Parent.Should()
                    .Be(_doc, "beacuse the direct parent of the subdocument is the document");
                address["street"].Should().Be("1 Space Ave.", "because that is what was saved");
                address.Properties.Should()
                    .ContainKey("street")
                    .WhichValue.Should()
                    .Be("1 Space Ave.", "beacuse that is what was saved");
                _doc.Properties.Should()
                    .Equal(new Dictionary<string, object> {
                        ["address"] = address
                    }, "because that is the property set that was saved");
            });
        }

        [Fact]
        public async Task TestGetSubdocument()
        {
            await _doc.DoAsync(() =>
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
            });

            ReopenDB();

            await _doc.DoAsync(() => { 
                var address = _doc.GetSubdocument("address");
                address.Should().NotBeNull("because it was saved");
                _doc.GetSubdocument("address").Should().BeSameAs(address, "because the same object should be returned");
                address.Document.Should().Be(_doc, "because the document this object belongs to is _doc");
                address.ToConcrete().Parent.Should().Be(_doc, "beacuse the parent should be _doc");
                address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["street"] = "1 Space Ave."
                }, "because the properties were implicitly set");
                address["street"].Should().Be("1 Space Ave.", "because the properties should be retrievable");
            });
        }

        [Fact]
        public async Task TestNestedSubdocuments()
        {
            await _doc.DoAsync(() =>
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
            });
        }

        [Fact]
        public async Task TestSetDictionary()
        {
            await _doc.DoAsync(() =>
            {
                _doc["address"] = new Dictionary<string, object> {
                    ["street"] = "1 Space Ave."
                };

                var address = _doc.GetSubdocument("address");
                address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["street"] = "1 Space Ave."
                }, "beacuse those are the properties that were inserted");

                _doc.Save();
            });

            ReopenDB();

            await _doc.DoAsync(() => { 
                var address = _doc["address"] as ISubdocument;
                _doc["address"].Should().BeSameAs(address, "because the same object should be returned");
                address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["street"] = "1 Space Ave."
                }, "beacuse those are the properties that were saved");
            });
        }

        [Fact]
        public async Task TestSetDocumentProperties()
        {
            await _doc.DoAsync(() =>
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
                phones.ToConcrete()
                    .Parent.Should()
                    .Be(address, "because the subdocument is directly inside of address");
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
            });
        }

        [Fact]
        public async Task TestCopySubdocument()
        {
            await _doc.DoAsync(() =>
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
                phones.ToConcrete()
                    .Parent.Should()
                    .Be(address, "because the subdocument is directly inside of address");

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
            });
        }

        [Fact]
        public async Task TestSetSubdocumentFromAnotherKey()
        {
            await _doc.DoAsync(() =>
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
                phones.ToConcrete()
                    .Parent.Should()
                    .Be(address, "because the subdocument is directly inside of address");

                _doc["address2"] = address;
                var address2 = _doc.GetSubdocument("address2");
                address2.Should().NotBeSameAs(address, "because a copy should be made");
                address2.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
                address2.ToConcrete().Parent.Should().Be(_doc, "because the subdocument belongs to _doc");
                address2["street"]
                    .Should()
                    .Be(address["street"], "beacuse the two subdocuments should have the same values");

                var phones2 = address2.GetSubdocument("phones");
                phones2.Document.Should().Be(_doc, "because the subdocument is inside of _doc");
                phones2.ToConcrete().Parent.Should().Be(address2, "because the subdocument is inside address2");
                phones2["mobile"]
                    .Should()
                    .Be(phones2["mobile"], "beacuse the two subdocuments should have the same values");
            });
        }

        [Fact]
        public async Task TestSubdocumentArray()
        {
            await _doc.DoAsync(() =>
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

                _doc.Properties = new Dictionary<string, object> {
                    ["subdocs"] = dicts
                };

                var subdocs = _doc.GetArray("subdocs");
                subdocs.Should().HaveCount(4, "because there are four subdocuments in the array");

                var s1 = subdocs[0] as ISubdocument;
                var s2 = subdocs[1] as ISubdocument;
                var s3 = subdocs[2] as ISubdocument;
                var s4 = subdocs[3] as ISubdocument;

                s1["name"].Should().Be("1", "because that is the name that was inserted");
                s2["name"].Should().Be("2", "because that is the name that was inserted");
                s3["name"].Should().Be("3", "because that is the name that was inserted");
                s4["name"].Should().Be("4", "because that is the name that was inserted");

                // Make changes:

                var s5 = SubdocumentFactory.Create();
                s5["name"] = "5";

                var nuSubdocs1 = new List<object> {
                    s5,
                    "dummy",
                    s2,
                    new Dictionary<string, object> {
                        ["name"] = "6"
                    },
                    s1
                };
                _doc["subdocs"] = nuSubdocs1;
                var nuSubdocs2 = _doc.GetArray("subdocs");
                nuSubdocs2[0].Should().Be(s5, "beacuse the array elements should survive in order");
                nuSubdocs2[1].Should().Be("dummy", "beacuse the array elements should survive in order");
                nuSubdocs2[2].Should().Be(s2, "beacuse the array elements should survive in order");
                nuSubdocs2[3].Should().Be(s4, "beacuse the array elements should survive in order");
                nuSubdocs2[4].Should().Be(s1, "beacuse the array elements should survive in order");

                s1["name"].Should().Be("1", "because that is the name that was inserted");
                s2["name"].Should().Be("2", "because that is the name that was inserted");
                s4["name"].Should().Be("6", "because that is the name that was inserted");
                s5["name"].Should().Be("5", "because that is the name that was inserted");

                // Check invalidated
                s3["name"].Should().BeNull("because this subdocument is no longer valid");
                s3.Document.Should().BeNull("because this subdocument is no longer inside a document");
            });
        }

        [Fact]
        public async Task TestSetSubdocumentPropertiesNull()
        {
            await _doc.DoAsync(() =>
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
                address.Should().NotBeNull("because it was just inserted");

                var phones = address.GetSubdocument("phones");
                phones.Should().NotBeNull("because it was just inserted");

                var references = _doc.GetArray("references");
                references.Should().HaveCount(2, "because there are two elements inside");
                var r1 = references[0] as ISubdocument;
                var r2 = references[1] as ISubdocument;
                r1.Should().NotBeNull("because the elements should remain inside");
                r2.Should().NotBeNull("because the elements should remain inside");

                _doc["address"] = null;
                _doc["references"] = null;

                // Check address:
                address.Document.Should().BeNull("because the subdocument is now invalid");
                address.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                address.Properties.Should().BeNull("because the subdocument is now invalid");
                address["street"].Should().BeNull("because the subdocument is now invalid");
                address["phones"].Should().BeNull("because the subdocument is now invalid");

                // Check phones:
                phones.Document.Should().BeNull("because the subdocument is now invalid");
                phones.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                phones.Properties.Should().BeNull("because the subdocument is now invalid");
                phones["mobile"].Should().BeNull("because the subdocument is now invalid");

                // Check references:
                r1.Document.Should().BeNull("because the subdocument is now invalid");
                r1.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                r1.Properties.Should().BeNull("because the subdocument is now invalid");
                r1["name"].Should().BeNull("because the subdocument is now invalid");

                r2.Document.Should().BeNull("because the subdocument is now invalid");
                r2.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                r2.Properties.Should().BeNull("because the subdocument is now invalid");
                r2["name"].Should().BeNull("because the subdocument is now invalid");
            });
        }

        [Fact]
        public async Task TestSetDocumentPropertiesNull()
        {
            await _doc.DoAsync(() =>
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
                address.Should().NotBeNull("because it was just inserted");

                var phones = address.GetSubdocument("phones");
                phones.Should().NotBeNull("because it was just inserted");

                var references = _doc.GetArray("references");
                references.Should().HaveCount(2, "because there are two elements inside");
                var r1 = references[0] as ISubdocument;
                var r2 = references[1] as ISubdocument;
                r1.Should().NotBeNull("because the elements should remain inside");
                r2.Should().NotBeNull("because the elements should remain inside");

                _doc.Properties = null;

                // Check address:
                address.Document.Should().BeNull("because the subdocument is now invalid");
                address.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                address.Properties.Should().BeNull("because the subdocument is now invalid");
                address["street"].Should().BeNull("because the subdocument is now invalid");
                address["phones"].Should().BeNull("because the subdocument is now invalid");

                // Check phones:
                phones.Document.Should().BeNull("because the subdocument is now invalid");
                phones.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                phones.Properties.Should().BeNull("because the subdocument is now invalid");
                phones["mobile"].Should().BeNull("because the subdocument is now invalid");

                // Check references:
                r1.Document.Should().BeNull("because the subdocument is now invalid");
                r1.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                r1.Properties.Should().BeNull("because the subdocument is now invalid");
                r1["name"].Should().BeNull("because the subdocument is now invalid");

                r2.Document.Should().BeNull("because the subdocument is now invalid");
                r2.ToConcrete().Parent.Should().BeNull("because the subdocument is now invalid");
                r2.Properties.Should().BeNull("because the subdocument is now invalid");
                r2["name"].Should().BeNull("because the subdocument is now invalid");
            });
        }

        [Fact]
        public async Task TestReplaceWithNonDict()
        {
            await _doc.DoAsync(() =>
            {
                var address = SubdocumentFactory.Create();
                address["street"] = "1 Star Way.";
                address["street"].Should().Be("1 Star Way.", "because this is the value that was just inserted");
                address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["street"] = "1 Star Way."
                }, "beacuse the properties should be retrievable as a complete set");

                _doc["address"] = address;
                _doc["address"].Should().Be(address, "because this is the value that was just inserted");
                address.Document.Should().Be(_doc, "because _doc now owns address");

                _doc["address"] = "123 Space Dr.";
                address.Document.Should().BeNull("because _doc no longer owns address");
                address.Properties.Should().BeNull("because the subdocument has been invalidated");
            });
        }

        [Fact]
        public async Task TestReplaceWithNewSubdocument()
        {
            await _doc.DoAsync(() =>
            {
                var address = SubdocumentFactory.Create();
                address["street"] = "1 Star Way.";
                address["street"].Should().Be("1 Star Way.", "because this is the value that was just inserted");
                address.Properties.ShouldBeEquivalentTo(new Dictionary<string, object> {
                    ["street"] = "1 Star Way."
                }, "beacuse the properties should be retrievable as a complete set");

                _doc["address"] = address;
                _doc["address"].Should().Be(address, "because this is the value that was just inserted");
                address.Document.Should().Be(_doc, "because _doc now owns address");

                var nuAddress = SubdocumentFactory.Create();
                nuAddress["street"] = "123 Space Dr.";
                _doc["address"] = nuAddress;

                _doc["address"].Should().Be(nuAddress, "beacuse the address was replaced");
                address.Document.Should().BeNull("because _doc no longer owns address");
                address.Properties.Should().BeNull("because address was invalidated");
            });
        }

        [Fact]
        public async Task TestReplaceWithNewDocProperties()
        {
            await _doc.DoAsync(() =>
            {
                _doc.Properties = new Dictionary<string, object> {
                    ["name"] = "Jason",
                    ["address"] = new Dictionary<string, object> {
                        ["street"] = "1 Star Way.",
                        ["phones"] = new Dictionary<string, object> {
                            ["mobile"] = "650-123-4567"
                        }
                    },
                    ["work"] = new Dictionary<string, object> {
                        ["company"] = "Couchbase"    
                    },
                    ["subscription"] = new Dictionary<string, object> {
                        ["type"] = "silver"    
                    },
                    ["expiration"] = new Dictionary<string, object> {
                        ["date"] = "2017-03-03T07:13:46.536Z"    
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
                address.Should().NotBeNull("because it was just inserted");

                var phones = address.GetSubdocument("phones");
                phones.Should().NotBeNull("because it was just inserted");

                var work = _doc.GetSubdocument("work");
                work.Should().NotBeNull("because it was just inserted");

                var subscription = _doc.GetSubdocument("subscription");
                subscription.Should().NotBeNull("because it was just inserted");

                var expiration = _doc.GetSubdocument("expiration");
                expiration.Should().NotBeNull("because it was just inserted");

                var references = _doc.GetArray("references");
                references.Should().HaveCount(2, "because there are two elements inside");
                var r1 = references[0] as ISubdocument;
                var r2 = references[1] as ISubdocument;
                r1.Should().NotBeNull("because the elements should remain inside");
                r2.Should().NotBeNull("because the elements should remain inside");

                var nuSubscription = SubdocumentFactory.Create();
                nuSubscription["type"] = "platinum";

                var date = DateTimeOffset.Now;
                _doc.Properties = new Dictionary<string, object> {
                    ["name"] = "Jason",
                    ["address"] = "1 Star Way.",
                    ["work"] = new Dictionary<string, object> {
                        ["company"] = "Couchbase",
                        ["position"] = "Engineer"
                    },
                    ["subscription"] = nuSubscription,
                    ["expiration"] = date,
                    ["references"] = new List<object> {
                        new Dictionary<string, object> {
                            ["name"] = "Smith"
                        }
                    }
                };

                _doc["address"].Should().Be("1 Star Way.", "because address was replaced");
                address.Document.Should().BeNull("because _doc no longer owns address");
                address.ToConcrete().Parent.Should().BeNull("because address is no longer inside any object");
                address.Properties.Should().BeNull("because address was invalidated");

                phones.Document.Should().BeNull("because _doc no longer owns phones");
                phones.ToConcrete().Parent.Should().BeNull("because phones is no longer inside any object");
                phones.Properties.Should().BeNull("because phones was invalidated");

                _doc["work"].Should().Be(work, "because the existing work subdocument should be updated");
                work["company"].Should().Be("Couchbase", "because this is one of the new work values");
                work["position"].Should().Be("Engineer", "because this is one of the new work values");

                _doc["subscription"].Should().Be(nuSubscription, "because the new subscription should be inside _doc");
                nuSubscription["type"].Should().Be("platinum", "because that is the type of the new subscription");
                subscription.Document.Should().BeNull("because _doc no longer owns subscription");
                subscription.ToConcrete().Parent.Should().BeNull("because subscription is no longer inside any object");
                subscription.Properties.Should().BeNull("because subscription was invalidated");

                _doc.GetDate("expiration").Should().Be(date, "because that is the value that was stored");
                expiration.Document.Should().BeNull("because _doc no longer owns expiration");
                expiration.ToConcrete().Parent.Should().BeNull("because expiration is no longer inside any object");
                expiration.Properties.Should().BeNull("because expiration was invalidated");

                references = _doc.GetArray("references");
                references.Should().HaveCount(1, "because the new collection has only one item");
                references[0]
                    .As<ISubdocument>()
                    .Should()
                    .Be(r1, "because r1 should be updated as the new first object in the array");
                r1.Document.Should().Be(_doc, "because _doc owns r1");
                r1.ToConcrete().Parent.Should().Be(_doc, "because r1 is directly inside of _doc");
                r1["name"].Should().Be("Smith", "because that is the new value in the subdocument");
                r2.Document.Should().BeNull("because _doc no longer owns r2");
                r2.ToConcrete().Parent.Should().BeNull("because r2 is no longer inside any object");
                r2.Properties.Should().BeNull("because r2 was invalidated");
            });
        }

        [Fact]
        public async Task TestDeleteDocument()
        {
            await _doc.DoAsync(() =>
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

                _doc.Save();
                var address = _doc.GetSubdocument("address");
                address.Should().NotBeNull("because it was just inserted");

                var phones = address.GetSubdocument("phones");
                phones.Should().NotBeNull("because it was just inserted");

                var references = _doc.GetArray("references");
                references.Should().HaveCount(2, "because there are two elements inside");
                var r1 = references[0] as ISubdocument;
                var r2 = references[1] as ISubdocument;
                r1.Should().NotBeNull("because the elements should remain inside");
                r2.Should().NotBeNull("because the elements should remain inside");

                _doc.Delete();

                // Check doc:
                _doc.Exists.Should().BeTrue("because the doc exists, even if it is deleted");
                _doc.Properties.Should().BeNull("because the document was deleted");
                _doc["name"].Should().BeNull("because the document was deleted");
                _doc["address"].Should().BeNull("because the document was deleted");
                _doc["references"].Should().BeNull("because the document was deleted");

                // Check address:
                address.Document.Should().BeNull("because _doc was deleted");
                address.ToConcrete().Parent.Should().BeNull("because _doc was deleted");
                address.Exists.Should().BeFalse("because _doc was deleted");
                address.Properties.Should().BeNull("because _doc was deleted");
                address["street"].Should().BeNull("because _doc was deleted");
                address["phones"].Should().BeNull("because _doc was deleted");

                // Check phones:
                phones.Document.Should().BeNull("because _doc was deleted");
                phones.ToConcrete().Parent.Should().BeNull("because _doc was deleted");
                phones.Exists.Should().BeFalse("because _doc was deleted");
                phones.Properties.Should().BeNull("because _doc was deleted");
                phones["mobile"].Should().BeNull("because _doc was deleted");

                // Check references:
                r1.Document.Should().BeNull("because _doc was deleted");
                r1.ToConcrete().Parent.Should().BeNull("because _doc was deleted");
                r1.Exists.Should().BeFalse("because _doc was deleted");
                r1.Properties.Should().BeNull("because _doc was deleted");
                r1["name"].Should().BeNull("because _doc was deleted");

                r2.Document.Should().BeNull("because _doc was deleted");
                r2.ToConcrete().Parent.Should().BeNull("because _doc was deleted");
                r2.Exists.Should().BeFalse("because _doc was deleted");
                r2.Properties.Should().BeNull("because _doc was deleted");
                r2["name"].Should().BeNull("because _doc was deleted");
            });
        }

        protected override void ReopenDB()
        {
            base.ReopenDB();

            _doc = Db.DoSync(() => Db["doc1"]);
        }

        protected override void Dispose(bool disposing)
        {
            _doc.DoSync(() => _doc.Revert());

            base.Dispose(disposing);
        }
    }
}
