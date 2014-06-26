//
// MultipartReader.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Text;
using Apache.Http.Util;
using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class MultipartReader
	{
		private enum MultipartReaderState
		{
			kUninitialized,
			kAtStart,
			kInPrologue,
			kInBody,
			kInHeaders,
			kAtEnd,
			kFailed
		}

		private static Encoding utf8 = Sharpen.Extensions.GetEncoding("UTF-8");

		private static byte[] kCRLFCRLF = Sharpen.Runtime.GetBytesForString(new string("\r\n\r\n"
			), utf8);

		private MultipartReader.MultipartReaderState state;

		private ByteArrayBuffer buffer;

		private string contentType;

		private byte[] boundary;

		private MultipartReaderDelegate delegate_;

		public IDictionary<string, string> headers;

		public MultipartReader(string contentType, MultipartReaderDelegate delegate_)
		{
			this.contentType = contentType;
			this.delegate_ = delegate_;
			this.buffer = new ByteArrayBuffer(1024);
			this.state = MultipartReader.MultipartReaderState.kAtStart;
			ParseContentType();
		}

		public virtual byte[] GetBoundary()
		{
			return boundary;
		}

		public virtual byte[] GetBoundaryWithoutLeadingCRLF()
		{
			byte[] rawBoundary = GetBoundary();
			byte[] result = Arrays.CopyOfRange(rawBoundary, 2, rawBoundary.Length);
			return result;
		}

		public virtual bool Finished()
		{
			return state == MultipartReader.MultipartReaderState.kAtEnd;
		}

		private byte[] EomBytes()
		{
			return Sharpen.Runtime.GetBytesForString(new string("--"), Sharpen.Extensions.GetEncoding
				("UTF-8"));
		}

		private bool Memcmp(byte[] array1, byte[] array2, int len)
		{
			bool equals = true;
			for (int i = 0; i < len; i++)
			{
				if (array1[i] != array2[i])
				{
					equals = false;
				}
			}
			return equals;
		}

		public virtual Range SearchFor(byte[] pattern, int start)
		{
			KMPMatch searcher = new KMPMatch();
			int matchIndex = searcher.IndexOf(buffer.ToByteArray(), pattern, start);
			if (matchIndex != -1)
			{
				return new Range(matchIndex, pattern.Length);
			}
			else
			{
				return new Range(matchIndex, 0);
			}
		}

		public virtual void ParseHeaders(string headersStr)
		{
			headers = new Dictionary<string, string>();
			if (headersStr != null && headersStr.Length > 0)
			{
				headersStr = headersStr.Trim();
				StringTokenizer tokenizer = new StringTokenizer(headersStr, "\r\n");
				while (tokenizer.HasMoreTokens())
				{
					string header = tokenizer.NextToken();
					if (!header.Contains(":"))
					{
						throw new ArgumentException("Missing ':' in header line: " + header);
					}
					StringTokenizer headerTokenizer = new StringTokenizer(header, ":");
					string key = headerTokenizer.NextToken().Trim();
					string value = headerTokenizer.NextToken().Trim();
					headers.Put(key, value);
				}
			}
		}

		private void DeleteUpThrough(int location)
		{
			// int start = location + 1;  // start at the first byte after the location
			byte[] newBuffer = Arrays.CopyOfRange(buffer.ToByteArray(), location, buffer.Length
				());
			buffer.Clear();
			buffer.Append(newBuffer, 0, newBuffer.Length);
		}

		private void TrimBuffer()
		{
			int bufLen = buffer.Length();
			int boundaryLen = GetBoundary().Length;
			if (bufLen > boundaryLen)
			{
				// Leave enough bytes in _buffer that we can find an incomplete boundary string
				byte[] dataToAppend = Arrays.CopyOfRange(buffer.ToByteArray(), 0, bufLen - boundaryLen
					);
				delegate_.AppendToPart(dataToAppend);
				DeleteUpThrough(bufLen - boundaryLen);
			}
		}

		public virtual void AppendData(byte[] data)
		{
			if (buffer == null)
			{
				return;
			}
			if (data.Length == 0)
			{
				return;
			}
			buffer.Append(data, 0, data.Length);
			MultipartReader.MultipartReaderState nextState;
			do
			{
				nextState = MultipartReader.MultipartReaderState.kUninitialized;
				int bufLen = buffer.Length();
				switch (state)
				{
					case MultipartReader.MultipartReaderState.kAtStart:
					{
						// Log.d(Database.TAG, "appendData.  bufLen: " + bufLen);
						// The entire message might start with a boundary without a leading CRLF.
						byte[] boundaryWithoutLeadingCRLF = GetBoundaryWithoutLeadingCRLF();
						if (bufLen >= boundaryWithoutLeadingCRLF.Length)
						{
							// if (Arrays.equals(buffer.toByteArray(), boundaryWithoutLeadingCRLF)) {
							if (Memcmp(buffer.ToByteArray(), boundaryWithoutLeadingCRLF, boundaryWithoutLeadingCRLF
								.Length))
							{
								DeleteUpThrough(boundaryWithoutLeadingCRLF.Length);
								nextState = MultipartReader.MultipartReaderState.kInHeaders;
							}
							else
							{
								nextState = MultipartReader.MultipartReaderState.kInPrologue;
							}
						}
						break;
					}

					case MultipartReader.MultipartReaderState.kInPrologue:
					case MultipartReader.MultipartReaderState.kInBody:
					{
						// Look for the next part boundary in the data we just added and the ending bytes of
						// the previous data (in case the boundary string is split across calls)
						if (bufLen < boundary.Length)
						{
							break;
						}
						int start = Math.Max(0, bufLen - data.Length - boundary.Length);
						Range r = SearchFor(boundary, start);
						if (r.GetLength() > 0)
						{
							if (state == MultipartReader.MultipartReaderState.kInBody)
							{
								byte[] dataToAppend = Arrays.CopyOfRange(buffer.ToByteArray(), 0, r.GetLocation()
									);
								delegate_.AppendToPart(dataToAppend);
								delegate_.FinishedPart();
							}
							DeleteUpThrough(r.GetLocation() + r.GetLength());
							nextState = MultipartReader.MultipartReaderState.kInHeaders;
						}
						else
						{
							TrimBuffer();
						}
						break;
					}

					case MultipartReader.MultipartReaderState.kInHeaders:
					{
						// First check for the end-of-message string ("--" after separator):
						if (bufLen >= 2 && Memcmp(buffer.ToByteArray(), EomBytes(), 2))
						{
							state = MultipartReader.MultipartReaderState.kAtEnd;
							Close();
							return;
						}
						// Otherwise look for two CRLFs that delimit the end of the headers:
						Range r = SearchFor(kCRLFCRLF, 0);
						if (r.GetLength() > 0)
						{
							byte[] headersBytes = Arrays.CopyOf(buffer.ToByteArray(), r.GetLocation());
							// byte[] headersBytes = Arrays.copyOfRange(buffer.toByteArray(), 0, r.getLocation())  <-- better?
							string headersString = new string(headersBytes, utf8);
							ParseHeaders(headersString);
							DeleteUpThrough(r.GetLocation() + r.GetLength());
							delegate_.StartedPart(headers);
							nextState = MultipartReader.MultipartReaderState.kInBody;
						}
						break;
					}

					default:
					{
						throw new InvalidOperationException("Unexpected data after end of MIME body");
					}
				}
				if (nextState != MultipartReader.MultipartReaderState.kUninitialized)
				{
					state = nextState;
				}
			}
			while (nextState != MultipartReader.MultipartReaderState.kUninitialized && buffer
				.Length() > 0);
		}

		private void Close()
		{
			buffer = null;
			boundary = null;
		}

		private void ParseContentType()
		{
			StringTokenizer tokenizer = new StringTokenizer(contentType, ";");
			bool first = true;
			while (tokenizer.HasMoreTokens())
			{
				string param = tokenizer.NextToken().Trim();
				if (first == true)
				{
					if (!param.StartsWith("multipart/"))
					{
						throw new ArgumentException(contentType + " does not start with multipart/");
					}
					first = false;
				}
				else
				{
					if (param.StartsWith("boundary="))
					{
						string tempBoundary = Sharpen.Runtime.Substring(param, 9);
						if (tempBoundary.StartsWith("\""))
						{
							if (tempBoundary.Length < 2 || !tempBoundary.EndsWith("\""))
							{
								throw new ArgumentException(contentType + " is not valid");
							}
							tempBoundary = Sharpen.Runtime.Substring(tempBoundary, 1, tempBoundary.Length - 1
								);
						}
						if (tempBoundary.Length < 1)
						{
							throw new ArgumentException(contentType + " has zero-length boundary");
						}
						tempBoundary = string.Format("\r\n--%s", tempBoundary);
						boundary = Sharpen.Runtime.GetBytesForString(tempBoundary, Sharpen.Extensions.GetEncoding
							("UTF-8"));
						break;
					}
				}
			}
		}
	}

	/// <summary>Knuth-Morris-Pratt Algorithm for Pattern Matching</summary>
	internal class KMPMatch
	{
		/// <summary>Finds the first occurrence of the pattern in the text.</summary>
		/// <remarks>Finds the first occurrence of the pattern in the text.</remarks>
		public virtual int IndexOf(byte[] data, byte[] pattern, int dataOffset)
		{
			int[] failure = ComputeFailure(pattern);
			int j = 0;
			if (data.Length == 0)
			{
				return -1;
			}
			int dataLength = data.Length;
			int patternLength = pattern.Length;
			for (int i = dataOffset; i < dataLength; i++)
			{
				while (j > 0 && pattern[j] != data[i])
				{
					j = failure[j - 1];
				}
				if (pattern[j] == data[i])
				{
					j++;
				}
				if (j == patternLength)
				{
					return i - patternLength + 1;
				}
			}
			return -1;
		}

		/// <summary>
		/// Computes the failure function using a boot-strapping process,
		/// where the pattern is matched against itself.
		/// </summary>
		/// <remarks>
		/// Computes the failure function using a boot-strapping process,
		/// where the pattern is matched against itself.
		/// </remarks>
		private int[] ComputeFailure(byte[] pattern)
		{
			int[] failure = new int[pattern.Length];
			int j = 0;
			for (int i = 1; i < pattern.Length; i++)
			{
				while (j > 0 && pattern[j] != pattern[i])
				{
					j = failure[j - 1];
				}
				if (pattern[j] == pattern[i])
				{
					j++;
				}
				failure[i] = j;
			}
			return failure;
		}
	}
}
