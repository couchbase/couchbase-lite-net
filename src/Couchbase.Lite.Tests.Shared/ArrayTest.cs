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
            var array = new MutableArrayObject();
            array.Count.Should().Be(0, "because the array is empty");
            array.ToList().Should().BeEmpty("because the array is empty");

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
            doc.GetArray("array")
                .As<MutableArrayObject>()
                .Should()
                .BeSameAs(array, "because the doc should return the same object");

            var savedDoc = Db.Save(doc);
            savedDoc.GetArray("array").ToList().Should().BeEmpty("because no objects were inserted");
        }

        [Fact]
        public void TestCreateWithCSharpList()
        {
            var data = new[] {"1", "2", "3"};
            var array = new MutableArrayObject(data);
            array.Count.Should().Be(data.Length, "because the two objects should have the same length");
            array.ToList().Should().ContainInOrder(data, "because the contents should match");

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
            doc.GetArray("array")
                .As<object>()
                .Should()
                .BeSameAs(array, "because the doc should return the same object");

            var savedDoc = Db.Save(doc);
            savedDoc.GetArray("array").ToList().Should().ContainInOrder(data, "because the contents should match");
        }

        [Fact]
        public void TestSetCSharpList()
        {
            var data = new[] { "1", "2", "3" };
            var array = new MutableArrayObject();
            array.SetData(data);

            array.Count.Should().Be(data.Length, "because the two objects should have the same length");
            array.ToList().Should().ContainInOrder(data, "because the contents should match");

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
            var savedDoc = Db.Save(doc);
            doc = savedDoc.ToMutable();

            var gotArray = doc.GetArray("array");
            data = new[] {"4", "5", "6"};
            gotArray.SetData(data);

            gotArray.Count.Should().Be(data.Length, "because the two objects should have the same length");
            gotArray.ToList().Should().ContainInOrder(data, "because the contents should match");
        }

        [Fact]
        public void TestAddObjects()
        {
            var array = new MutableArrayObject();
            PopulateData(array);
            var doc = new MutableDocument("doc1");

            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because 11 entries were added");
                a.GetValue(0).Should().Be(true, "because that is what was added");
                a.GetValue(1).Should().Be(false, "because that is what was added");
                a.GetValue(2).Should().Be("string", "because that is what was added");
                a.GetInt(3).Should().Be(0, "because that is what was added");
                a.GetInt(4).Should().Be(1, "because that is what was added");
                a.GetInt(5).Should().Be(-1, "because that is what was added");
                a.GetValue(6).Should().Be(1.1, "because that is what was added");
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
            var array = new MutableArrayObject();
            PopulateData(array);

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
            doc = Db.Save(doc).ToMutable();

            var gotArray = doc.GetArray("array");
            gotArray.Should().NotBeNull("because an array should be present at this key");

            PopulateData(array); // Extra stuff
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(22, "because 11 entries were added");
                a.GetValue(11).Should().Be(true, "because that is what was added");
                a.GetValue(12).Should().Be(false, "because that is what was added");
                a.GetValue(13).Should().Be("string", "because that is what was added");
                a.GetInt(14).Should().Be(0, "because that is what was added");
                a.GetInt(15).Should().Be(1, "because that is what was added");
                a.GetInt(16).Should().Be(-1, "because that is what was added");
                a.GetValue(17).Should().Be(1.1, "because that is what was added");
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
            var array = new MutableArrayObject();

            // Prepare array with placeholders
            for (int i = 0; i < data.Count; i++) {
                array.AddInt(Int32.MinValue);
            }

            for (int i = 0; i < data.Count; i++) {
                array.SetValue(i, data[i]);
            }

            var doc = new MutableDocument("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because 11 entries were added");
                a.GetValue(0).Should().Be(true, "because that is what was added");
                a.GetValue(1).Should().Be(false, "because that is what was added");
                a.GetValue(2).Should().Be("string", "because that is what was added");
                a.GetInt(3).Should().Be(0, "because that is what was added");
                a.GetInt(4).Should().Be(1, "because that is what was added");
                a.GetInt(5).Should().Be(-1, "because that is what was added");
                a.GetValue(6).Should().Be(1.1, "because that is what was added");
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
            var array = new MutableArrayObject();
            PopulateData(array);

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
            doc = Db.Save(doc).ToMutable();
            var gotArray = doc.GetArray("array");

            var data = CreateArrayOfAllTypes();
            array.Count.Should().Be(data.Count, "because the array was populated with this data");

            // Reverse the array
            for (int i = 0; i < data.Count; i++) {
                gotArray.SetValue(i, data[data.Count - i - 1]);
            }

            SaveArray(gotArray, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because 11 entries were added");
                a.GetValue(10).Should().Be(true, "because that is what was added");
                a.GetValue(9).Should().Be(false, "because that is what was added");
                a.GetValue(8).Should().Be("string", "because that is what was added");
                a.GetInt(7).Should().Be(0, "because that is what was added");
                a.GetInt(6).Should().Be(1, "because that is what was added");
                a.GetInt(5).Should().Be(-1, "because that is what was added");
                a.GetValue(4).Should().Be(1.1, "because that is what was added");
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
            var array = new MutableArrayObject();
            array.AddString("a");
            foreach (var index in new[] {-1, 1}) {
                array.Invoking(a => a.SetString(index, "b")).ShouldThrow<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public void TestInsertObject()
        {
            var array = new MutableArrayObject();
            array.InsertString(0, "a");
            array.Count.Should().Be(1, "because one item was inserted");
            array.GetValue(0).Should().Be("a", "because that is what was inserted");

            array.InsertString(0, "c");
            array.Count.Should().Be(2, "because another item was inserted");
            array.Should().ContainInOrder(new[] {"c", "a"}, "because these are the new contents");

            array.InsertString(1, "d");
            array.Count.Should().Be(3, "because another item was inserted");
            array.Should().ContainInOrder(new[] { "c", "d", "a" }, "because these are the new contents");

            array.InsertString(2, "e");
            array.Count.Should().Be(4, "because another item was inserted");
            array.Should().ContainInOrder(new[] { "c", "d", "e", "a" }, "because these are the new contents");

            array.InsertString(4, "f");
            array.Count.Should().Be(5, "because another item was inserted");
            array.Should().ContainInOrder(new[] { "c", "d", "e", "a", "f" }, "because these are the new contents");
        }

        [Fact]
        public void TestInsertObjectToExistingArray()
        {
            var doc = new MutableDocument("doc1");
            doc.SetArray("array", new MutableArrayObject());
            doc = Db.Save(doc).ToMutable();

            var array = doc.GetArray("array");
            array.Should().NotBeNull("because an array exists at this key");

            array.InsertString(0, "a");
            doc = SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(1, "because one item has been inserted");
                array.Should().ContainInOrder(new[] {"a"}, "because those are the correct contents");
            }).ToMutable();

            array.InsertString(0, "c");
            doc = SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(2, "because two items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "a" }, "because those are the correct contents");
            }).ToMutable();

            array.InsertString(1, "d");
            doc = SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(3, "because three items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "d", "a" }, "because those are the correct contents");
            }).ToMutable();

            array.InsertString(2, "e");
            doc = SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(4, "because four items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "d", "e", "a" }, "because those are the correct contents");
            }).ToMutable();

            array.InsertString(4, "f");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(5, "because five items have been inserted");
                array.Should().ContainInOrder(new[] { "c", "d", "e", "a", "f" }, "because those are the correct contents");
            });
        }

        [Fact]
        public void TestInsertObjectOutOfBound()
        {
            var array = new MutableArrayObject();
            array.AddString("a");

            foreach (int index in new[] {-1, 2}) {
                array.Invoking(a => a.InsertString(index, "b")).ShouldThrow<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public void TestRemove()
        {
            var array = new MutableArrayObject();
            PopulateData(array);

            for (int i = array.Count - 1; i >= 0; i--) {
                array.RemoveAt(i);
            }

            var doc = new MutableDocument("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(0, "because all elements were removed");
                a.ToList().Should().BeEmpty("because there are no elements inside");
            });
        }

        [Fact]
        public void TestRemoveExistingArray()
        {
            var array = new MutableArrayObject();
            PopulateData(array);

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
            doc = Db.Save(doc).ToMutable();
            var gotArray = doc.GetArray("array");

            for (int i = gotArray.Count - 1; i >= 0; i--) {
                gotArray.RemoveAt(i);
            }
            
            SaveArray(gotArray, doc, "array", a =>
            {
                a.Count.Should().Be(0, "because all elements were removed");
                a.ToList().Should().BeEmpty("because there are no elements inside");
            });
        }

        [Fact]
        public void TestRemoveOutOfBound()
        {
            var array = new MutableArrayObject();
            array.AddString("a");
            foreach (int index in new[] { -1, 1 }) {
                array.Invoking(a => a.RemoveAt(index)).ShouldThrow<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public void TestCount()
        {
            var array = new MutableArrayObject();
            PopulateData(array);

            var doc = new MutableDocument("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.Count.Should().Be(11, "because that is how many elements were inserted");
            });
        }

        [Fact]
        public void TestGetString()
        {
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
        public void TestGetLong()
        {
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetLong(0).Should().Be(1L, "because a boolean true becomes 1L");
                a.GetLong(1).Should().Be(0L, "because a boolean false becomes 0");
                a.GetLong(2).Should().Be(0L, "because that is the default value");
                a.GetLong(3).Should().Be(0L, "because that is the stored value");
                a.GetLong(4).Should().Be(1L, "because that is the stored value");
                a.GetLong(5).Should().Be(-1L, "because that is the stored value");
                a.GetLong(6).Should().Be(1L, "because that is the truncated value of 1L.1L");
                a.GetLong(7).Should().Be(0L, "because that is the default value");
                a.GetLong(8).Should().Be(0L, "because that is the default value");
                a.GetLong(9).Should().Be(0L, "because that is the default value");
                a.GetLong(10).Should().Be(0L, "because that is the default value");
            });
        }

        [Fact]
        public void TestGetDouble()
        {
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();

            array.AddLong(Int64.MaxValue)
                .AddLong(Int64.MinValue)
                .AddDouble(Double.MaxValue)
                .AddDouble(Double.MinValue);

            var doc = new MutableDocument("doc1");
            SaveArray(array, doc, "array", a =>
            {
                a.GetValue(0).Should().Be(Int64.MaxValue, "because that is the stored value");
                a.GetValue(1).Should().Be(Int64.MinValue, "because that is the stored value");
                a.GetLong(0).Should().Be(Int64.MaxValue, "because that is the stored value");
                a.GetLong(1).Should().Be(Int64.MinValue, "because that is the stored value");
                a.GetValue(2).Should().Be(Double.MaxValue, "because that is the stored value");
                a.GetValue(3).Should().Be(Double.MinValue, "because that is the stored value");
                a.GetDouble(2).Should().Be(Double.MaxValue, "because that is the stored value");
                a.GetDouble(3).Should().Be(Double.MinValue, "because that is the stored value");
            });
        }

        [Fact]
        public void TestSetGetFloatNumbers()
        {
            var array = new MutableArrayObject();

            array.AddDouble(1.00)
                .AddDouble(1.49)
                .AddDouble(1.50)
                .AddDouble(1.51)
                .AddDouble(1.99);

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
            var array = new MutableArrayObject();
            PopulateData(array);
            array.Count.Should().Be(11, "because that is how many elements were inserted");

            var doc = new MutableDocument("doc1");
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
        public void TestGetArray2()
        {
            var mNestedArray = new MutableArrayObject();
            mNestedArray.AddLong(1L).AddString("Hello").AddValue(null);
            var mArray = new MutableArrayObject();
            mArray.AddLong(1L).AddString("Hello").AddValue(null).AddArray(mNestedArray);

            using (var mDoc = new MutableDocument("test")) {
                mDoc.SetArray("array", mArray);

                using (var doc = Db.Save(mDoc)) {
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.GetArray(0).Should().BeNull();
                    array.GetArray(1).Should().BeNull();
                    array.GetArray(2).Should().BeNull();
                    array.GetArray(3).Should().NotBeNull();

                    var nestedArray = array.GetArray(3);
                    nestedArray.ShouldBeEquivalentTo(mNestedArray);
                    array.ShouldBeEquivalentTo(mArray);
                }
            }
        }

        [Fact]
        public void TestSetNestedArray()
        {
            var array1 = new MutableArrayObject();
            var array2 = new MutableArrayObject();
            var array3 = new MutableArrayObject();

            array1.AddArray(array2);
            array2.AddArray(array3);
            array3.AddString("a").AddString("b").AddString("c");

            var doc = new MutableDocument("doc1");
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
        public void TestSetNull()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddValue(null).AddString(null).AddArray(null).AddDictionary(null);
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetValue(0).Should().BeNull();
                    array.GetValue(1).Should().BeNull();
                    array.GetValue(2).Should().BeNull();
                    array.GetValue(3).Should().BeNull();
                });
            }
        }

        [Fact]
        public void TestReplaceArray()
        {
            var doc = new MutableDocument("doc1");
            var array1 = new MutableArrayObject();

            array1.AddString("a")
                .AddString("b")
                .AddString("c");

            array1.Count.Should().Be(3, "because the array has three elements inside");
            array1.Should().ContainInOrder(new[] { "a", "b", "c" }, "because otherwise the contents are incorrect");
            doc.SetArray("array", array1);

            var array2 = new MutableArrayObject();

            array2.AddString("x").AddString("y").AddString("z");

            array2.Count.Should().Be(3, "because the array has three elements inside");
            array2.Should().ContainInOrder(new[] { "x", "y", "z" }, "because otherwise the contents are incorrect");

            doc.SetArray("array", array2);

            array1.AddString("d");
            array1.Count.Should().Be(4, "because another element was added");
            array1.Should().ContainInOrder(new[] { "a", "b", "c", "d" }, "because otherwise the contents are incorrect");
            array2.Count.Should().Be(3, "because array1 should not affect array2");
            array2.Should().ContainInOrder(new[] { "x", "y", "z" }, "because array1 should not affect array2");

            var savedDoc = Db.Save(doc);

            savedDoc.GetArray("array")
                .As<object>()
                .Should()
                .NotBeSameAs(array2, "because a new doc should return a new object");
            var savedArray = savedDoc.GetArray("array");
            savedArray.Count.Should().Be(3, "because there are still just three items");
            savedArray.Should().ContainInOrder(new[] { "x", "y", "z" }, "because otherwise the contents are incorrect");
        }

        [Fact]
        public void TestReplaceArrayDifferentType()
        {
            var doc = new MutableDocument("doc1");
            var array1 = new MutableArrayObject();
            array1.AddString("a")
                .AddString("b")
                .AddString("c");

            array1.Count.Should().Be(3, "because the array has three elements inside");
            array1.Should().ContainInOrder(new[] { "a", "b", "c" }, "because otherwise the contents are incorrect");
            doc.SetArray("array", array1);

            doc.SetString("array", "Daniel Tiger");
            doc.GetValue("array").Should().Be("Daniel Tiger", "because it was replaced");

            array1.AddString("d");
            array1.Count.Should().Be(4, "because another element was added");
            array1.Should().ContainInOrder(new[] { "a", "b", "c", "d" }, "because otherwise the contents are incorrect");

            var savedDoc = Db.Save(doc);
            savedDoc.GetValue("array").Should().Be("Daniel Tiger", "because that is what was saved");
        }

        [Fact]
        public void TestEnumeratingArray()
        {
            var array = new MutableArrayObject();
            for (int i = 0; i < 20; i++) {
                array.AddInt(i);
            }

            var content = array.ToList();
            var result = new List<object>();
            result.AddRange(array);
            result.Should().ContainInOrder(content, "because that is the correct content");

            array.RemoveAt(1);
            array.AddInt(20).AddInt(21);
            content = array.ToList();

            result = new List<object>();
            result.AddRange(array);
            result.Should().ContainInOrder(content, "because that is the correct content");

            var doc = new MutableDocument("doc1");
            doc.SetArray("array", array);
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

        [Fact]
        public void TestAddNull()
        {
            var array = new MutableArrayObject();
            array.AddValue(null);
            using (var doc = new MutableDocument("doc1")) {
                SaveArray(array, doc, "array", a =>
                {
                    a.Count.Should().Be(1);
                    a.GetValue(0).Should().BeNull();
                });
            }
        }

        [Fact]
        public void TestAddInt()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddInt(0);
                mArray.AddInt(Int32.MaxValue);
                mArray.AddInt(Int32.MinValue);
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetInt(0).Should().Be(0);
                    array.GetInt(1).Should().Be(Int32.MaxValue);
                    array.GetInt(2).Should().Be(Int32.MinValue);
                });
            }
        }

        [Fact]
        public void TestSetInt()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddInt(0);
                mArray.AddInt(Int32.MaxValue);
                mArray.AddInt(Int32.MinValue);

                mArray.SetInt(0, Int32.MaxValue);
                mArray.SetInt(1, Int32.MinValue);
                mArray.SetInt(2, 0);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetInt(2).Should().Be(0);
                    array.GetInt(0).Should().Be(Int32.MaxValue);
                    array.GetInt(1).Should().Be(Int32.MinValue);
                });
            }
        }

        [Fact]
        public void TestInsertInt()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddInt(10);
                mArray.InsertInt(0, 0);
                mArray.InsertInt(1, Int32.MaxValue);
                mArray.InsertInt(2, Int32.MinValue);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetInt(0).Should().Be(0);
                    array.GetInt(1).Should().Be(Int32.MaxValue);
                    array.GetInt(2).Should().Be(Int32.MinValue);
                    array.GetInt(3).Should().Be(10);
                });
            }
        }

        [Fact]
        public void TestAddLong()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddLong(0);
                mArray.AddLong(Int64.MaxValue);
                mArray.AddLong(Int64.MinValue);
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetLong(0).Should().Be(0);
                    array.GetLong(1).Should().Be(Int64.MaxValue);
                    array.GetLong(2).Should().Be(Int64.MinValue);
                });
            }
        }

        [Fact]
        public void TestSetLong()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddLong(0);
                mArray.AddLong(Int64.MaxValue);
                mArray.AddLong(Int64.MinValue);

                mArray.SetLong(0, Int64.MaxValue);
                mArray.SetLong(1, Int64.MinValue);
                mArray.SetLong(2, 0);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetLong(2).Should().Be(0);
                    array.GetLong(0).Should().Be(Int64.MaxValue);
                    array.GetLong(1).Should().Be(Int64.MinValue);
                });
            }
        }

        [Fact]
        public void TestInsertLong()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddLong(10);
                mArray.InsertLong(0, 0);
                mArray.InsertLong(1, Int64.MaxValue);
                mArray.InsertLong(2, Int64.MinValue);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetLong(0).Should().Be(0);
                    array.GetLong(1).Should().Be(Int64.MaxValue);
                    array.GetLong(2).Should().Be(Int64.MinValue);
                    array.GetLong(3).Should().Be(10);
                });
            }
        }

        [Fact]
        public void TestAddFloat()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddFloat(0);
                mArray.AddFloat(Single.MaxValue);
                mArray.AddFloat(Single.MinValue);
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetFloat(0).Should().Be(0);
                    array.GetFloat(1).Should().Be(Single.MaxValue);
                    array.GetFloat(2).Should().Be(Single.MinValue);
                });
            }
        }

        [Fact]
        public void TestSetFloat()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddFloat(0);
                mArray.AddFloat(Single.MaxValue);
                mArray.AddFloat(Single.MinValue);

                mArray.SetFloat(0, Single.MaxValue);
                mArray.SetFloat(1, Single.MinValue);
                mArray.SetFloat(2, 0);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetFloat(2).Should().Be(0);
                    array.GetFloat(0).Should().Be(Single.MaxValue);
                    array.GetFloat(1).Should().Be(Single.MinValue);
                });
            }
        }

        [Fact]
        public void TestInsertFloat()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddFloat(10);
                mArray.InsertFloat(0, 0);
                mArray.InsertFloat(1, Single.MaxValue);
                mArray.InsertFloat(2, Single.MinValue);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetFloat(0).Should().Be(0);
                    array.GetFloat(1).Should().Be(Single.MaxValue);
                    array.GetFloat(2).Should().Be(Single.MinValue);
                    array.GetFloat(3).Should().Be(10);
                });
            }
        }

        [Fact]
        public void TestAddDouble()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddDouble(0);
                mArray.AddDouble(Double.MaxValue);
                mArray.AddDouble(Double.MinValue);
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetDouble(0).Should().Be(0);
                    array.GetDouble(1).Should().Be(Double.MaxValue);
                    array.GetDouble(2).Should().Be(Double.MinValue);
                });
            }
        }

        [Fact]
        public void TestSetDouble()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddDouble(0);
                mArray.AddDouble(Double.MaxValue);
                mArray.AddDouble(Double.MinValue);

                mArray.SetDouble(0, Double.MaxValue);
                mArray.SetDouble(1, Double.MinValue);
                mArray.SetDouble(2, 0);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetDouble(2).Should().Be(0);
                    array.GetDouble(0).Should().Be(Double.MaxValue);
                    array.GetDouble(1).Should().Be(Double.MinValue);
                });
            }
        }

        [Fact]
        public void TestInsertDouble()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddDouble(10);
                mArray.InsertDouble(0, 0);
                mArray.InsertDouble(1, Double.MaxValue);
                mArray.InsertDouble(2, Double.MinValue);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetDouble(0).Should().Be(0);
                    array.GetDouble(1).Should().Be(Double.MaxValue);
                    array.GetDouble(2).Should().Be(Double.MinValue);
                    array.GetDouble(3).Should().Be(10);
                });
            }
        }

         [Fact]
        public void TestAddString()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddString("");
                mArray.AddString("Hello");
                mArray.AddString("World");
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetString(0).Should().Be("");
                    array.GetString(1).Should().Be("Hello");
                    array.GetString(2).Should().Be("World");
                });
            }
        }

        [Fact]
        public void TestSetString()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddString("");
                mArray.AddString("Hello");
                mArray.AddString("World");

                mArray.SetString(0, "Hello");
                mArray.SetString(1, "World");
                mArray.SetString(2, "");

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(3);
                    array.GetString(2).Should().Be("");
                    array.GetString(0).Should().Be("Hello");
                    array.GetString(1).Should().Be("World");
                });
            }
        }

        [Fact]
        public void TestInsertString()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddString("");
                mArray.InsertString(0, "Hello");
                mArray.InsertString(1, "World");
                mArray.InsertString(2, "!");

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetString(0).Should().Be("Hello");
                    array.GetString(1).Should().Be("World");
                    array.GetString(2).Should().Be("!");
                    array.GetString(3).Should().Be("");
                });
            }
        }

        [Fact]
        public void TestAddBoolean()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddBoolean(true);
                mArray.AddBoolean(false);
                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(2);
                    array.GetBoolean(0).Should().BeTrue();
                    array.GetBoolean(1).Should().BeFalse();
                });
            }
        }

        [Fact]
        public void TestSetBoolean()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddBoolean(true);
                mArray.AddBoolean(false);

                mArray.SetBoolean(0, false);
                mArray.SetBoolean(1, true);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(2);
                    array.GetBoolean(1).Should().BeTrue();
                    array.GetBoolean(0).Should().BeFalse();
                });
            }
        }

        [Fact]
        public void TestInsertBoolean()
        {
            using (var mDoc = new MutableDocument("test")) {
                var mArray = new MutableArrayObject();
                mArray.AddBoolean(false);
                mArray.AddBoolean(true);
                mArray.InsertBoolean(0, true);
                mArray.InsertBoolean(1, false);

                mDoc.SetArray("array", mArray);
                SaveDocument(mDoc, doc =>
                {
                    doc.Count.Should().Be(1);
                    doc.Contains("array").Should().BeTrue();
                    var array = doc.GetArray("array");
                    array.Should().NotBeNull();
                    array.Count.Should().Be(4);
                    array.GetBoolean(0).Should().BeTrue();
                    array.GetBoolean(1).Should().BeFalse();
                    array.GetBoolean(2).Should().BeFalse();
                    array.GetBoolean(3).Should().BeTrue();
                });
            }
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

            var dict = new MutableDictionaryObject();
            dict.SetString("name", "Scott Tiger");
            array.Add(dict);

            var subarray = new MutableArrayObject();
            subarray.AddString("a")
                .AddString("b")
                .AddString("c");
            array.Add(subarray);
            array.Add(ArrayTestBlob());

            return array;
        }

        private static Blob ArrayTestBlob() => new Blob("text/plain", Encoding.UTF8.GetBytes("12345"));

        private void PopulateData(IMutableArray array)
        {
            var data = CreateArrayOfAllTypes();
            foreach (var o in data) {
                array.AddValue(o);
            }
        }

        private Document SaveArray(MutableArrayObject array, MutableDocument doc, string key, Action<IArray> eval)
        {
            eval(array);

            doc.SetArray(key, array);
            var savedDoc = Db.Save(doc);
            
            var savedArray = savedDoc.GetArray("array");
            eval(savedArray);

            return savedDoc;
        }
    }
}
