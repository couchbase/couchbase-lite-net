//
//  MultipartTest.cs
//
//  Author:
//      Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Text;
using System.Threading;

using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using NUnit.Framework;

namespace Couchbase.Lite
{
    public class MultipartTest : LiteTestCase
    {
        private const string ATTACH_TEST_DB_NAME = "attach_test";

        public MultipartTest(string storageType) : base(storageType) {}

        [Test]
        public void TestMultipartWriter()
        {
            const string expectedOutput = "\r\n--BOUNDARY\r\nContent-Length: 16\r\n\r\n<part the first>\r\n--BOUNDARY\r\nContent-Length: " +
                "10\r\nContent-Type: something\r\n\r\n<2nd part>\r\n--BOUNDARY--";
            for (var bufSize = 1; bufSize < expectedOutput.Length - 1; ++bufSize) {
                var mp = new MultipartWriter("foo/bar", "BOUNDARY");
                Assert.AreEqual("foo/bar; boundary=\"BOUNDARY\"", mp.ContentType);
                Assert.AreEqual("BOUNDARY", mp.Boundary);
                mp.AddData(Encoding.UTF8.GetBytes("<part the first>"));
                mp.SetNextPartHeaders(new Dictionary<string, string> {
                    { "Content-Type", "something" }
                });
                mp.AddData(Encoding.UTF8.GetBytes("<2nd part>"));
                Assert.AreEqual(expectedOutput.Length, mp.Length);

                var output = mp.AllOutput();
                Assert.IsNotNull(output);
                Assert.AreEqual(expectedOutput, Encoding.UTF8.GetString(output.ToArray()));
                mp.Close();
            }
        }

        [Test]
        public void TestMultipartWriterGzipped()
        {
            var mp = new MultipartWriter("foo/bar", "BOUNDARY");
            var data1 = Enumerable.Repeat((byte)'*', 100);
            mp.SetNextPartHeaders(new Dictionary<string, string> { 
                { "Content-Type", "star-bellies" }
            });
            mp.AddGZippedData(data1);
            var output = mp.AllOutput();

            // Compression flags & OS type will differ depending on which platform this test is run on
            // So we need to compare for "almost" equality.  In this particular output the compression
            // flags are on byte 97 and the os type is in byte 98
            var expectedOutput = GetType().Assembly.GetManifestResourceStream("MultipartStars.mime").ReadAllBytes();
            Assert.AreEqual(expectedOutput.Take(96), output.Take(96));
            Assert.AreEqual(expectedOutput.Skip(98), output.Skip(98));
        }

