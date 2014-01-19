/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Couchbase.Lite;
using Couchbase.Lite.Support;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class MultipartReaderTest : LiteTestCase
	{
        internal class TestMultipartReaderDelegate : IMultipartReaderDelegate
		{
            private List<Byte> currentPartData;

			private IList<IDictionary<string, string>> headersList;

            private IList<List<Byte>> partList;

			public virtual void StartedPart(IDictionary<string, string> headers)
			{
                NUnit.Framework.Assert.IsNull(this.currentPartData);
				if (this.partList == null)
				{
					this.partList = new AList<List<Byte>>();
				}
                currentPartData = new List<Byte>(1024);
                this.partList.AddItem(currentPartData);
				if (this.headersList == null)
				{
					this.headersList = new AList<IDictionary<string, string>>();
				}
				this.headersList.AddItem(headers);
			}

			public virtual void AppendToPart(byte[] data)
			{
                NUnit.Framework.Assert.IsNotNull(this.currentPartData);
                this.currentPartData.AddRange(data);
			}

			public virtual void FinishedPart()
			{
                NUnit.Framework.Assert.IsNotNull(this.currentPartData);
                this.currentPartData = null;
			}

			internal TestMultipartReaderDelegate(MultipartReaderTest _enclosing)
			{
				this._enclosing = _enclosing;
			}

			private readonly MultipartReaderTest _enclosing;
		}

		public virtual void TestParseContentType()
		{
			Encoding utf8 = Sharpen.Extensions.GetEncoding("UTF-8");
			Dictionary<string, byte[]> contentTypes = new Dictionary<string, byte[]>();
			contentTypes.Put("multipart/related; boundary=\"BOUNDARY\"", Sharpen.Runtime.GetBytesForString
				(new string("\r\n--BOUNDARY"), utf8));
			contentTypes.Put("multipart/related; boundary=BOUNDARY", Sharpen.Runtime.GetBytesForString
				(new string("\r\n--BOUNDARY"), utf8));
			contentTypes.Put("multipart/related;boundary=X", Sharpen.Runtime.GetBytesForString
				(new string("\r\n--X"), utf8));
			foreach (string contentType in contentTypes.Keys)
			{
				MultipartReaderDelegate delegate_ = null;
				MultipartReader reader = new MultipartReader(contentType, delegate_);
				byte[] expectedBoundary = (byte[])contentTypes[contentType];
				byte[] boundary = reader.GetBoundary();
				NUnit.Framework.Assert.IsTrue(Arrays.Equals(boundary, expectedBoundary));
			}
			try
			{
				MultipartReaderDelegate delegate_ = null;
				MultipartReader reader = new MultipartReader("multipart/related; boundary=\"BOUNDARY"
					, delegate_);
				NUnit.Framework.Assert.IsTrue("Should not have gotten here, above lines should have thrown exception"
					, false);
			}
			catch (Exception)
			{
			}
		}

		// expected exception
		public virtual void TestParseHeaders()
		{
			string testString = new string("\r\nFoo: Bar\r\n Header : Val ue ");
			MultipartReader reader = new MultipartReader("multipart/related;boundary=X", null
				);
			reader.ParseHeaders(testString);
			NUnit.Framework.Assert.AreEqual(reader.headers.Keys.Count, 2);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSearchFor()
		{
			string testString = new string("\r\n\r\n");
			byte[] testStringBytes = Sharpen.Runtime.GetBytesForString(testString, Sharpen.Extensions.GetEncoding
				("UTF-8"));
			MultipartReader reader = new MultipartReader("multipart/related;boundary=X", null
				);
			reader.AppendData(testStringBytes);
			Range r = reader.SearchFor(testStringBytes, 0);
			NUnit.Framework.Assert.AreEqual(0, r.GetLocation());
			NUnit.Framework.Assert.AreEqual(4, r.GetLength());
			Range r2 = reader.SearchFor(Sharpen.Runtime.GetBytesForString(new string("nomatch"
				), Sharpen.Extensions.GetEncoding("UTF-8")), 0);
			NUnit.Framework.Assert.AreEqual(-1, r2.GetLocation());
			NUnit.Framework.Assert.AreEqual(0, r2.GetLength());
		}

		public virtual void TestReaderOperation()
		{
			Encoding utf8 = Sharpen.Extensions.GetEncoding("UTF-8");
			byte[] mime = Sharpen.Runtime.GetBytesForString(new string("--BOUNDARY\r\nFoo: Bar\r\n Header : Val ue \r\n\r\npart the first\r\n--BOUNDARY  \r\n\r\n2nd part\r\n--BOUNDARY--"
				), utf8);
			ReaderOperationWithMime(mime, "part the first", "2nd part", mime.Length);
			byte[] mime2 = Sharpen.Runtime.GetBytesForString(new string("--BOUNDARY\r\nFoo: Bar\r\n Header : Val ue \r\n\r\npart the first\r\n--BOUNDARY\r\n\r\n2nd part\r\n--BOUNDARY--"
				), utf8);
			ReaderOperationWithMime(mime2, "part the first", "2nd part", mime2.Length);
			StringBuilder mime3Buffer = new StringBuilder();
			StringBuilder mime3BufferFirstPart = new StringBuilder();
			mime3Buffer.Append("--BOUNDARY\r\nFoo: Bar\r\n Header : Val ue \r\n\r\n");
			for (int i = 0; i < 10000; i++)
			{
				mime3BufferFirstPart.Append("large_part_data");
			}
			mime3Buffer.Append(mime3BufferFirstPart);
			mime3Buffer.Append("\r\n--BOUNDARY\r\n\r\n2nd part\r\n--BOUNDARY--");
			byte[] mime3 = Sharpen.Runtime.GetBytesForString(mime3Buffer.ToString(), utf8);
			ReaderOperationWithMime(mime3, mime3BufferFirstPart.ToString(), "2nd part", 1024);
		}

		private void ReaderOperationWithMime(byte[] mime, string part1ExpectedStr, string
			 part2ExpectedStr, int recommendedChunkSize)
		{
			Encoding utf8 = Sharpen.Extensions.GetEncoding("UTF-8");
			// if the caller passes in a special chunksize, which is not equal to mime.length, then
			// lets test the algorithm _only_ at that chunksize.  otherwise, test it at every chunksize
			// between 1 and mime.length.  (this is needed because when testing with a very large mime value,
			// the test takes too long to test at every single chunk size)
			int chunkSize = 1;
			if (recommendedChunkSize != mime.Length)
			{
				chunkSize = recommendedChunkSize;
			}
			for (; chunkSize <= recommendedChunkSize; ++chunkSize)
			{
				ByteArrayInputStream mimeInputStream = new ByteArrayInputStream(mime);
				MultipartReaderTest.TestMultipartReaderDelegate delegate_ = new MultipartReaderTest.TestMultipartReaderDelegate
					(this);
				string contentType = "multipart/related; boundary=\"BOUNDARY\"";
				MultipartReader reader = new MultipartReader(contentType, delegate_);
				NUnit.Framework.Assert.IsFalse(reader.Finished());
				int location = 0;
				int length = 0;
				do
				{
					NUnit.Framework.Assert.IsTrue("Parser didn't stop at end", location < mime.Length
						);
					length = Math.Min(chunkSize, (mime.Length - location));
					byte[] bytesRead = new byte[length];
					mimeInputStream.Read(bytesRead, 0, length);
					reader.AppendData(bytesRead);
					location += chunkSize;
				}
				while (!reader.Finished());
				NUnit.Framework.Assert.AreEqual(delegate_.partList.Count, 2);
				NUnit.Framework.Assert.AreEqual(delegate_.headersList.Count, 2);
				byte[] part1Expected = Sharpen.Runtime.GetBytesForString(part1ExpectedStr, utf8);
				byte[] part2Expected = Sharpen.Runtime.GetBytesForString(part2ExpectedStr, utf8);
				List<Byte> part1 = delegate_.partList[0];
				List<Byte> part2 = delegate_.partList[1];
				NUnit.Framework.Assert.IsTrue(Arrays.Equals(part1.ToByteArray(), part1Expected));
				NUnit.Framework.Assert.IsTrue(Arrays.Equals(part2.ToByteArray(), part2Expected));
				IDictionary<string, string> headers1 = delegate_.headersList[0];
				NUnit.Framework.Assert.IsTrue(headers1.ContainsKey("Foo"));
				NUnit.Framework.Assert.AreEqual(headers1["Foo"], "Bar");
				NUnit.Framework.Assert.IsTrue(headers1.ContainsKey("Header"));
				NUnit.Framework.Assert.AreEqual(headers1["Header"], "Val ue");
			}
		}
	}
}
