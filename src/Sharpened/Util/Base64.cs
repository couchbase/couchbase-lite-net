// 
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
//using System;
using System.IO;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	/// <summary>
	/// Utilities for encoding and decoding the Base64 representation of
	/// binary data.
	/// </summary>
	/// <remarks>
	/// Utilities for encoding and decoding the Base64 representation of
	/// binary data.  See RFCs &lt;a
	/// href="http://www.ietf.org/rfc/rfc2045.txt"&gt;2045</a> and &lt;a
	/// href="http://www.ietf.org/rfc/rfc3548.txt"&gt;3548</a>.
	/// </remarks>
	public class Base64
	{
		/// <summary>Default values for encoder/decoder flags.</summary>
		/// <remarks>Default values for encoder/decoder flags.</remarks>
		public const int Default = 0;

		/// <summary>
		/// Encoder flag bit to omit the padding '=' characters at the end
		/// of the output (if any).
		/// </summary>
		/// <remarks>
		/// Encoder flag bit to omit the padding '=' characters at the end
		/// of the output (if any).
		/// </remarks>
		public const int NoPadding = 1;

		/// <summary>
		/// Encoder flag bit to omit all line terminators (i.e., the output
		/// will be on one long line).
		/// </summary>
		/// <remarks>
		/// Encoder flag bit to omit all line terminators (i.e., the output
		/// will be on one long line).
		/// </remarks>
		public const int NoWrap = 2;

		/// <summary>
		/// Encoder flag bit to indicate lines should be terminated with a
		/// CRLF pair instead of just an LF.
		/// </summary>
		/// <remarks>
		/// Encoder flag bit to indicate lines should be terminated with a
		/// CRLF pair instead of just an LF.  Has no effect if
		/// <code>NO_WRAP</code>
		/// is specified as well.
		/// </remarks>
		public const int Crlf = 4;

		/// <summary>
		/// Encoder/decoder flag bit to indicate using the "URL and
		/// filename safe" variant of Base64 (see RFC 3548 section 4) where
		/// <code>-</code>
		/// and
		/// <code>_</code>
		/// are used in place of
		/// <code>+</code>
		/// and
		/// <code>/</code>
		/// .
		/// </summary>
		public const int UrlSafe = 8;

		/// <summary>
		/// Flag to pass to
		/// <see cref="Android.Util.Base64OutputStream">Android.Util.Base64OutputStream</see>
		/// to indicate that it
		/// should not close the output stream it is wrapping when it
		/// itself is closed.
		/// </summary>
		public const int NoClose = 16;

		internal abstract class Coder
		{
			public byte[] output;

			public int op;

			//  --------------------------------------------------------
			//  shared code
			//  --------------------------------------------------------
			/// <summary>Encode/decode another block of input data.</summary>
			/// <remarks>
			/// Encode/decode another block of input data.  this.output is
			/// provided by the caller, and must be big enough to hold all
			/// the coded data.  On exit, this.opwill be set to the length
			/// of the coded data.
			/// </remarks>
			/// <param name="finish">
			/// true if this is the final call to process for
			/// this object.  Will finalize the coder state and
			/// include any final bytes in the output.
			/// </param>
			/// <returns>
			/// true if the input so far is good; false if some
			/// error has been detected in the input stream..
			/// </returns>
			public abstract bool Process(byte[] input, int offset, int len, bool finish);

			/// <returns>
			/// the maximum number of bytes a call to process()
			/// could produce for the given number of input bytes.  This may
			/// be an overestimate.
			/// </returns>
			public abstract int MaxOutputSize(int len);
		}

		//  --------------------------------------------------------
		//  decoding
		//  --------------------------------------------------------
		/// <summary>
		/// Decode the Base64-encoded data in input and return the data in
		/// a new byte array.
		/// </summary>
		/// <remarks>
		/// Decode the Base64-encoded data in input and return the data in
		/// a new byte array.
		/// <p>The padding '=' characters at the end are considered optional, but
		/// if any are present, there must be the correct number of them.
		/// </remarks>
		/// <param name="str">
		/// the input String to decode, which is converted to
		/// bytes using the default charset
		/// </param>
		/// <param name="flags">
		/// controls certain features of the decoded output.
		/// Pass
		/// <code>DEFAULT</code>
		/// to decode standard Base64.
		/// </param>
		/// <exception cref="System.ArgumentException">
		/// if the input contains
		/// incorrect padding
		/// </exception>
		public static byte[] Decode(string str, int flags)
		{
			return Decode(Sharpen.Runtime.GetBytesForString(str), flags);
		}

		/// <summary>
		/// Decode the Base64-encoded data in input and return the data in
		/// a new byte array.
		/// </summary>
		/// <remarks>
		/// Decode the Base64-encoded data in input and return the data in
		/// a new byte array.
		/// <p>The padding '=' characters at the end are considered optional, but
		/// if any are present, there must be the correct number of them.
		/// </remarks>
		/// <param name="input">the input array to decode</param>
		/// <param name="flags">
		/// controls certain features of the decoded output.
		/// Pass
		/// <code>DEFAULT</code>
		/// to decode standard Base64.
		/// </param>
		/// <exception cref="System.ArgumentException">
		/// if the input contains
		/// incorrect padding
		/// </exception>
		public static byte[] Decode(byte[] input, int flags)
		{
			return Decode(input, 0, input.Length, flags);
		}

		/// <summary>
		/// Decode the Base64-encoded data in input and return the data in
		/// a new byte array.
		/// </summary>
		/// <remarks>
		/// Decode the Base64-encoded data in input and return the data in
		/// a new byte array.
		/// <p>The padding '=' characters at the end are considered optional, but
		/// if any are present, there must be the correct number of them.
		/// </remarks>
		/// <param name="input">the data to decode</param>
		/// <param name="offset">the position within the input array at which to start</param>
		/// <param name="len">the number of bytes of input to decode</param>
		/// <param name="flags">
		/// controls certain features of the decoded output.
		/// Pass
		/// <code>DEFAULT</code>
		/// to decode standard Base64.
		/// </param>
		/// <exception cref="System.ArgumentException">
		/// if the input contains
		/// incorrect padding
		/// </exception>
		public static byte[] Decode(byte[] input, int offset, int len, int flags)
		{
			// Allocate space for the most data the input could represent.
			// (It could contain less if it contains whitespace, etc.)
			Base64.Decoder decoder = new Base64.Decoder(flags, new byte[len * 3 / 4]);
			if (!decoder.Process(input, offset, len, true))
			{
				throw new ArgumentException("bad base-64");
			}
			// Maybe we got lucky and allocated exactly enough output space.
			if (decoder.op == decoder.output.Length)
			{
				return decoder.output;
			}
			// Need to shorten the array, so allocate a new one of the
			// right size and copy.
			byte[] temp = new byte[decoder.op];
			System.Array.Copy(decoder.output, 0, temp, 0, decoder.op);
			return temp;
		}

		internal class Decoder : Base64.Coder
		{
			/// <summary>
			/// Lookup table for turning bytes into their position in the
			/// Base64 alphabet.
			/// </summary>
			/// <remarks>
			/// Lookup table for turning bytes into their position in the
			/// Base64 alphabet.
			/// </remarks>
			private static readonly int Decode = new int[] { -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -1, -1, -1, 63, 
				52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -2, -1, -1, -1, 0, 1, 2, 3, 
				4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
				, -1, -1, -1, -1, -1, -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39
				, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
				, -1, -1, -1, -1, -1 };

			/// <summary>
			/// Decode lookup table for the "web safe" variant (RFC 3548
			/// sec.
			/// </summary>
			/// <remarks>
			/// Decode lookup table for the "web safe" variant (RFC 3548
			/// sec. 4) where - and _ replace + and /.
			/// </remarks>
			private static readonly int DecodeWebsafe = new int[] { -1, -1, -1, -1, -1, -1, -
				1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -
				1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -
				1, -1, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -2, -1, -1, -1, 0, 1, 
				2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 
				24, 25, -1, -1, -1, -1, 63, -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 
				38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 
				-1, -1, -1, -1, -1, -1, -1 };

			/// <summary>Non-data values in the DECODE arrays.</summary>
			/// <remarks>Non-data values in the DECODE arrays.</remarks>
			private const int Skip = -1;

			private const int Equals = -2;

			/// <summary>States 0-3 are reading through the next input tuple.</summary>
			/// <remarks>
			/// States 0-3 are reading through the next input tuple.
			/// State 4 is having read one '=' and expecting exactly
			/// one more.
			/// State 5 is expecting no more data or padding characters
			/// in the input.
			/// State 6 is the error state; an error has been detected
			/// in the input and no future input can "fix" it.
			/// </remarks>
			private int state;

			private int value;

			private readonly int[] alphabet;

			public Decoder(int flags, byte[] output)
			{
				// state number (0 to 6)
				this.output = output;
				alphabet = ((flags & UrlSafe) == 0) ? Decode : DecodeWebsafe;
				state = 0;
				value = 0;
			}

			/// <returns>
			/// an overestimate for the number of bytes
			/// <code>len</code>
			/// bytes could decode to.
			/// </returns>
			public override int MaxOutputSize(int len)
			{
				return len * 3 / 4 + 10;
			}

			/// <summary>Decode another block of input data.</summary>
			/// <remarks>Decode another block of input data.</remarks>
			/// <returns>
			/// true if the state machine is still healthy.  false if
			/// bad base-64 data has been detected in the input stream.
			/// </returns>
			public override bool Process(byte[] input, int offset, int len, bool finish)
			{
				if (this.state == 6)
				{
					return false;
				}
				int p = offset;
				len += offset;
				// Using local variables makes the decoder about 12%
				// faster than if we manipulate the member variables in
				// the loop.  (Even alphabet makes a measurable
				// difference, which is somewhat surprising to me since
				// the member variable is final.)
				int state = this.state;
				int value = this.value;
				int op = 0;
				byte[] output = this.output;
				int[] alphabet = this.alphabet;
				while (p < len)
				{
					// Try the fast path:  we're starting a new tuple and the
					// next four bytes of the input stream are all data
					// bytes.  This corresponds to going through states
					// 0-1-2-3-0.  We expect to use this method for most of
					// the data.
					//
					// If any of the next four bytes of input are non-data
					// (whitespace, etc.), value will end up negative.  (All
					// the non-data values in decode are small negative
					// numbers, so shifting any of them up and or'ing them
					// together will result in a value with its top bit set.)
					//
					// You can remove this whole block and the output should
					// be the same, just slower.
					if (state == 0)
					{
						while (p + 4 <= len && (value = ((alphabet[input[p] & unchecked((int)(0xff))] << 
							18) | (alphabet[input[p + 1] & unchecked((int)(0xff))] << 12) | (alphabet[input[
							p + 2] & unchecked((int)(0xff))] << 6) | (alphabet[input[p + 3] & unchecked((int
							)(0xff))]))) >= 0)
						{
							output[op + 2] = unchecked((byte)value);
							output[op + 1] = unchecked((byte)(value >> 8));
							output[op] = unchecked((byte)(value >> 16));
							op += 3;
							p += 4;
						}
						if (p >= len)
						{
							break;
						}
					}
					// The fast path isn't available -- either we've read a
					// partial tuple, or the next four input bytes aren't all
					// data, or whatever.  Fall back to the slower state
					// machine implementation.
					int d = alphabet[input[p++] & unchecked((int)(0xff))];
					switch (state)
					{
						case 0:
						{
							if (d >= 0)
							{
								value = d;
								++state;
							}
							else
							{
								if (d != Skip)
								{
									this.state = 6;
									return false;
								}
							}
							break;
						}

						case 1:
						{
							if (d >= 0)
							{
								value = (value << 6) | d;
								++state;
							}
							else
							{
								if (d != Skip)
								{
									this.state = 6;
									return false;
								}
							}
							break;
						}

						case 2:
						{
							if (d >= 0)
							{
								value = (value << 6) | d;
								++state;
							}
							else
							{
								if (d == Equals)
								{
									// Emit the last (partial) output tuple;
									// expect exactly one more padding character.
									output[op++] = unchecked((byte)(value >> 4));
									state = 4;
								}
								else
								{
									if (d != Skip)
									{
										this.state = 6;
										return false;
									}
								}
							}
							break;
						}

						case 3:
						{
							if (d >= 0)
							{
								// Emit the output triple and return to state 0.
								value = (value << 6) | d;
								output[op + 2] = unchecked((byte)value);
								output[op + 1] = unchecked((byte)(value >> 8));
								output[op] = unchecked((byte)(value >> 16));
								op += 3;
								state = 0;
							}
							else
							{
								if (d == Equals)
								{
									// Emit the last (partial) output tuple;
									// expect no further data or padding characters.
									output[op + 1] = unchecked((byte)(value >> 2));
									output[op] = unchecked((byte)(value >> 10));
									op += 2;
									state = 5;
								}
								else
								{
									if (d != Skip)
									{
										this.state = 6;
										return false;
									}
								}
							}
							break;
						}

						case 4:
						{
							if (d == Equals)
							{
								++state;
							}
							else
							{
								if (d != Skip)
								{
									this.state = 6;
									return false;
								}
							}
							break;
						}

						case 5:
						{
							if (d != Skip)
							{
								this.state = 6;
								return false;
							}
							break;
						}
					}
				}
				if (!finish)
				{
					// We're out of input, but a future call could provide
					// more.
					this.state = state;
					this.value = value;
					this.op = op;
					return true;
				}
				switch (state)
				{
					case 0:
					{
						// Done reading input.  Now figure out where we are left in
						// the state machine and finish up.
						// Output length is a multiple of three.  Fine.
						break;
					}

					case 1:
					{
						// Read one extra input byte, which isn't enough to
						// make another output byte.  Illegal.
						this.state = 6;
						return false;
					}

					case 2:
					{
						// Read two extra input bytes, enough to emit 1 more
						// output byte.  Fine.
						output[op++] = unchecked((byte)(value >> 4));
						break;
					}

					case 3:
					{
						// Read three extra input bytes, enough to emit 2 more
						// output bytes.  Fine.
						output[op++] = unchecked((byte)(value >> 10));
						output[op++] = unchecked((byte)(value >> 2));
						break;
					}

					case 4:
					{
						// Read one padding '=' when we expected 2.  Illegal.
						this.state = 6;
						return false;
					}

					case 5:
					{
						// Read all the padding '='s we expected and no more.
						// Fine.
						break;
					}
				}
				this.state = state;
				this.op = op;
				return true;
			}
		}

		//  --------------------------------------------------------
		//  encoding
		//  --------------------------------------------------------
		/// <summary>
		/// Base64-encode the given data and return a newly allocated
		/// String with the result.
		/// </summary>
		/// <remarks>
		/// Base64-encode the given data and return a newly allocated
		/// String with the result.
		/// </remarks>
		/// <param name="input">the data to encode</param>
		/// <param name="flags">
		/// controls certain features of the encoded output.
		/// Passing
		/// <code>DEFAULT</code>
		/// results in output that
		/// adheres to RFC 2045.
		/// </param>
		public static string EncodeToString(byte[] input, int flags)
		{
			try
			{
				return Sharpen.Runtime.GetStringForBytes(Encode(input, flags), "US-ASCII");
			}
			catch (UnsupportedEncodingException e)
			{
				// US-ASCII is guaranteed to be available.
				throw new Exception(e);
			}
		}

		/// <summary>
		/// Base64-encode the given data and return a newly allocated
		/// String with the result.
		/// </summary>
		/// <remarks>
		/// Base64-encode the given data and return a newly allocated
		/// String with the result.
		/// </remarks>
		/// <param name="input">the data to encode</param>
		/// <param name="offset">
		/// the position within the input array at which to
		/// start
		/// </param>
		/// <param name="len">the number of bytes of input to encode</param>
		/// <param name="flags">
		/// controls certain features of the encoded output.
		/// Passing
		/// <code>DEFAULT</code>
		/// results in output that
		/// adheres to RFC 2045.
		/// </param>
		public static string EncodeToString(byte[] input, int offset, int len, int flags)
		{
			try
			{
				return Sharpen.Runtime.GetStringForBytes(Encode(input, offset, len, flags), "US-ASCII"
					);
			}
			catch (UnsupportedEncodingException e)
			{
				// US-ASCII is guaranteed to be available.
				throw new Exception(e);
			}
		}

		/// <summary>
		/// Base64-encode the given data and return a newly allocated
		/// byte[] with the result.
		/// </summary>
		/// <remarks>
		/// Base64-encode the given data and return a newly allocated
		/// byte[] with the result.
		/// </remarks>
		/// <param name="input">the data to encode</param>
		/// <param name="flags">
		/// controls certain features of the encoded output.
		/// Passing
		/// <code>DEFAULT</code>
		/// results in output that
		/// adheres to RFC 2045.
		/// </param>
		public static byte[] Encode(byte[] input, int flags)
		{
			return Encode(input, 0, input.Length, flags);
		}

		/// <summary>
		/// Base64-encode the given data and return a newly allocated
		/// byte[] with the result.
		/// </summary>
		/// <remarks>
		/// Base64-encode the given data and return a newly allocated
		/// byte[] with the result.
		/// </remarks>
		/// <param name="input">the data to encode</param>
		/// <param name="offset">
		/// the position within the input array at which to
		/// start
		/// </param>
		/// <param name="len">the number of bytes of input to encode</param>
		/// <param name="flags">
		/// controls certain features of the encoded output.
		/// Passing
		/// <code>DEFAULT</code>
		/// results in output that
		/// adheres to RFC 2045.
		/// </param>
		public static byte[] Encode(byte[] input, int offset, int len, int flags)
		{
			Base64.Encoder encoder = new Base64.Encoder(flags, null);
			// Compute the exact length of the array we will produce.
			int output_len = len / 3 * 4;
			// Account for the tail of the data and the padding bytes, if any.
			if (encoder.do_padding)
			{
				if (len % 3 > 0)
				{
					output_len += 4;
				}
			}
			else
			{
				switch (len % 3)
				{
					case 0:
					{
						break;
					}

					case 1:
					{
						output_len += 2;
						break;
					}

					case 2:
					{
						output_len += 3;
						break;
					}
				}
			}
			// Account for the newlines, if any.
			if (encoder.do_newline && len > 0)
			{
				output_len += (((len - 1) / (3 * Base64.Encoder.LineGroups)) + 1) * (encoder.do_cr
					 ? 2 : 1);
			}
			encoder.output = new byte[output_len];
			encoder.Process(input, offset, len, true);
			System.Diagnostics.Debug.Assert(encoder.op == output_len);
			return encoder.output;
		}

		internal class Encoder : Base64.Coder
		{
			/// <summary>Emit a new line every this many output tuples.</summary>
			/// <remarks>
			/// Emit a new line every this many output tuples.  Corresponds to
			/// a 76-character line length (the maximum allowable according to
			/// <a href="http://www.ietf.org/rfc/rfc2045.txt">RFC 2045</a>).
			/// </remarks>
			public const int LineGroups = 19;

			/// <summary>
			/// Lookup table for turning Base64 alphabet positions (6 bits)
			/// into output bytes.
			/// </summary>
			/// <remarks>
			/// Lookup table for turning Base64 alphabet positions (6 bits)
			/// into output bytes.
			/// </remarks>
			private static readonly byte Encode = new byte[] { (byte)('A'), (byte)('B'), (byte
				)('C'), (byte)('D'), (byte)('E'), (byte)('F'), (byte)('G'), (byte)('H'), (byte)(
				'I'), (byte)('J'), (byte)('K'), (byte)('L'), (byte)('M'), (byte)('N'), (byte)('O'
				), (byte)('P'), (byte)('Q'), (byte)('R'), (byte)('S'), (byte)('T'), (byte)('U'), 
				(byte)('V'), (byte)('W'), (byte)('X'), (byte)('Y'), (byte)('Z'), (byte)('a'), (byte
				)('b'), (byte)('c'), (byte)('d'), (byte)('e'), (byte)('f'), (byte)('g'), (byte)(
				'h'), (byte)('i'), (byte)('j'), (byte)('k'), (byte)('l'), (byte)('m'), (byte)('n'
				), (byte)('o'), (byte)('p'), (byte)('q'), (byte)('r'), (byte)('s'), (byte)('t'), 
				(byte)('u'), (byte)('v'), (byte)('w'), (byte)('x'), (byte)('y'), (byte)('z'), (byte
				)('0'), (byte)('1'), (byte)('2'), (byte)('3'), (byte)('4'), (byte)('5'), (byte)(
				'6'), (byte)('7'), (byte)('8'), (byte)('9'), (byte)('+'), (byte)('/') };

			/// <summary>
			/// Lookup table for turning Base64 alphabet positions (6 bits)
			/// into output bytes.
			/// </summary>
			/// <remarks>
			/// Lookup table for turning Base64 alphabet positions (6 bits)
			/// into output bytes.
			/// </remarks>
			private static readonly byte EncodeWebsafe = new byte[] { (byte)('A'), (byte)('B'
				), (byte)('C'), (byte)('D'), (byte)('E'), (byte)('F'), (byte)('G'), (byte)('H'), 
				(byte)('I'), (byte)('J'), (byte)('K'), (byte)('L'), (byte)('M'), (byte)('N'), (byte
				)('O'), (byte)('P'), (byte)('Q'), (byte)('R'), (byte)('S'), (byte)('T'), (byte)(
				'U'), (byte)('V'), (byte)('W'), (byte)('X'), (byte)('Y'), (byte)('Z'), (byte)('a'
				), (byte)('b'), (byte)('c'), (byte)('d'), (byte)('e'), (byte)('f'), (byte)('g'), 
				(byte)('h'), (byte)('i'), (byte)('j'), (byte)('k'), (byte)('l'), (byte)('m'), (byte
				)('n'), (byte)('o'), (byte)('p'), (byte)('q'), (byte)('r'), (byte)('s'), (byte)(
				't'), (byte)('u'), (byte)('v'), (byte)('w'), (byte)('x'), (byte)('y'), (byte)('z'
				), (byte)('0'), (byte)('1'), (byte)('2'), (byte)('3'), (byte)('4'), (byte)('5'), 
				(byte)('6'), (byte)('7'), (byte)('8'), (byte)('9'), (byte)('-'), (byte)('_') };

			private readonly byte[] tail;

			internal int tailLen;

			private int count;

			public readonly bool do_padding;

			public readonly bool do_newline;

			public readonly bool do_cr;

			private readonly byte[] alphabet;

			public Encoder(int flags, byte[] output)
			{
				this.output = output;
				do_padding = (flags & NoPadding) == 0;
				do_newline = (flags & NoWrap) == 0;
				do_cr = (flags & Crlf) != 0;
				alphabet = ((flags & UrlSafe) == 0) ? Encode : EncodeWebsafe;
				tail = new byte[2];
				tailLen = 0;
				count = do_newline ? LineGroups : -1;
			}

			/// <returns>
			/// an overestimate for the number of bytes
			/// <code>len</code>
			/// bytes could encode to.
			/// </returns>
			public override int MaxOutputSize(int len)
			{
				return len * 8 / 5 + 10;
			}

			public override bool Process(byte[] input, int offset, int len, bool finish)
			{
				// Using local variables makes the encoder about 9% faster.
				byte[] alphabet = this.alphabet;
				byte[] output = this.output;
				int op = 0;
				int count = this.count;
				int p = offset;
				len += offset;
				int v = -1;
				switch (tailLen)
				{
					case 0:
					{
						// First we need to concatenate the tail of the previous call
						// with any input bytes available now and see if we can empty
						// the tail.
						// There was no tail.
						break;
					}

					case 1:
					{
						if (p + 2 <= len)
						{
							// A 1-byte tail with at least 2 bytes of
							// input available now.
							v = ((tail[0] & unchecked((int)(0xff))) << 16) | ((input[p++] & unchecked((int)(0xff
								))) << 8) | (input[p++] & unchecked((int)(0xff)));
							tailLen = 0;
						}
						break;
					}

					case 2:
					{
						if (p + 1 <= len)
						{
							// A 2-byte tail with at least 1 byte of input.
							v = ((tail[0] & unchecked((int)(0xff))) << 16) | ((tail[1] & unchecked((int)(0xff
								))) << 8) | (input[p++] & unchecked((int)(0xff)));
							tailLen = 0;
						}
						break;
					}
				}
				if (v != -1)
				{
					output[op++] = alphabet[(v >> 18) & unchecked((int)(0x3f))];
					output[op++] = alphabet[(v >> 12) & unchecked((int)(0x3f))];
					output[op++] = alphabet[(v >> 6) & unchecked((int)(0x3f))];
					output[op++] = alphabet[v & unchecked((int)(0x3f))];
					if (--count == 0)
					{
						if (do_cr)
						{
							output[op++] = (byte)('\r');
						}
						output[op++] = (byte)('\n');
						count = LineGroups;
					}
				}
				// At this point either there is no tail, or there are fewer
				// than 3 bytes of input available.
				// The main loop, turning 3 input bytes into 4 output bytes on
				// each iteration.
				while (p + 3 <= len)
				{
					v = ((input[p] & unchecked((int)(0xff))) << 16) | ((input[p + 1] & unchecked((int
						)(0xff))) << 8) | (input[p + 2] & unchecked((int)(0xff)));
					output[op] = alphabet[(v >> 18) & unchecked((int)(0x3f))];
					output[op + 1] = alphabet[(v >> 12) & unchecked((int)(0x3f))];
					output[op + 2] = alphabet[(v >> 6) & unchecked((int)(0x3f))];
					output[op + 3] = alphabet[v & unchecked((int)(0x3f))];
					p += 3;
					op += 4;
					if (--count == 0)
					{
						if (do_cr)
						{
							output[op++] = (byte)('\r');
						}
						output[op++] = (byte)('\n');
						count = LineGroups;
					}
				}
				if (finish)
				{
					// Finish up the tail of the input.  Note that we need to
					// consume any bytes in tail before any bytes
					// remaining in input; there should be at most two bytes
					// total.
					if (p - tailLen == len - 1)
					{
						int t = 0;
						v = ((tailLen > 0 ? tail[t++] : input[p++]) & unchecked((int)(0xff))) << 4;
						tailLen -= t;
						output[op++] = alphabet[(v >> 6) & unchecked((int)(0x3f))];
						output[op++] = alphabet[v & unchecked((int)(0x3f))];
						if (do_padding)
						{
							output[op++] = (byte)('=');
							output[op++] = (byte)('=');
						}
						if (do_newline)
						{
							if (do_cr)
							{
								output[op++] = (byte)('\r');
							}
							output[op++] = (byte)('\n');
						}
					}
					else
					{
						if (p - tailLen == len - 2)
						{
							int t = 0;
							v = (((tailLen > 1 ? tail[t++] : input[p++]) & unchecked((int)(0xff))) << 10) | (
								((tailLen > 0 ? tail[t++] : input[p++]) & unchecked((int)(0xff))) << 2);
							tailLen -= t;
							output[op++] = alphabet[(v >> 12) & unchecked((int)(0x3f))];
							output[op++] = alphabet[(v >> 6) & unchecked((int)(0x3f))];
							output[op++] = alphabet[v & unchecked((int)(0x3f))];
							if (do_padding)
							{
								output[op++] = (byte)('=');
							}
							if (do_newline)
							{
								if (do_cr)
								{
									output[op++] = (byte)('\r');
								}
								output[op++] = (byte)('\n');
							}
						}
						else
						{
							if (do_newline && op > 0 && count != LineGroups)
							{
								if (do_cr)
								{
									output[op++] = (byte)('\r');
								}
								output[op++] = (byte)('\n');
							}
						}
					}
					System.Diagnostics.Debug.Assert(tailLen == 0);
					System.Diagnostics.Debug.Assert(p == len);
				}
				else
				{
					// Save the leftovers in tail to be consumed on the next
					// call to encodeInternal.
					if (p == len - 1)
					{
						tail[tailLen++] = input[p];
					}
					else
					{
						if (p == len - 2)
						{
							tail[tailLen++] = input[p];
							tail[tailLen++] = input[p + 1];
						}
					}
				}
				this.op = op;
				this.count = count;
				return true;
			}
		}

		private Base64()
		{
		}
		// don't instantiate
	}
}