        [Test]
        public void TestMultipartDocumentReader()
        {
            var mime = GetType().Assembly.GetManifestResourceStream("Multipart1.mime").ReadAllBytes();
            var headers = new Dictionary<string, string> {
                { "Content-Type", "multipart/mixed; boundary=\"BOUNDARY\"" }
            };

            var dict = default(IDictionary<string, object>);
            Assert.DoesNotThrow(() => dict = MultipartDocumentReader.ReadToDatabase(mime, headers, database));

            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "_id", "THX-1138" },
                { "_rev", "1-foobar" },
                { "_attachments", new Dictionary<string, object> {
                        { "mary.txt", new Dictionary<string, object> {
                                { "type", "text/doggerel" },
                                { "length", 52 },
                                { "follows", true },
                                { "digest", "sha1-Jcy8i3K9HZ8UGLO9j+KNbLLjm7M=" }
                            }
                        }
                    }
                }
            }, dict);

            var attachment = dict.Get("_attachments").AsDictionary<string, object>().Get("mary.txt").AsDictionary<string, object>();
            var writer = database.AttachmentWriterForAttachment(attachment);
            Assert.IsNotNull(writer);
            Assert.AreEqual(52, writer.GetLength());

            mime = GetType().Assembly.GetManifestResourceStream("MultipartBinary.mime").ReadAllBytes();
            headers["Content-Type"] = "multipart/mixed; boundary=\"dc0bf3cdc9a6c6e4c46fe2a361c8c5d7\"";
            Assert.DoesNotThrow(() => dict = MultipartDocumentReader.ReadToDatabase(mime, headers, database));
            AssertDictionariesAreEqual(new Dictionary<string, object> {
                { "_id", "038c536dc29ff0f4127705879700062c" },
                { "_rev", "3-e715bcf1865f8283ab1f0ba76e7a92ba" },
                { "_attachments", new Dictionary<string, object> {
                        { "want3.jpg", new Dictionary<string, object> {
                                { "content_type", "image/jpeg" },
                                { "revpos", 3 },
                                { "length", 24758 },
                                { "follows", true },
                                { "digest", "sha1-mmlbbSUTrKoaD67j7Hyjgq2y1aI=" }
                            }
                        },
                        { "Toad.gif", new Dictionary<string, object> {
                                { "content_type", "image/gif" },
                                { "revpos", 2 },
                                { "length", 6566 },
                                { "follows", true },
                                { "digest", "sha1-Y8ppBwk8w1j6nP5rwmeB8FwPtgg=" }
                            }
                        }
                    }
                }
            }, dict);

            attachment = dict.Get("_attachments").AsDictionary<string, object>().Get("Toad.gif").AsDictionary<string, object>();
            writer = database.AttachmentWriterForAttachment(attachment);
            Assert.IsNotNull(writer);
            Assert.AreEqual(6566, writer.GetLength());
            attachment = dict.Get("_attachments").AsDictionary<string, object>().Get("want3.jpg").AsDictionary<string, object>();
            writer = database.AttachmentWriterForAttachment(attachment);
            Assert.IsNotNull(writer);
            Assert.AreEqual(24758, writer.GetLength());

            // Read data that's equivalent to the last one except the JSON is gzipped:
            mime = GetType().Assembly.GetManifestResourceStream("MultipartGZipped.mime").ReadAllBytes();
            headers["Content-Type"] = "multipart/mixed; boundary=\"d7a34c160fd136b5baf17055012e611abcb45dd3fe39fb81831ffd5dc920\"";
            var unzippedDict = default(IDictionary<string, object>);
            Assert.DoesNotThrow(() => unzippedDict = MultipartDocumentReader.ReadToDatabase(mime, headers, database));
            AssertDictionariesAreEqual(dict, unzippedDict);
        }
    }

    public class MultiStreamWriterTest : LiteTestCase
    {
        private const string TAG = "MultiStreamWriterTest";
        private const string EXPECTED_OUTPUT = "<part the first, let us make it a bit longer for greater interest>" +
            "<2nd part, again unnecessarily prolonged for testing purposes beyond any reasonable length...>";

        public MultiStreamWriterTest(string storageType) : base(storageType) {}

        private MultiStreamWriter CreateWriter(int bufferSize)
        {
            var stream = new MultiStreamWriter(bufferSize);
            stream.AddData(Encoding.UTF8.GetBytes("<part the first, let us make it a bit longer for greater interest>"));
            stream.AddData(Encoding.UTF8.GetBytes("<2nd part, again unnecessarily prolonged for testing purposes beyond any reasonable length...>"));
            Assert.AreEqual(EXPECTED_OUTPUT.Length, stream.Length);
            return stream;
        }

        [Test]
        public void TestMultiStreamWriterSync()
        {
            for (int bufferSize = 1; bufferSize < 128; bufferSize++) {
                Log.D(TAG, "Buffer size = {0}", bufferSize);
                var mp = CreateWriter(bufferSize);
                var outputBytes = mp.AllOutput();
                Assert.IsNotNull(outputBytes);
                Assert.AreEqual(EXPECTED_OUTPUT, Encoding.UTF8.GetString(outputBytes.ToArray()));

                // Run it a second time to make sure re-opening works:
                outputBytes = mp.AllOutput();
                Assert.IsNotNull(outputBytes);
                Assert.AreEqual(EXPECTED_OUTPUT, Encoding.UTF8.GetString(outputBytes.ToArray()));
            }
        }

        [Test]
        public void TestMultiStreamWriterAsync()
        {
            var writer = CreateWriter(16);
            var mre = new ManualResetEventSlim();
            var ms = new MemoryStream();
            var bytes = default(IEnumerable<byte>);
            writer.WriteAsync(ms).ContinueWith(t => {
                bytes = ms.ToArray();
                ms.Dispose();
                mre.Set();
            });

            Assert.IsTrue(mre.Wait(TimeSpan.FromSeconds(5)), "Write timed out");
            Assert.AreEqual(EXPECTED_OUTPUT, Encoding.UTF8.GetString(bytes.ToArray()));
        }
    }

    public class MultipartReaderTest : LiteTestCase, IMultipartReaderDelegate
    {
        private const string TAG = "MultipartReaderTest";

        private List<byte> _currentPartData;
        private IList<IEnumerable<byte>> _partList;
        private IList<IDictionary<string, string>> _headerList;

        public MultipartReaderTest(string storageType) : base(storageType) {}

        [Test]
        public void TestTypes()
        {
            var reader = new MultipartReader("multipart/related; boundary=\"BOUNDARY\"", null);
            Assert.AreEqual(Encoding.UTF8.GetBytes("\r\n--BOUNDARY"), reader.Boundary);

            reader = new MultipartReader("multipart/related; boundary=BOUNDARY", null);
            Assert.AreEqual(Encoding.UTF8.GetBytes("\r\n--BOUNDARY"), reader.Boundary);

            Assert.Throws<ArgumentException>(() => reader = new MultipartReader("multipart/related; boundary=\"BOUNDARY", null));

            reader = new MultipartReader("multipart/related; boundary=X", null);
            Assert.AreEqual(Encoding.UTF8.GetBytes("\r\n--X"), reader.Boundary);
        }

        [Test]
        public void TestSimple()
        {
            var mime = Encoding.UTF8.GetBytes("--BOUNDARY\r\nFoo: Bar\r\n Header : Val ue \r\n\r\npart the first\r\n--BOUNDARY  \r\n\r\n2nd part\r\n--BOUNDARY--");
            var expectedParts = new List<IEnumerable<byte>> {
                Encoding.UTF8.GetBytes("part the first"),
                Encoding.UTF8.GetBytes("2nd part")
            };

            var expectedHeaders = new List<IDictionary<string, object>> {
                new Dictionary<string, object> {
                    { "Foo", "Bar" },
                    { "Header", "Val ue" }
                },
                new Dictionary<string, object>()
            };

            for (int chunkSize = 1; chunkSize <= mime.Length; ++chunkSize) {
                Log.D(TAG, "--- chunkSize = {0}", chunkSize);
                Reset();
                var reader = new MultipartReader("multipart/related; boundary=\"BOUNDARY\"", this);
                Assert.IsFalse(reader.Finished);

                Range r = new Range(0, 0);
                do {
                    Assert.IsTrue(r.Location < mime.Length, "Parser didn't stop at end");
                    r.Length = Math.Min(chunkSize, mime.Length - r.Location);
                    var sublist = new Couchbase.Lite.Util.ArraySegment<byte>(mime, r.Location, r.Length);
                    reader.AppendData(sublist);
                    r.Location += chunkSize;
                } while(!reader.Finished);
            }

            Assert.AreEqual(expectedHeaders, _headerList);
            Assert.AreEqual(expectedParts, _partList);
        }

        [Test]
        public void TestGZipped()
        {
            var mime = GetType().Assembly.GetManifestResourceStream("MultipartStars.mime").ReadAllBytes();
            var reader = new MultipartReader("multipart/related; boundary=\"BOUNDARY\"", this);
            reader.AppendData(mime);
            Assert.IsTrue(reader.Finished);

            CollectionAssert.AreEquivalent(new Dictionary<string, object> {
                { "Content-Encoding", "gzip" },
                { "Content-Length", "24" },
                { "Content-Type", "star-bellies" }
            }, _headerList[0]);

            var stars = _partList[0].Decompress();
            Assert.AreEqual(Enumerable.Repeat((byte)'*', 100), stars);
        }

        private void Reset()
        {
            _currentPartData = null;
            _partList = null;
            _headerList = null;
        }

        protected override void SetUp()
        {
            base.SetUp();
            Reset();
        }

        #region IMultipartReaderDelegate implementation

        public void StartedPart(IDictionary<string, string> headers)
        {
            Assert.IsNull(_currentPartData);
            _currentPartData = new List<byte>();
            if (_partList == null) {
                _partList = new List<IEnumerable<byte>>();
            }

            _partList.Add(_currentPartData);
            if (_headerList == null) {
                _headerList = new List<IDictionary<string, string>>();
            }

            _headerList.Add(headers);
        }

        public void AppendToPart(IEnumerable<byte> data)
        {
            Assert.IsNotNull(_currentPartData);
            _currentPartData.AddRange(data);
        }

        public void FinishedPart()
        {
            Assert.IsNotNull(_currentPartData);
            _currentPartData = null;
        }

        #endregion


    }
}
