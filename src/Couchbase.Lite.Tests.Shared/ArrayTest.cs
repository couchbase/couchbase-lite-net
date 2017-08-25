//
//  ArrayTest.cs
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
    public sealed class ArrayTest : TestCase
    {
        private static readonly DateTimeOffset ArrayTestDate =
            new DateTimeOffset(2017, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);

#if !WINDOWS_UWP
        public ArrayTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestCreate()
        {
            var array = new ArrayObject();
            array.Count.Should().Be(0, "because the array is empty");
            array.ToList().Should().BeEmpty("because the array is empty");

            var doc = new Document("doc1");
            doc.Set("array", array);
            doc.GetArray("array")
                .As<object>()
                .Should()
                .BeSameAs(array, "because the doc should return the same object");

            doc = SaveDocument(doc);
            doc.GetArray("array").ToList().Should().BeEmpty("because no objects were inserted");
        }

        [Fact]
        public void TestCreateWithCSharpList()
        {
            var data = new[] {"1", "2", "3"};
            var array = new ArrayObject(data);
            array.Count.Should().Be(data.Length, "because the two objects should have the same length");
            array.ToList().Should().ContainInOrder(data, "because the contents should match");

            var doc = new Document("doc1");
            doc.Set("array", array);
            doc.GetArray("array")
                .As<object>()
                .Should()
                .BeSameAs(array, "because the doc should return the same object");

            doc = SaveDocument(doc);
            doc.GetArray("array").ToList().Should().ContainInOrder(data, "because the contents should match");
        }

        [Fact]
        public void TestSetCSharpList()
        {
            var data = new[] { "1", "2", "3" };
            IArray array = new ArrayObject();
            array.Set(data);

            array.Count.Should().Be(data.Length, "because the two objects should have the same length");
            array.ToList().Should().ContainInOrder(data, "because the contents should match");

            var doc = new Document("doc1");
            doc.Set("array", array);
            doc = SaveDocument(doc);

            array = doc.GetArray("array");
            data = new[] {"4", "5", "6"};
            array.Set(data);

            array.Count.Should().Be(data.Length, "because the two objects should have the same length");
            array.ToList().Should().ContainInOrder(data, "because the contents should match");
        }

        [Fact]
        public void TestAddObjects()
        {
            var array = new ArrayObject();
            PopulateData(array);
            var doc = new Document("doc1");

            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because 11 entries were added");
                a.GetObject(0).Should().Be(true, "because that is what was added");
                a.GetObject(1).Should().Be(false, "because that is what was added");
                a.GetObject(2).Should().Be("string", "because that is what was added");
                a.GetInt(3).Should().Be(0, "because that is what was added");
                a.GetInt(4).Should().Be(1, "because that is what was added");
                a.GetInt(5).Should().Be(-1, "because that is what was added");
                a.GetObject(6).Should().Be(1.1, "because that is what was added");
                a.GetDate(7).Should().Be(ArrayTestDate, "because that is what was added");

                var subdict = a.GetDictionary(8);
                subdict.Should().NotBeNull("because a dictionary should be present at this index");
                subdict.ToDictionary()
                    .ShouldBeEquivalentTo(new Dictionary<string, object> {
                        ["name"] = "Scott Tiger"
                    }, "because that is what was added");

                var subarray = a.GetArray(9);
                subarray.Should().NotBeNull("because an array should be present at this index");
                subarray.ToList().Should().ContainInOrder(new[] {"a", "b", "c"}, "because that is what was added");
                a.GetBlob(10).Should().Be(ArrayTestBlob(), "because that is what was added");
            });
        }

        [Fact]
        public void TestAddObjectsToExistingArray()
        {
            IArray array = new ArrayObject();
            PopulateData(array);

            var doc = new Document("doc1");
            doc.Set("array", array);
            doc = SaveDocument(doc);

            array = doc.GetArray("array");
            array.Should().NotBeNull("because an array should be present at this key");

            PopulateData(array); // Extra stuff
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(22, "because 11 entries were added");
                a.GetObject(11).Should().Be(true, "because that is what was added");
                a.GetObject(12).Should().Be(false, "because that is what was added");
                a.GetObject(13).Should().Be("string", "because that is what was added");
                a.GetInt(14).Should().Be(0, "because that is what was added");
                a.GetInt(15).Should().Be(1, "because that is what was added");
                a.GetInt(16).Should().Be(-1, "because that is what was added");
                a.GetObject(17).Should().Be(1.1, "because that is what was added");
                a.GetDate(18).Should().Be(ArrayTestDate, "because that is what was added");

                var subdict = a.GetDictionary(19);
                subdict.Should().NotBeNull("because a dictionary should be present at this index");
                subdict.ToDictionary()
                    .ShouldBeEquivalentTo(new Dictionary<string, object> {
                        ["name"] = "Scott Tiger"
                    }, "because that is what was added");

                var subarray = a.GetArray(20);
                subarray.Should().NotBeNull("because an array should be present at this index");
                subarray.ToList().Should().ContainInOrder(new[] { "a", "b", "c" }, "because that is what was added");
                a.GetBlob(21).Should().Be(ArrayTestBlob(), "because that is what was added");
            });
        }

        [Fact]
        public void TestSetObject()
        {
            var data = CreateArrayOfAllTypes();
            var array = new ArrayObject();

            // Prepare array with placeholders
            for (int i = 0; i < data.Count; i++) {
                array.Add(Int32.MinValue);
            }

            for (int i = 0; i < data.Count; i++) {
                array.Set(i, data[i]);
            }

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because 11 entries were added");
                a.GetObject(0).Should().Be(true, "because that is what was added");
                a.GetObject(1).Should().Be(false, "because that is what was added");
                a.GetObject(2).Should().Be("string", "because that is what was added");
                a.GetInt(3).Should().Be(0, "because that is what was added");
                a.GetInt(4).Should().Be(1, "because that is what was added");
                a.GetInt(5).Should().Be(-1, "because that is what was added");
                a.GetObject(6).Should().Be(1.1, "because that is what was added");
                a.GetDate(7).Should().Be(ArrayTestDate, "because that is what was added");

                var subdict = a.GetDictionary(8);
                subdict.Should().NotBeNull("because a dictionary should be present at this index");
                subdict.ToDictionary()
                    .ShouldBeEquivalentTo(new Dictionary<string, object> {
                        ["name"] = "Scott Tiger"
                    }, "because that is what was added");

                var subarray = a.GetArray(9);
                subarray.Should().NotBeNull("because an array should be present at this index");
                subarray.ToList().Should().ContainInOrder(new[] { "a", "b", "c" }, "because that is what was added");
                a.GetBlob(10).Should().Be(ArrayTestBlob(), "because that is what was added");
            });
        }

        [Fact]
        public void TestSetObjectToExistingArray()
        {
            IArray array = new ArrayObject();
            PopulateData(array);

            var doc = new Document("doc1");
            doc.Set("array", array);
            doc = SaveDocument(doc);
            array = doc.GetArray("array");

            var data = CreateArrayOfAllTypes();
            array.Count.Should().Be(data.Count, "because the array was populated with this data");

            // Reverse the array
            for (int i = 0; i < data.Count; i++) {
                array.Set(i, data[data.Count - i - 1]);
            }

            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because 11 entries were added");
                a.GetObject(10).Should().Be(true, "because that is what was added");
                a.GetObject(9).Should().Be(false, "because that is what was added");
                a.GetObject(8).Should().Be("string", "because that is what was added");
                a.GetInt(7).Should().Be(0, "because that is what was added");
                a.GetInt(6).Should().Be(1, "because that is what was added");
                a.GetInt(5).Should().Be(-1, "because that is what was added");
                a.GetObject(4).Should().Be(1.1, "because that is what was added");
                a.GetDate(3).Should().Be(ArrayTestDate, "because that is what was added");

                var subdict = a.GetDictionary(2);
                subdict.Should().NotBeNull("because a dictionary should be present at this index");
                subdict.ToDictionary()
                    .ShouldBeEquivalentTo(new Dictionary<string, object> {
                        ["name"] = "Scott Tiger"
                    }, "because that is what was added");

                var subarray = a.GetArray(1);
                subarray.Should().NotBeNull("because an array should be present at this index");
                subarray.ToList().Should().ContainInOrder(new[] { "a", "b", "c" }, "because that is what was added");
                a.GetBlob(0).Should().Be(ArrayTestBlob(), "because that is what was added");
            });
        }

        [Fact]
        public void TestSetObjectOutOfBound()
        {
            var array = new ArrayObject {"a"};
            foreach (var index in new[] {-1, 1}) {
                array.Invoking(a => a.Set(index, "b")).ShouldThrow<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public void TestInsertObject()
        {
            var array = new ArrayObject();
            array.Insert(0, "a");
            array.Count.Should().Be(1, "because one item was inserted");
            array.GetObject(0).Should().Be("a", "because that is what was inserted");

            array.Insert(0, "c");
            array.Count.Should().Be(2, "because another item was inserted");
            array.Should().ContainInOrder(new[] {"c", "a"}, "because these are the new contents");

            array.Insert(1, "d");
            array.Count.Should().Be(3, "because another item was inserted");
            array.Should().ContainInOrder(new[] { "c", "d", "a" }, "because these are the new contents");

            array.Insert(2, "e");
            array.Count.Should().Be(4, "because another item was inserted");
            array.Should().ContainInOrder(new[] { "c", "d", "e", "a" }, "because these are the new contents");

            array.Insert(4, "f");
            array.Count.Should().Be(5, "because another item was inserted");
            array.Should().ContainInOrder(new[] { "c", "d", "e", "a", "f" }, "because these are the new contents");
        }

        [Fact]
        public void TestInsertObjectToExistingArray()
        {
            var doc = new Document("doc1");
            doc.Set("array", new ArrayObject());
            doc = SaveDocument(doc);

            var array = doc.GetArray("array");
            array.Should().NotBeNull("because an array exists at this key");

            array.Insert(0, "a");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(1, "because one item has been inserted");
                array.Should().ContainInOrder(new[] {"a"}, "because those are the correct contents");
            });

            array.Insert(0, "c");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(2, "because two items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "a" }, "because those are the correct contents");
            });

            array.Insert(1, "d");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(3, "because three items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "d", "a" }, "because those are the correct contents");
            });

            array.Insert(2, "e");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(4, "because four items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "d", "e", "a" }, "because those are the correct contents");
            });

            array.Insert(4, "f");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(5, "because five items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "d", "e", "a", "f" }, "because those are the correct contents");
            });
        }

        [Fact]
        public void TestInsertObjectOutOfBound()
        {
            var array = new ArrayObject {"a"};

            foreach (int index in new[] {-1, 2}) {
                array.Invoking(a => a.Insert(index, "b")).ShouldThrow<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public void TestRemove()
        {
            var array = new ArrayObject();
            PopulateData(array);

            for (int i = array.Count - 1; i >= 0; i--) {
                array.RemoveAt(i);
            }

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(0, "because all elements were removed");
                a.ToList().Should().BeEmpty("because there are no elements inside");
            });
        }

        [Fact]
        public void TestRemoveExistingArray()
        {
            IArray array = new ArrayObject();
            PopulateData(array);

            var doc = new Document("doc1");
            doc.Set("array", array);
            doc = SaveDocument(doc);
            array = doc.GetArray("array");

            for (int i = array.Count - 1; i >= 0; i--) {
                array.RemoveAt(i);
            }
            
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(0, "because all elements were removed");
                a.ToList().Should().BeEmpty("because there are no elements inside");
            });
        }

        [Fact]
        public void TestRemoveOutOfBound()
        {
            var array = new ArrayObject() {"a"};
            foreach (int index in new[] { -1, 1 }) {
                array.Invoking(a => a.RemoveAt(index)).ShouldThrow<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public void TestCount()
        {
            var array = new ArrayObject();
            PopulateData(array);

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because that is how many elements were inserted");
            });
        }

        [Fact]
        public void TestGetString()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetString(0).Should().BeNull("because that is the default value");
                a.GetString(1).Should().BeNull("because that is the default value");
                a.GetString(2).Should().Be("string", "because that is the value at this index");
                a.GetString(3).Should().BeNull("because that is the default value");
                a.GetString(4).Should().BeNull("because that is the default value");
                a.GetString(5).Should().BeNull("because that is the default value");
                a.GetString(6).Should().BeNull("because that is the default value");
                a.GetString(7).Should().Be(ArrayTestDate.ToString("o"), "because the date at this index can be a string");
                a.GetString(8).Should().BeNull("because that is the default value");
                a.GetString(9).Should().BeNull("because that is the default value");
                a.GetString(10).Should().BeNull("because that is the default value");
            });
        }

        [Fact]
        public void TestGetInteger()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetInt(0).Should().Be(1, "because a boolean true becomes 1");
                a.GetInt(1).Should().Be(0, "because a boolean false becomes 0");
                a.GetInt(2).Should().Be(0, "because that is the default value");
                a.GetInt(3).Should().Be(0, "because that is the stored value");
                a.GetInt(4).Should().Be(1, "because that is the stored value");
                a.GetInt(5).Should().Be(-1, "because that is the stored value");
                a.GetInt(6).Should().Be(1, "because that is the truncated value of 1.1");
                a.GetInt(7).Should().Be(0, "because that is the default value");
                a.GetInt(8).Should().Be(0, "because that is the default value");
                a.GetInt(9).Should().Be(0, "because that is the default value");
                a.GetInt(10).Should().Be(0, "because that is the default value");
            });
        }

        [Fact]
        public void TestGetDouble()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetDouble(0).Should().Be(1.0, "because a boolean true becomes 1.0");
                a.GetDouble(1).Should().Be(0.0, "because a boolean false becomes 0.0");
                a.GetDouble(2).Should().Be(0.0, "because that is the default value");
                a.GetDouble(3).Should().Be(0.0, "because 0 becomes 0.0");
                a.GetDouble(4).Should().Be(1.0, "because 1 becomes 1.0");
                a.GetDouble(5).Should().Be(-1.0, "because -1 becomes -1.0");
                a.GetDouble(6).Should().Be(1.1, "because that is the stored value");
                a.GetDouble(7).Should().Be(0.0, "because that is the default value");
                a.GetDouble(8).Should().Be(0.0, "because that is the default value");
                a.GetDouble(9).Should().Be(0.0, "because that is the default value");
                a.GetDouble(10).Should().Be(0.0, "because that is the default value");
            });
        }

        [Fact]
        public void TestGetFloat()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetFloat(0).Should().Be(1.0f, "because a boolean true becomes 1.0f");
                a.GetFloat(1).Should().Be(0.0f, "because a boolean false becomes 0.0f");
                a.GetFloat(2).Should().Be(0.0f, "because that is the default value");
                a.GetFloat(3).Should().Be(0.0f, "because 0 becomes 0.0f");
                a.GetFloat(4).Should().Be(1.0f, "because 1 becomes 1.0f");
                a.GetFloat(5).Should().Be(-1.0f, "because -1 becomes -1.0f");
                a.GetFloat(6).Should().Be(1.1f, "because that is the stored value");
                a.GetFloat(7).Should().Be(0.0f, "because that is the default value");
                a.GetFloat(8).Should().Be(0.0f, "because that is the default value");
                a.GetFloat(9).Should().Be(0.0f, "because that is the default value");
                a.GetFloat(10).Should().Be(0.0f, "because that is the default value");
            });
        }

        [Fact]
        public void TestSetGetMinMaxNumbers()
        {
            var array = new ArrayObject {
                Int64.MaxValue,
                Int64.MinValue,
                Double.MaxValue,
                Double.MinValue
            };

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetObject(0).Should().Be(Int64.MaxValue, "because that is the stored value");
                a.GetObject(1).Should().Be(Int64.MinValue, "because that is the stored value");
                a.GetLong(0).Should().Be(Int64.MaxValue, "because that is the stored value");
                a.GetLong(1).Should().Be(Int64.MinValue, "because that is the stored value");
                a.GetObject(2).Should().Be(Double.MaxValue, "because that is the stored value");
                a.GetObject(3).Should().Be(Double.MinValue, "because that is the stored value");
                a.GetDouble(2).Should().Be(Double.MaxValue, "because that is the stored value");
                a.GetDouble(3).Should().Be(Double.MinValue, "because that is the stored value");
            });
        }

        [Fact]
        public void TestSetGetFloatNumbers()
        {
            var array = new ArrayObject {
                1.00,
                1.49,
                1.50,
                1.51,
                1.99
            };

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetInt(0).Should().Be(1, "because that is the converted value");
                a.GetLong(0).Should().Be(1L, "because that is the converted value");
                a.GetDouble(0).Should().Be(1.00, "because that is the stored value");
                a.GetInt(1).Should().Be(1, "because that is the converted value");
                a.GetLong(1).Should().Be(1L, "because that is the converted value");
                a.GetDouble(1).Should().Be(1.49, "because that is the stored value");
                a.GetInt(2).Should().Be(1, "because that is the converted value");
                a.GetLong(2).Should().Be(1L, "because that is the converted value");
                a.GetDouble(2).Should().Be(1.50, "because that is the stored value");
                a.GetInt(3).Should().Be(1, "because that is the converted value");
                a.GetLong(3).Should().Be(1L, "because that is the converted value");
                a.GetDouble(3).Should().Be(1.51, "because that is the stored value");
                a.GetInt(4).Should().Be(1, "because that is the converted value");
                a.GetLong(4).Should().Be(1L, "because that is the converted value");
                a.GetDouble(4).Should().Be(1.99, "because that is the stored value");
            });
        }

        [Fact]
        public void TestGetBoolean()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetBoolean(0).Should().Be(true, "because that is the stored value");
                a.GetBoolean(1).Should().Be(false, "because that is the stored value");
                a.GetBoolean(2).Should().Be(true, "because that is the value for non-zero objects");
                a.GetBoolean(3).Should().Be(false, "because zero means false");
                a.GetBoolean(4).Should().Be(true, "because non-zero means true");
                a.GetBoolean(5).Should().Be(true, "because non-zero means true");
                a.GetBoolean(6).Should().Be(true, "because non-zero means true");
                a.GetBoolean(7).Should().Be(true, "because that is the value for non-zero objects");
                a.GetBoolean(8).Should().Be(true, "because that is the value for non-zero objects");
                a.GetBoolean(9).Should().Be(true, "because that is the value for non-zero objects");
                a.GetBoolean(10).Should().Be(true, "because that is the value for non-zero objects");
            });
        }

        [Fact]
        public void TestGetDate()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetDate(0).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(1).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(2).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(3).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(4).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(5).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(6).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(7).Should().Be(ArrayTestDate, "because that is the value that was stored");
                a.GetDate(8).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(9).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
                a.GetDate(10).Should().Be(DateTimeOffset.MinValue, "because that is the default value");
            });
        }

        [Fact]
        public void TestGetDictionary()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetDictionary(0).Should().BeNull("because that is the default value");
                a.GetDictionary(1).Should().BeNull("because that is the default value");
                a.GetDictionary(2).Should().BeNull("because that is the default value");
                a.GetDictionary(3).Should().BeNull("because that is the default value");
                a.GetDictionary(4).Should().BeNull("because that is the default value");
                a.GetDictionary(5).Should().BeNull("because that is the default value");
                a.GetDictionary(6).Should().BeNull("because that is the default value");
                a.GetDictionary(7).Should().BeNull("because that is the default value");
                a.GetDictionary(8)
                    .ToDictionary()
                    .ShouldBeEquivalentTo(new Dictionary<string, object> {
                        ["name"] = "Scott Tiger"
                    }, "because that is the stored value");
                a.GetDictionary(9).Should().BeNull("because that is the default value");
                a.GetDictionary(10).Should().BeNull("because that is the default value");
            });
        }

        [Fact]
        public void TestGetArray()
        {
            var array = new ArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new Document("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetArray(0).Should().BeNull("because that is the default value");
                a.GetArray(1).Should().BeNull("because that is the default value");
                a.GetArray(2).Should().BeNull("because that is the default value");
                a.GetArray(3).Should().BeNull("because that is the default value");
                a.GetArray(4).Should().BeNull("because that is the default value");
                a.GetArray(5).Should().BeNull("because that is the default value");
                a.GetArray(6).Should().BeNull("because that is the default value");
                a.GetArray(7).Should().BeNull("because that is the default value");
                a.GetArray(8).Should().BeNull("because that is the default value");
                a.GetArray(9).Should().ContainInOrder(new[] {"a", "b", "c"}, "because that is the stored value");
                a.GetArray(10).Should().BeNull("because that is the default value");
            });
        }

        [Fact]
        public void TestSetNestedArray()
        {
            var array1 = new ArrayObject();
            var array2 = new ArrayObject();
            var array3 = new ArrayObject();

            array1.Add(array2);
            array2.Add(array3);
            array3.Add("a").Add("b").Add("c");

            var doc = new Document("doc1");
            SaveArray(array1, doc, "array", a =>
            {
                var a1 = a;
                a1.Count.Should().Be(1, "because this array has one element");
                var a2 = a1.GetArray(0);
                a2.Count.Should().Be(1, "because this array has one element");
                var a3 = a2.GetArray(0);
                a3.Count.Should().Be(3, "because this array has three elements");
                a3.Should().ContainInOrder(new[] {"a", "b", "c"}, "because otherwise the contents are incorrect");
            });
        }

        [Fact]
        public void TestReplaceArray()
        {
            var doc = new Document("doc1");
            var array1 = new ArrayObject {
                "a",
                "b",
                "c"
            };

            array1.Count.Should().Be(3, "because the array has three elements inside");
            array1.Should().ContainInOrder(new[] { "a", "b", "c" }, "because otherwise the contents are incorrect");
            doc.Set("array", array1);

            IArray array2 = new ArrayObject {
                "x",
                "y",
                "z"
            };

            array2.Count.Should().Be(3, "because the array has three elements inside");
            array2.Should().ContainInOrder(new[] { "x", "y", "z" }, "because otherwise the contents are incorrect");

            doc.Set("array", array2);

            array1.Add("d");
            array1.Count.Should().Be(4, "because another element was added");
            array1.Should().ContainInOrder(new[] { "a", "b", "c", "d" }, "because otherwise the contents are incorrect");
            array2.Count.Should().Be(3, "because array1 should not affect array2");
            array2.Should().ContainInOrder(new[] { "x", "y", "z" }, "because array1 should not affect array2");

            doc = SaveDocument(doc);

            doc.GetArray("array")
                .As<object>()
                .Should()
                .NotBeSameAs(array2, "because a new doc should return a new object");
            array2 = doc.GetArray("array");
            array2.Count.Should().Be(3, "because there are still just three items");
            array2.Should().ContainInOrder(new[] { "x", "y", "z" }, "because otherwise the contents are incorrect");
        }

        [Fact]
        public void TestReplaceArrayDifferentType()
        {
            var doc = new Document("doc1");
            var array1 = new ArrayObject {
                "a",
                "b",
                "c"
            };

            array1.Count.Should().Be(3, "because the array has three elements inside");
            array1.Should().ContainInOrder(new[] { "a", "b", "c" }, "because otherwise the contents are incorrect");
            doc.Set("array", array1);

            doc.Set("array", "Daniel Tiger");
            doc.GetObject("array").Should().Be("Daniel Tiger", "because it was replaced");

            array1.Add("d");
            array1.Count.Should().Be(4, "because another element was added");
            array1.Should().ContainInOrder(new[] { "a", "b", "c", "d" }, "because otherwise the contents are incorrect");

            doc = SaveDocument(doc);
            doc.GetObject("array").Should().Be("Daniel Tiger", "because that is what was saved");
        }

        [Fact]
        public void TestEnumeratingArray()
        {
            var array = new ArrayObject();
            for (int i = 0; i < 20; i++) {
                array.Add(i);
            }

            var content = array.ToList();
            var result = new List<object>();
            result.AddRange(array);
            result.Should().ContainInOrder(content, "because that is the correct content");

            array.RemoveAt(1);
            array.Add(20).Add(21);
            content = array.ToList();

            result = new List<object>();
            result.AddRange(array);
            result.Should().ContainInOrder(content, "because that is the correct content");

            var doc = new Document("doc1");
            doc.Set("array", array);
            SaveArray(array, doc, "array", a =>
            {
                result = new List<object>();
                result.AddRange(a);
                for (int i = 0; i < 20; i++) {
                    Convert.ToInt32(result[i]).Should().Be(Convert.ToInt32(content[i]),
                        $"because that is the correct entry for index {i}");
                }
            });
        }

        private IList<object> CreateArrayOfAllTypes()
        {
            var array = new List<object> {
                true,
                false,
                "string",
                0,
                1,
                -1,
                1.1,
                ArrayTestDate
            };

            var dict = new DictionaryObject();
            dict.Set("name", "Scott Tiger");
            array.Add(dict);

            var subarray = new ArrayObject {
                "a",
                "b",
                "c"
            };
            array.Add(subarray);
            array.Add(ArrayTestBlob());

            return array;
        }

        private static Blob ArrayTestBlob() => new Blob("text/plain", Encoding.UTF8.GetBytes("12345"));

        private void PopulateData(IArray array)
        {
            var data = CreateArrayOfAllTypes();
            foreach (var o in data) {
                array.Add(o);
            }
        }

        private void SaveArray(IArray array, Document doc, string key, Action<IArray> eval)
        {
            eval(array);

            doc.Set(key, array);
            SaveDocument(doc);

            doc = Db.GetDocument(doc.Id);
            array = doc.GetArray("array");

            eval(array);
        }

        private string GetBlobContent(Blob blob)
        {
            return Encoding.UTF8.GetString(blob.Content);
        }
    }
}
