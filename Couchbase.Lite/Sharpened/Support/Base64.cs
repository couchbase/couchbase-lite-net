/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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
using System.IO;
using Couchbase.Lite.Support;
using Sharpen;
using System.Runtime.Serialization;

namespace Couchbase.Lite.Support
{
	/// <summary>
	/// <p>Encodes and decodes to and from Base64 notation.</p>
	/// <p>Homepage: <a href="http://iharder.net/base64">http://iharder.net/base64</a>.</p>
	/// <p>Example:</p>
	/// <code>String encoded = Base64.encode( myByteArray );</code>
	/// <br />
	/// <code>byte[] myByteArray = Base64.decode( encoded );</code>
	/// <p>The <tt>options</tt> parameter, which appears in a few places, is used to pass
	/// several pieces of information to the encoder.
	/// </summary>
	/// <remarks>
	/// <p>Encodes and decodes to and from Base64 notation.</p>
	/// <p>Homepage: <a href="http://iharder.net/base64">http://iharder.net/base64</a>.</p>
	/// <p>Example:</p>
	/// <code>String encoded = Base64.encode( myByteArray );</code>
	/// <br />
	/// <code>byte[] myByteArray = Base64.decode( encoded );</code>
	/// <p>The <tt>options</tt> parameter, which appears in a few places, is used to pass
	/// several pieces of information to the encoder. In the "higher level" methods such as
	/// encodeBytes( bytes, options ) the options parameter can be used to indicate such
	/// things as first gzipping the bytes before encoding them, not inserting linefeeds,
	/// and encoding using the URL-safe and Ordered dialects.</p>
	/// <p>Note, according to <a href="http://www.faqs.org/rfcs/rfc3548.html">RFC3548</a>,
	/// Section 2.1, implementations should not add line feeds unless explicitly told
	/// to do so. I've got Base64 set to this behavior now, although earlier versions
	/// broke lines by default.</p>
	/// <p>The constants defined in Base64 can be OR-ed together to combine options, so you
	/// might make a call like this:</p>
	/// <code>String encoded = Base64.encodeBytes( mybytes, Base64.GZIP | Base64.DO_BREAK_LINES );</code>
	/// <p>to compress the data before encoding it and then making the output have newline characters.</p>
	/// <p>Also...</p>
	/// <code>String encoded = Base64.encodeBytes( crazyString.getBytes() );</code>
	/// <p>
	/// Change Log:
	/// </p>
	/// <ul>
	/// <li>v2.3.7 - Fixed subtle bug when base 64 input stream contained the
	/// value 01111111, which is an invalid base 64 character but should not
	/// throw an ArrayIndexOutOfBoundsException either. Led to discovery of
	/// mishandling (or potential for better handling) of other bad input
	/// characters. You should now get an IOException if you try decoding
	/// something that has bad characters in it.</li>
	/// <li>v2.3.6 - Fixed bug when breaking lines and the final byte of the encoded
	/// string ended in the last column; the buffer was not properly shrunk and
	/// contained an extra (null) byte that made it into the string.</li>
	/// <li>v2.3.5 - Fixed bug in
	/// <see cref="EncodeFromFile(string)">EncodeFromFile(string)</see>
	/// where estimated buffer size
	/// was wrong for files of size 31, 34, and 37 bytes.</li>
	/// <li>v2.3.4 - Fixed bug when working with gzipped streams whereby flushing
	/// the Base64.OutputStream closed the Base64 encoding (by padding with equals
	/// signs) too soon. Also added an option to suppress the automatic decoding
	/// of gzipped streams. Also added experimental support for specifying a
	/// class loader when using the
	/// <see cref="DecodeToObject(string, int, Sharpen.ClassLoader)">DecodeToObject(string, int, Sharpen.ClassLoader)
	/// 	</see>
	/// method.</li>
	/// <li>v2.3.3 - Changed default char encoding to US-ASCII which reduces the internal Java
	/// footprint with its CharEncoders and so forth. Fixed some javadocs that were
	/// inconsistent. Removed imports and specified things like java.io.IOException
	/// explicitly inline.</li>
	/// <li>v2.3.2 - Reduced memory footprint! Finally refined the "guessing" of how big the
	/// final encoded data will be so that the code doesn't have to create two output
	/// arrays: an oversized initial one and then a final, exact-sized one. Big win
	/// when using the
	/// <see cref="EncodeBytesToBytes(byte[])">EncodeBytesToBytes(byte[])</see>
	/// family of methods (and not
	/// using the gzip options which uses a different mechanism with streams and stuff).</li>
	/// <li>v2.3.1 - Added
	/// <see cref="EncodeBytesToBytes(byte[], int, int, int)">EncodeBytesToBytes(byte[], int, int, int)
	/// 	</see>
	/// and some
	/// similar helper methods to be more efficient with memory by not returning a
	/// String but just a byte array.</li>
	/// <li>v2.3 - <strong>This is not a drop-in replacement!</strong> This is two years of comments
	/// and bug fixes queued up and finally executed. Thanks to everyone who sent
	/// me stuff, and I'm sorry I wasn't able to distribute your fixes to everyone else.
	/// Much bad coding was cleaned up including throwing exceptions where necessary
	/// instead of returning null values or something similar. Here are some changes
	/// that may affect you:
	/// <ul>
	/// <li><em>Does not break lines, by default.</em> This is to keep in compliance with
	/// <a href="http://www.faqs.org/rfcs/rfc3548.html">RFC3548</a>.</li>
	/// <li><em>Throws exceptions instead of returning null values.</em> Because some operations
	/// (especially those that may permit the GZIP option) use IO streams, there
	/// is a possiblity of an java.io.IOException being thrown. After some discussion and
	/// thought, I've changed the behavior of the methods to throw java.io.IOExceptions
	/// rather than return null if ever there's an error. I think this is more
	/// appropriate, though it will require some changes to your code. Sorry,
	/// it should have been done this way to begin with.</li>
	/// <li><em>Removed all references to System.out, System.err, and the like.</em>
	/// Shame on me. All I can say is sorry they were ever there.</li>
	/// <li><em>Throws NullPointerExceptions and IllegalArgumentExceptions</em> as needed
	/// such as when passed arrays are null or offsets are invalid.</li>
	/// <li>Cleaned up as much javadoc as I could to avoid any javadoc warnings.
	/// This was especially annoying before for people who were thorough in their
	/// own projects and then had gobs of javadoc warnings on this file.</li>
	/// </ul>
	/// <li>v2.2.1 - Fixed bug using URL_SAFE and ORDERED encodings. Fixed bug
	/// when using very small files (~&lt; 40 bytes).</li>
	/// <li>v2.2 - Added some helper methods for encoding/decoding directly from
	/// one file to the next. Also added a main() method to support command line
	/// encoding/decoding from one file to the next. Also added these Base64 dialects:
	/// <ol>
	/// <li>The default is RFC3548 format.</li>
	/// <li>Calling Base64.setFormat(Base64.BASE64_FORMAT.URLSAFE_FORMAT) generates
	/// URL and file name friendly format as described in Section 4 of RFC3548.
	/// http://www.faqs.org/rfcs/rfc3548.html</li>
	/// <li>Calling Base64.setFormat(Base64.BASE64_FORMAT.ORDERED_FORMAT) generates
	/// URL and file name friendly format that preserves lexical ordering as described
	/// in http://www.faqs.org/qa/rfcc-1940.html</li>
	/// </ol>
	/// Special thanks to Jim Kellerman at <a href="http://www.powerset.com/">http://www.powerset.com/</a>
	/// for contributing the new Base64 dialects.
	/// </li>
	/// <li>v2.1 - Cleaned up javadoc comments and unused variables and methods. Added
	/// some convenience methods for reading and writing to and from files.</li>
	/// <li>v2.0.2 - Now specifies UTF-8 encoding in places where the code fails on systems
	/// with other encodings (like EBCDIC).</li>
	/// <li>v2.0.1 - Fixed an error when decoding a single byte, that is, when the
	/// encoded data was a single byte.</li>
	/// <li>v2.0 - I got rid of methods that used booleans to set options.
	/// Now everything is more consolidated and cleaner. The code now detects
	/// when data that's being decoded is gzip-compressed and will decompress it
	/// automatically. Generally things are cleaner. You'll probably have to
	/// change some method calls that you were making to support the new
	/// options format (<tt>int</tt>s that you "OR" together).</li>
	/// <li>v1.5.1 - Fixed bug when decompressing and decoding to a
	/// byte[] using <tt>decode( String s, boolean gzipCompressed )</tt>.
	/// Added the ability to "suspend" encoding in the Output Stream so
	/// you can turn on and off the encoding if you need to embed base64
	/// data in an otherwise "normal" stream (like an XML file).</li>
	/// <li>v1.5 - Output stream pases on flush() command but doesn't do anything itself.
	/// This helps when using GZIP streams.
	/// Added the ability to GZip-compress objects before encoding them.</li>
	/// <li>v1.4 - Added helper methods to read/write files.</li>
	/// <li>v1.3.6 - Fixed OutputStream.flush() so that 'position' is reset.</li>
	/// <li>v1.3.5 - Added flag to turn on and off line breaks. Fixed bug in input stream
	/// where last buffer being read, if not completely full, was not returned.</li>
	/// <li>v1.3.4 - Fixed when "improperly padded stream" error was thrown at the wrong time.</li>
	/// <li>v1.3.3 - Fixed I/O streams which were totally messed up.</li>
	/// </ul>
	/// <p>
	/// I am placing this code in the Public Domain. Do with it as you will.
	/// This software comes with no guarantees or warranties but with
	/// plenty of well-wishing instead!
	/// Please visit <a href="http://iharder.net/base64">http://iharder.net/base64</a>
	/// periodically to check for updates or to contribute improvements.
	/// </p>
	/// </remarks>
	/// <author>Robert Harder</author>
	/// <author>rob@iharder.net</author>
	/// <version>2.3.7</version>
	public class Base64
	{
		/// <summary>No options specified.</summary>
		/// <remarks>No options specified. Value is zero.</remarks>
		public const int NoOptions = 0;

		/// <summary>Specify encoding in first bit.</summary>
		/// <remarks>Specify encoding in first bit. Value is one.</remarks>
        public const int FirstBitEncoding = 1;

		/// <summary>Specify decoding in first bit.</summary>
		/// <remarks>Specify decoding in first bit. Value is zero.</remarks>
        public const int FirstBitDecoding = 0;

		/// <summary>Specify that data should be gzip-compressed in second bit.</summary>
		/// <remarks>Specify that data should be gzip-compressed in second bit. Value is two.
		/// 	</remarks>
		public const int Gzip = 2;

		/// <summary>Specify that gzipped data should <em>not</em> be automatically gunzipped.
		/// 	</summary>
		/// <remarks>Specify that gzipped data should <em>not</em> be automatically gunzipped.
		/// 	</remarks>
		public const int DontGunzip = 4;

		/// <summary>Do break lines when encoding.</summary>
		/// <remarks>Do break lines when encoding. Value is 8.</remarks>
		public const int DoBreakLines = 8;

		/// <summary>
		/// Encode using Base64-like encoding that is URL- and Filename-safe as described
		/// in Section 4 of RFC3548:
		/// <a href="http://www.faqs.org/rfcs/rfc3548.html">http://www.faqs.org/rfcs/rfc3548.html</a>.
		/// </summary>
		/// <remarks>
		/// Encode using Base64-like encoding that is URL- and Filename-safe as described
		/// in Section 4 of RFC3548:
		/// <a href="http://www.faqs.org/rfcs/rfc3548.html">http://www.faqs.org/rfcs/rfc3548.html</a>.
		/// It is important to note that data encoded this way is <em>not</em> officially valid Base64,
		/// or at the very least should not be called Base64 without also specifying that is
		/// was encoded using the URL- and Filename-safe dialect.
		/// </remarks>
		public const int UrlSafe = 16;

		/// <summary>
		/// Encode using the special "ordered" dialect of Base64 described here:
		/// <a href="http://www.faqs.org/qa/rfcc-1940.html">http://www.faqs.org/qa/rfcc-1940.html</a>.
		/// </summary>
		/// <remarks>
		/// Encode using the special "ordered" dialect of Base64 described here:
		/// <a href="http://www.faqs.org/qa/rfcc-1940.html">http://www.faqs.org/qa/rfcc-1940.html</a>.
		/// </remarks>
		public const int Ordered = 32;

		/// <summary>Maximum line length (76) of Base64 output.</summary>
		/// <remarks>Maximum line length (76) of Base64 output.</remarks>
		private const int MaxLineLength = 76;

		/// <summary>The equals sign (=) as a byte.</summary>
		/// <remarks>The equals sign (=) as a byte.</remarks>
		private const byte EqualsSign = unchecked((byte)(byte)('='));

		/// <summary>The new line character (\n) as a byte.</summary>
		/// <remarks>The new line character (\n) as a byte.</remarks>
		private const byte NewLine = unchecked((byte)(byte)('\n'));

		/// <summary>Preferred encoding.</summary>
		/// <remarks>Preferred encoding.</remarks>
		private const string PreferredEncoding = "US-ASCII";

		private const byte WhiteSpaceEnc = unchecked((byte)(-5));

		private const byte EqualsSignEnc = unchecked((byte)(-1));

		/// <summary>The 64 valid Base64 values.</summary>
		/// <remarks>The 64 valid Base64 values.</remarks>
		private static readonly byte[] StandardAlphabet = new byte[] { unchecked((byte)(byte
			)('A')), unchecked((byte)(byte)('B')), unchecked((byte)(byte)('C')), unchecked((
			byte)(byte)('D')), unchecked((byte)(byte)('E')), unchecked((byte)(byte)('F')), unchecked(
			(byte)(byte)('G')), unchecked((byte)(byte)('H')), unchecked((byte)(byte)('I')), 
			unchecked((byte)(byte)('J')), unchecked((byte)(byte)('K')), unchecked((byte)(byte
			)('L')), unchecked((byte)(byte)('M')), unchecked((byte)(byte)('N')), unchecked((
			byte)(byte)('O')), unchecked((byte)(byte)('P')), unchecked((byte)(byte)('Q')), unchecked(
			(byte)(byte)('R')), unchecked((byte)(byte)('S')), unchecked((byte)(byte)('T')), 
			unchecked((byte)(byte)('U')), unchecked((byte)(byte)('V')), unchecked((byte)(byte
			)('W')), unchecked((byte)(byte)('X')), unchecked((byte)(byte)('Y')), unchecked((
			byte)(byte)('Z')), unchecked((byte)(byte)('a')), unchecked((byte)(byte)('b')), unchecked(
			(byte)(byte)('c')), unchecked((byte)(byte)('d')), unchecked((byte)(byte)('e')), 
			unchecked((byte)(byte)('f')), unchecked((byte)(byte)('g')), unchecked((byte)(byte
			)('h')), unchecked((byte)(byte)('i')), unchecked((byte)(byte)('j')), unchecked((
			byte)(byte)('k')), unchecked((byte)(byte)('l')), unchecked((byte)(byte)('m')), unchecked(
			(byte)(byte)('n')), unchecked((byte)(byte)('o')), unchecked((byte)(byte)('p')), 
			unchecked((byte)(byte)('q')), unchecked((byte)(byte)('r')), unchecked((byte)(byte
			)('s')), unchecked((byte)(byte)('t')), unchecked((byte)(byte)('u')), unchecked((
			byte)(byte)('v')), unchecked((byte)(byte)('w')), unchecked((byte)(byte)('x')), unchecked(
			(byte)(byte)('y')), unchecked((byte)(byte)('z')), unchecked((byte)(byte)('0')), 
			unchecked((byte)(byte)('1')), unchecked((byte)(byte)('2')), unchecked((byte)(byte
			)('3')), unchecked((byte)(byte)('4')), unchecked((byte)(byte)('5')), unchecked((
			byte)(byte)('6')), unchecked((byte)(byte)('7')), unchecked((byte)(byte)('8')), unchecked(
			(byte)(byte)('9')), unchecked((byte)(byte)('+')), unchecked((byte)(byte)('/')) };

		/// <summary>
		/// Translates a Base64 value to either its 6-bit reconstruction value
		/// or a negative number indicating some other meaning.
		/// </summary>
		/// <remarks>
		/// Translates a Base64 value to either its 6-bit reconstruction value
		/// or a negative number indicating some other meaning.
		/// </remarks>
		private static readonly byte[] StandardDecodabet = new byte[] { unchecked((byte)(
			-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-5)), unchecked((byte)(-5)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-5)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-5)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, 62, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), 63, 52
			, 53, 54, 55, 56, 57, 58, 59, 60, 61, unchecked((byte)(-9)), unchecked((byte)(-9
			)), unchecked((byte)(-9)), unchecked((byte)(-1)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13
			, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 
			40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)) };

		/// <summary>
		/// Used in the URL- and Filename-safe dialect described in Section 4 of RFC3548:
		/// <a href="http://www.faqs.org/rfcs/rfc3548.html">http://www.faqs.org/rfcs/rfc3548.html</a>.
		/// </summary>
		/// <remarks>
		/// Used in the URL- and Filename-safe dialect described in Section 4 of RFC3548:
		/// <a href="http://www.faqs.org/rfcs/rfc3548.html">http://www.faqs.org/rfcs/rfc3548.html</a>.
		/// Notice that the last two bytes become "hyphen" and "underscore" instead of "plus" and "slash."
		/// </remarks>
		private static readonly byte[] UrlSafeAlphabet = new byte[] { unchecked((byte)(byte
			)('A')), unchecked((byte)(byte)('B')), unchecked((byte)(byte)('C')), unchecked((
			byte)(byte)('D')), unchecked((byte)(byte)('E')), unchecked((byte)(byte)('F')), unchecked(
			(byte)(byte)('G')), unchecked((byte)(byte)('H')), unchecked((byte)(byte)('I')), 
			unchecked((byte)(byte)('J')), unchecked((byte)(byte)('K')), unchecked((byte)(byte
			)('L')), unchecked((byte)(byte)('M')), unchecked((byte)(byte)('N')), unchecked((
			byte)(byte)('O')), unchecked((byte)(byte)('P')), unchecked((byte)(byte)('Q')), unchecked(
			(byte)(byte)('R')), unchecked((byte)(byte)('S')), unchecked((byte)(byte)('T')), 
			unchecked((byte)(byte)('U')), unchecked((byte)(byte)('V')), unchecked((byte)(byte
			)('W')), unchecked((byte)(byte)('X')), unchecked((byte)(byte)('Y')), unchecked((
			byte)(byte)('Z')), unchecked((byte)(byte)('a')), unchecked((byte)(byte)('b')), unchecked(
			(byte)(byte)('c')), unchecked((byte)(byte)('d')), unchecked((byte)(byte)('e')), 
			unchecked((byte)(byte)('f')), unchecked((byte)(byte)('g')), unchecked((byte)(byte
			)('h')), unchecked((byte)(byte)('i')), unchecked((byte)(byte)('j')), unchecked((
			byte)(byte)('k')), unchecked((byte)(byte)('l')), unchecked((byte)(byte)('m')), unchecked(
			(byte)(byte)('n')), unchecked((byte)(byte)('o')), unchecked((byte)(byte)('p')), 
			unchecked((byte)(byte)('q')), unchecked((byte)(byte)('r')), unchecked((byte)(byte
			)('s')), unchecked((byte)(byte)('t')), unchecked((byte)(byte)('u')), unchecked((
			byte)(byte)('v')), unchecked((byte)(byte)('w')), unchecked((byte)(byte)('x')), unchecked(
			(byte)(byte)('y')), unchecked((byte)(byte)('z')), unchecked((byte)(byte)('0')), 
			unchecked((byte)(byte)('1')), unchecked((byte)(byte)('2')), unchecked((byte)(byte
			)('3')), unchecked((byte)(byte)('4')), unchecked((byte)(byte)('5')), unchecked((
			byte)(byte)('6')), unchecked((byte)(byte)('7')), unchecked((byte)(byte)('8')), unchecked(
			(byte)(byte)('9')), unchecked((byte)(byte)('-')), unchecked((byte)(byte)('_')) };

		/// <summary>Used in decoding URL- and Filename-safe dialects of Base64.</summary>
		/// <remarks>Used in decoding URL- and Filename-safe dialects of Base64.</remarks>
		private static readonly byte[] UrlSafeDecodabet = new byte[] { unchecked((byte)(-
			9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-5)), unchecked((byte)(-5)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-5)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-5)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), 62, unchecked((byte)(-9)), unchecked(
			(byte)(-9)), 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-1)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
			, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, unchecked((byte)(-
			9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), 63, unchecked(
			(byte)(-9)), 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 
			43, 44, 45, 46, 47, 48, 49, 50, 51, unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)) };

		/// <summary>
		/// I don't get the point of this technique, but someone requested it,
		/// and it is described here:
		/// <a href="http://www.faqs.org/qa/rfcc-1940.html">http://www.faqs.org/qa/rfcc-1940.html</a>.
		/// </summary>
		/// <remarks>
		/// I don't get the point of this technique, but someone requested it,
		/// and it is described here:
		/// <a href="http://www.faqs.org/qa/rfcc-1940.html">http://www.faqs.org/qa/rfcc-1940.html</a>.
		/// </remarks>
		private static readonly byte[] OrderedAlphabet = new byte[] { unchecked((byte)(byte
			)('-')), unchecked((byte)(byte)('0')), unchecked((byte)(byte)('1')), unchecked((
			byte)(byte)('2')), unchecked((byte)(byte)('3')), unchecked((byte)(byte)('4')), unchecked(
			(byte)(byte)('5')), unchecked((byte)(byte)('6')), unchecked((byte)(byte)('7')), 
			unchecked((byte)(byte)('8')), unchecked((byte)(byte)('9')), unchecked((byte)(byte
			)('A')), unchecked((byte)(byte)('B')), unchecked((byte)(byte)('C')), unchecked((
			byte)(byte)('D')), unchecked((byte)(byte)('E')), unchecked((byte)(byte)('F')), unchecked(
			(byte)(byte)('G')), unchecked((byte)(byte)('H')), unchecked((byte)(byte)('I')), 
			unchecked((byte)(byte)('J')), unchecked((byte)(byte)('K')), unchecked((byte)(byte
			)('L')), unchecked((byte)(byte)('M')), unchecked((byte)(byte)('N')), unchecked((
			byte)(byte)('O')), unchecked((byte)(byte)('P')), unchecked((byte)(byte)('Q')), unchecked(
			(byte)(byte)('R')), unchecked((byte)(byte)('S')), unchecked((byte)(byte)('T')), 
			unchecked((byte)(byte)('U')), unchecked((byte)(byte)('V')), unchecked((byte)(byte
			)('W')), unchecked((byte)(byte)('X')), unchecked((byte)(byte)('Y')), unchecked((
			byte)(byte)('Z')), unchecked((byte)(byte)('_')), unchecked((byte)(byte)('a')), unchecked(
			(byte)(byte)('b')), unchecked((byte)(byte)('c')), unchecked((byte)(byte)('d')), 
			unchecked((byte)(byte)('e')), unchecked((byte)(byte)('f')), unchecked((byte)(byte
			)('g')), unchecked((byte)(byte)('h')), unchecked((byte)(byte)('i')), unchecked((
			byte)(byte)('j')), unchecked((byte)(byte)('k')), unchecked((byte)(byte)('l')), unchecked(
			(byte)(byte)('m')), unchecked((byte)(byte)('n')), unchecked((byte)(byte)('o')), 
			unchecked((byte)(byte)('p')), unchecked((byte)(byte)('q')), unchecked((byte)(byte
			)('r')), unchecked((byte)(byte)('s')), unchecked((byte)(byte)('t')), unchecked((
			byte)(byte)('u')), unchecked((byte)(byte)('v')), unchecked((byte)(byte)('w')), unchecked(
			(byte)(byte)('x')), unchecked((byte)(byte)('y')), unchecked((byte)(byte)('z')) };

		/// <summary>Used in decoding the "ordered" dialect of Base64.</summary>
		/// <remarks>Used in decoding the "ordered" dialect of Base64.</remarks>
		private static readonly byte[] OrderedDecodabet = new byte[] { unchecked((byte)(-
			9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-5)), unchecked((byte)(-5)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-5)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-5)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), 0, unchecked((byte)(-9)), unchecked(
			(byte)(-9)), 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, unchecked((byte)(-9)), unchecked((byte
			)(-9)), unchecked((byte)(-9)), unchecked((byte)(-1)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 
			22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, unchecked((byte)(-9)
			), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), 37, unchecked(
			(byte)(-9)), 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 
			55, 56, 57, 58, 59, 60, 61, 62, 63, unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9))
			, unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)), unchecked(
			(byte)(-9)), unchecked((byte)(-9)), unchecked((byte)(-9)) };

		// Indicates white space in encoding
		// Indicates equals sign in encoding
		// Decimal  0 -  8
		// Whitespace: Tab and Linefeed
		// Decimal 11 - 12
		// Whitespace: Carriage Return
		// Decimal 14 - 26
		// Decimal 27 - 31
		// Whitespace: Space
		// Decimal 33 - 42
		// Plus sign at decimal 43
		// Decimal 44 - 46
		// Slash at decimal 47
		// Numbers zero through nine
		// Decimal 58 - 60
		// Equals sign at decimal 61
		// Decimal 62 - 64
		// Letters 'A' through 'N'
		// Letters 'O' through 'Z'
		// Decimal 91 - 96
		// Letters 'a' through 'm'
		// Letters 'n' through 'z'
		// Decimal 123 - 127
		// Decimal 128 - 139
		// Decimal 140 - 152
		// Decimal 153 - 165
		// Decimal 166 - 178
		// Decimal 179 - 191
		// Decimal 192 - 204
		// Decimal 205 - 217
		// Decimal 218 - 230
		// Decimal 231 - 243
		// Decimal 244 - 255
		// Decimal  0 -  8
		// Whitespace: Tab and Linefeed
		// Decimal 11 - 12
		// Whitespace: Carriage Return
		// Decimal 14 - 26
		// Decimal 27 - 31
		// Whitespace: Space
		// Decimal 33 - 42
		// Plus sign at decimal 43
		// Decimal 44
		// Minus sign at decimal 45
		// Decimal 46
		// Slash at decimal 47
		// Numbers zero through nine
		// Decimal 58 - 60
		// Equals sign at decimal 61
		// Decimal 62 - 64
		// Letters 'A' through 'N'
		// Letters 'O' through 'Z'
		// Decimal 91 - 94
		// Underscore at decimal 95
		// Decimal 96
		// Letters 'a' through 'm'
		// Letters 'n' through 'z'
		// Decimal 123 - 127
		// Decimal 128 - 139
		// Decimal 140 - 152
		// Decimal 153 - 165
		// Decimal 166 - 178
		// Decimal 179 - 191
		// Decimal 192 - 204
		// Decimal 205 - 217
		// Decimal 218 - 230
		// Decimal 231 - 243
		// Decimal 244 - 255
		// Decimal  0 -  8
		// Whitespace: Tab and Linefeed
		// Decimal 11 - 12
		// Whitespace: Carriage Return
		// Decimal 14 - 26
		// Decimal 27 - 31
		// Whitespace: Space
		// Decimal 33 - 42
		// Plus sign at decimal 43
		// Decimal 44
		// Minus sign at decimal 45
		// Decimal 46
		// Slash at decimal 47
		// Numbers zero through nine
		// Decimal 58 - 60
		// Equals sign at decimal 61
		// Decimal 62 - 64
		// Letters 'A' through 'M'
		// Letters 'N' through 'Z'
		// Decimal 91 - 94
		// Underscore at decimal 95
		// Decimal 96
		// Letters 'a' through 'm'
		// Letters 'n' through 'z'
		// Decimal 123 - 127
		// Decimal 128 - 139
		// Decimal 140 - 152
		// Decimal 153 - 165
		// Decimal 166 - 178
		// Decimal 179 - 191
		// Decimal 192 - 204
		// Decimal 205 - 217
		// Decimal 218 - 230
		// Decimal 231 - 243
		// Decimal 244 - 255
		/// <summary>
		/// Returns one of the _SOMETHING_ALPHABET byte arrays depending on
		/// the options specified.
		/// </summary>
		/// <remarks>
		/// Returns one of the _SOMETHING_ALPHABET byte arrays depending on
		/// the options specified.
		/// It's possible, though silly, to specify ORDERED <b>and</b> URLSAFE
		/// in which case one of them will be picked, though there is
		/// no guarantee as to which one will be picked.
		/// </remarks>
		private static byte[] GetAlphabet(int options)
		{
			if ((options & UrlSafe) == UrlSafe)
			{
				return UrlSafeAlphabet;
			}
			else
			{
				if ((options & Ordered) == Ordered)
				{
					return OrderedAlphabet;
				}
				else
				{
					return StandardAlphabet;
				}
			}
		}

		// end getAlphabet
		/// <summary>
		/// Returns one of the _SOMETHING_DECODABET byte arrays depending on
		/// the options specified.
		/// </summary>
		/// <remarks>
		/// Returns one of the _SOMETHING_DECODABET byte arrays depending on
		/// the options specified.
		/// It's possible, though silly, to specify ORDERED and URL_SAFE
		/// in which case one of them will be picked, though there is
		/// no guarantee as to which one will be picked.
		/// </remarks>
		private static byte[] GetDecodabet(int options)
		{
			if ((options & UrlSafe) == UrlSafe)
			{
				return UrlSafeDecodabet;
			}
			else
			{
				if ((options & Ordered) == Ordered)
				{
					return OrderedDecodabet;
				}
				else
				{
					return StandardDecodabet;
				}
			}
		}

		/// <summary>Defeats instantiation.</summary>
		/// <remarks>Defeats instantiation.</remarks>
		private Base64()
		{
		}

		// end getAlphabet
		/// <summary>
		/// Encodes up to the first three bytes of array <var>threeBytes</var>
		/// and returns a four-byte array in Base64 notation.
		/// </summary>
		/// <remarks>
		/// Encodes up to the first three bytes of array <var>threeBytes</var>
		/// and returns a four-byte array in Base64 notation.
		/// The actual number of significant bytes in your array is
		/// given by <var>numSigBytes</var>.
		/// The array <var>threeBytes</var> needs only be as big as
		/// <var>numSigBytes</var>.
		/// Code can reuse a byte array by passing a four-byte array as <var>b4</var>.
		/// </remarks>
		/// <param name="b4">A reusable byte array to reduce array instantiation</param>
		/// <param name="threeBytes">the array to convert</param>
		/// <param name="numSigBytes">the number of significant bytes in your array</param>
		/// <returns>four byte array in Base64 notation.</returns>
		/// <since>1.5.1</since>
		private static byte[] Encode3to4(byte[] b4, byte[] threeBytes, int numSigBytes, int
			 options)
		{
			Encode3to4(threeBytes, 0, numSigBytes, b4, 0, options);
			return b4;
		}

		// end encode3to4
		/// <summary>
		/// <p>Encodes up to three bytes of the array <var>source</var>
		/// and writes the resulting four Base64 bytes to <var>destination</var>.
		/// </summary>
		/// <remarks>
		/// <p>Encodes up to three bytes of the array <var>source</var>
		/// and writes the resulting four Base64 bytes to <var>destination</var>.
		/// The source and destination arrays can be manipulated
		/// anywhere along their length by specifying
		/// <var>srcOffset</var> and <var>destOffset</var>.
		/// This method does not check to make sure your arrays
		/// are large enough to accomodate <var>srcOffset</var> + 3 for
		/// the <var>source</var> array or <var>destOffset</var> + 4 for
		/// the <var>destination</var> array.
		/// The actual number of significant bytes in your array is
		/// given by <var>numSigBytes</var>.</p>
		/// <p>This is the lowest level of the encoding methods with
		/// all possible parameters.</p>
		/// </remarks>
		/// <param name="source">the array to convert</param>
		/// <param name="srcOffset">the index where conversion begins</param>
		/// <param name="numSigBytes">the number of significant bytes in your array</param>
		/// <param name="destination">the array to hold the conversion</param>
		/// <param name="destOffset">the index where output will be put</param>
		/// <returns>the <var>destination</var> array</returns>
		/// <since>1.3</since>
		private static byte[] Encode3to4(byte[] source, int srcOffset, int numSigBytes, byte
			[] destination, int destOffset, int options)
		{
			byte[] Alphabet = GetAlphabet(options);
			//           1         2         3
			// 01234567890123456789012345678901 Bit position
			// --------000000001111111122222222 Array position from threeBytes
			// --------|    ||    ||    ||    | Six bit groups to index ALPHABET
			//          >>18  >>12  >> 6  >> 0  Right shift necessary
			//                0x3f  0x3f  0x3f  Additional AND
			// Create buffer with zero-padding if there are only one or two
			// significant bytes passed in the array.
			// We have to shift left 24 in order to flush out the 1's that appear
			// when Java treats a value as negative that is cast from a byte to an int.
			int inBuff = (numSigBytes > 0 ? ((int)(((uint)(source[srcOffset] << 24)) >> 8)) : 
				0) | (numSigBytes > 1 ? ((int)(((uint)(source[srcOffset + 1] << 24)) >> 16)) : 0
				) | (numSigBytes > 2 ? ((int)(((uint)(source[srcOffset + 2] << 24)) >> 24)) : 0);
			switch (numSigBytes)
			{
				case 3:
				{
					destination[destOffset] = Alphabet[((int)(((uint)inBuff) >> 18))];
					destination[destOffset + 1] = Alphabet[((int)(((uint)inBuff) >> 12)) & unchecked(
						(int)(0x3f))];
					destination[destOffset + 2] = Alphabet[((int)(((uint)inBuff) >> 6)) & unchecked((
						int)(0x3f))];
					destination[destOffset + 3] = Alphabet[(inBuff) & unchecked((int)(0x3f))];
					return destination;
				}

				case 2:
				{
					destination[destOffset] = Alphabet[((int)(((uint)inBuff) >> 18))];
					destination[destOffset + 1] = Alphabet[((int)(((uint)inBuff) >> 12)) & unchecked(
						(int)(0x3f))];
					destination[destOffset + 2] = Alphabet[((int)(((uint)inBuff) >> 6)) & unchecked((
						int)(0x3f))];
					destination[destOffset + 3] = EqualsSign;
					return destination;
				}

				case 1:
				{
					destination[destOffset] = Alphabet[((int)(((uint)inBuff) >> 18))];
					destination[destOffset + 1] = Alphabet[((int)(((uint)inBuff) >> 12)) & unchecked(
						(int)(0x3f))];
					destination[destOffset + 2] = EqualsSign;
					destination[destOffset + 3] = EqualsSign;
					return destination;
				}

				default:
				{
					return destination;
				}
			}
		}

		// end switch
		// end encode3to4
		/// <summary>
		/// Performs Base64 encoding on the <code>raw</code> ByteBuffer,
		/// writing it to the <code>encoded</code> ByteBuffer.
		/// </summary>
		/// <remarks>
		/// Performs Base64 encoding on the <code>raw</code> ByteBuffer,
		/// writing it to the <code>encoded</code> ByteBuffer.
		/// This is an experimental feature. Currently it does not
		/// pass along any options (such as
		/// <see cref="DoBreakLines">DoBreakLines</see>
		/// or
		/// <see cref="Gzip">Gzip</see>
		/// .
		/// </remarks>
		/// <param name="raw">input buffer</param>
		/// <param name="encoded">output buffer</param>
		/// <since>2.3</since>
		public static void Encode(ByteBuffer raw, ByteBuffer encoded)
		{
			byte[] raw3 = new byte[3];
			byte[] enc4 = new byte[4];
			while (raw.HasRemaining())
			{
				int rem = Math.Min(3, raw.Remaining());
				raw.Get(raw3, 0, rem);
				Couchbase.Lite.Support.Base64.Encode3to4(enc4, raw3, rem, Couchbase.Lite.Support.Base64
					.NoOptions);
				encoded.Put(enc4);
			}
		}

		// end input remaining
		/// <summary>
		/// Performs Base64 encoding on the <code>raw</code> ByteBuffer,
		/// writing it to the <code>encoded</code> CharBuffer.
		/// </summary>
		/// <remarks>
		/// Performs Base64 encoding on the <code>raw</code> ByteBuffer,
		/// writing it to the <code>encoded</code> CharBuffer.
		/// This is an experimental feature. Currently it does not
		/// pass along any options (such as
		/// <see cref="DoBreakLines">DoBreakLines</see>
		/// or
		/// <see cref="Gzip">Gzip</see>
		/// .
		/// </remarks>
		/// <param name="raw">input buffer</param>
		/// <param name="encoded">output buffer</param>
		/// <since>2.3</since>
		public static void Encode(ByteBuffer raw, CharBuffer encoded)
		{
			byte[] raw3 = new byte[3];
			byte[] enc4 = new byte[4];
			while (raw.HasRemaining())
			{
				int rem = Math.Min(3, raw.Remaining());
				raw.Get(raw3, 0, rem);
				Couchbase.Lite.Support.Base64.Encode3to4(enc4, raw3, rem, Couchbase.Lite.Support.Base64
					.NoOptions);
				for (int i = 0; i < 4; i++)
				{
					encoded.Put((char)(enc4[i] & unchecked((int)(0xFF))));
				}
			}
		}

		// end input remaining
		/// <summary>
		/// Serializes an object and returns the Base64-encoded
		/// version of that serialized object.
		/// </summary>
		/// <remarks>
		/// Serializes an object and returns the Base64-encoded
		/// version of that serialized object.
		/// <p>As of v 2.3, if the object
		/// cannot be serialized or there is another error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned a null value, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// The object is not GZip-compressed before being encoded.
		/// </remarks>
		/// <param name="serializableObject">The object to encode</param>
		/// <returns>The Base64-encoded object</returns>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <exception cref="System.ArgumentNullException">if serializedObject is null</exception>
		/// <since>1.4</since>
		public static string EncodeObject(Serializable serializableObject)
		{
			return EncodeObject(serializableObject, NoOptions);
		}

		// end encodeObject
		/// <summary>
		/// Serializes an object and returns the Base64-encoded
		/// version of that serialized object.
		/// </summary>
		/// <remarks>
		/// Serializes an object and returns the Base64-encoded
		/// version of that serialized object.
		/// <p>As of v 2.3, if the object
		/// cannot be serialized or there is another error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned a null value, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// The object is not GZip-compressed before being encoded.
		/// <p>
		/// Example options:<pre>
		/// GZIP: gzip-compresses object before encoding it.
		/// DO_BREAK_LINES: break lines at 76 characters
		/// </pre>
		/// <p>
		/// Example: <code>encodeObject( myObj, Base64.GZIP )</code> or
		/// <p>
		/// Example: <code>encodeObject( myObj, Base64.GZIP | Base64.DO_BREAK_LINES )</code>
		/// </remarks>
		/// <param name="serializableObject">The object to encode</param>
		/// <param name="options">Specified options</param>
		/// <returns>The Base64-encoded object</returns>
		/// <seealso cref="Gzip">Gzip</seealso>
		/// <seealso cref="DoBreakLines">DoBreakLines</seealso>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <since>2.0</since>
        public static string EncodeObject(ISerializable serializableObject, int options)
		{
			if (serializableObject == null)
			{
				throw new ArgumentNullException("Cannot serialize a null object.");
			}
			// end if: null
			// Streams
			ByteArrayOutputStream baos = null;
			OutputStream b64os = null;
			GZIPOutputStream gzos = null;
			ObjectOutputStream oos = null;
			try
			{
				// ObjectOutputStream -> (GZIP) -> Base64 -> ByteArrayOutputStream
				baos = new ByteArrayOutputStream();
				b64os = new Base64.OutputStream(baos, Encode | options);
				if ((options & Gzip) != 0)
				{
					// Gzip
					gzos = new GZIPOutputStream(b64os);
					oos = new ObjectOutputStream(gzos);
				}
				else
				{
					// Not gzipped
					oos = new ObjectOutputStream(b64os);
				}
                oos.WriteObject(serializableObject);
			}
			catch (IOException e)
			{
				// end try
				// Catch it and then throw it immediately so that
				// the finally{} block is called for cleanup.
				throw;
			}
			finally
			{
				// end catch
				try
				{
					oos.Close();
				}
				catch (Exception)
				{
				}
				try
				{
					gzos.Close();
				}
				catch (Exception)
				{
				}
				try
				{
					b64os.Close();
				}
				catch (Exception)
				{
				}
				try
				{
					baos.Close();
				}
				catch (Exception)
				{
				}
			}
			// end finally
			// Return value according to relevant encoding.
			try
			{
				return Sharpen.Runtime.GetStringForBytes(baos.ToByteArray(), PreferredEncoding);
			}
			catch (UnsupportedEncodingException)
			{
				// end try
				// Fall back to some Java default
				return Sharpen.Runtime.GetStringForBytes(baos.ToByteArray());
			}
		}

		// end catch
		// end encode
		/// <summary>Encodes a byte array into Base64 notation.</summary>
		/// <remarks>
		/// Encodes a byte array into Base64 notation.
		/// Does not GZip-compress data.
		/// </remarks>
		/// <param name="source">The data to convert</param>
		/// <returns>The data in Base64-encoded form</returns>
		/// <exception cref="System.ArgumentNullException">if source array is null</exception>
		/// <since>1.4</since>
		public static string EncodeBytes(byte[] source)
		{
			// Since we're not going to have the GZIP encoding turned on,
			// we're not going to have an java.io.IOException thrown, so
			// we should not force the user to have to catch it.
			string encoded = null;
			try
			{
				encoded = EncodeBytes(source, 0, source.Length, NoOptions);
			}
			catch (IOException ex)
			{
				System.Diagnostics.Debug.Assert(false, ex.Message);
			}
			// end catch
			System.Diagnostics.Debug.Assert(encoded != null);
			return encoded;
		}

		// end encodeBytes
		/// <summary>Encodes a byte array into Base64 notation.</summary>
		/// <remarks>
		/// Encodes a byte array into Base64 notation.
		/// <p>
		/// Example options:<pre>
		/// GZIP: gzip-compresses object before encoding it.
		/// DO_BREAK_LINES: break lines at 76 characters
		/// <i>Note: Technically, this makes your encoding non-compliant.</i>
		/// </pre>
		/// <p>
		/// Example: <code>encodeBytes( myData, Base64.GZIP )</code> or
		/// <p>
		/// Example: <code>encodeBytes( myData, Base64.GZIP | Base64.DO_BREAK_LINES )</code>
		/// <p>As of v 2.3, if there is an error with the GZIP stream,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned a null value, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="source">The data to convert</param>
		/// <param name="options">Specified options</param>
		/// <returns>The Base64-encoded data as a String</returns>
		/// <seealso cref="Gzip">Gzip</seealso>
		/// <seealso cref="DoBreakLines">DoBreakLines</seealso>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <exception cref="System.ArgumentNullException">if source array is null</exception>
		/// <since>2.0</since>
		public static string EncodeBytes(byte[] source, int options)
		{
			return EncodeBytes(source, 0, source.Length, options);
		}

		// end encodeBytes
		/// <summary>Encodes a byte array into Base64 notation.</summary>
		/// <remarks>
		/// Encodes a byte array into Base64 notation.
		/// Does not GZip-compress data.
		/// <p>As of v 2.3, if there is an error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned a null value, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="source">The data to convert</param>
		/// <param name="off">Offset in array where conversion should begin</param>
		/// <param name="len">Length of data to convert</param>
		/// <returns>The Base64-encoded data as a String</returns>
		/// <exception cref="System.ArgumentNullException">if source array is null</exception>
		/// <exception cref="System.ArgumentException">if source array, offset, or length are invalid
		/// 	</exception>
		/// <since>1.4</since>
		public static string EncodeBytes(byte[] source, int off, int len)
		{
			// Since we're not going to have the GZIP encoding turned on,
			// we're not going to have an java.io.IOException thrown, so
			// we should not force the user to have to catch it.
			string encoded = null;
			try
			{
				encoded = EncodeBytes(source, off, len, NoOptions);
			}
			catch (IOException ex)
			{
				System.Diagnostics.Debug.Assert(false, ex.Message);
			}
			// end catch
			System.Diagnostics.Debug.Assert(encoded != null);
			return encoded;
		}

		// end encodeBytes
		/// <summary>Encodes a byte array into Base64 notation.</summary>
		/// <remarks>
		/// Encodes a byte array into Base64 notation.
		/// <p>
		/// Example options:<pre>
		/// GZIP: gzip-compresses object before encoding it.
		/// DO_BREAK_LINES: break lines at 76 characters
		/// <i>Note: Technically, this makes your encoding non-compliant.</i>
		/// </pre>
		/// <p>
		/// Example: <code>encodeBytes( myData, Base64.GZIP )</code> or
		/// <p>
		/// Example: <code>encodeBytes( myData, Base64.GZIP | Base64.DO_BREAK_LINES )</code>
		/// <p>As of v 2.3, if there is an error with the GZIP stream,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned a null value, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="source">The data to convert</param>
		/// <param name="off">Offset in array where conversion should begin</param>
		/// <param name="len">Length of data to convert</param>
		/// <param name="options">Specified options</param>
		/// <returns>The Base64-encoded data as a String</returns>
		/// <seealso cref="Gzip">Gzip</seealso>
		/// <seealso cref="DoBreakLines">DoBreakLines</seealso>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <exception cref="System.ArgumentNullException">if source array is null</exception>
		/// <exception cref="System.ArgumentException">if source array, offset, or length are invalid
		/// 	</exception>
		/// <since>2.0</since>
		public static string EncodeBytes(byte[] source, int off, int len, int options)
		{
			byte[] encoded = EncodeBytesToBytes(source, off, len, options);
			// Return value according to relevant encoding.
			try
			{
				return Sharpen.Runtime.GetStringForBytes(encoded, PreferredEncoding);
			}
			catch (UnsupportedEncodingException)
			{
				// end try
				return Sharpen.Runtime.GetStringForBytes(encoded);
			}
		}

		// end catch
		// end encodeBytes
		/// <summary>
		/// Similar to
		/// <see cref="EncodeBytes(byte[])">EncodeBytes(byte[])</see>
		/// but returns
		/// a byte array instead of instantiating a String. This is more efficient
		/// if you're working with I/O streams and have large data sets to encode.
		/// </summary>
		/// <param name="source">The data to convert</param>
		/// <returns>The Base64-encoded data as a byte[] (of ASCII characters)</returns>
		/// <exception cref="System.ArgumentNullException">if source array is null</exception>
		/// <since>2.3.1</since>
		public static byte[] EncodeBytesToBytes(byte[] source)
		{
			byte[] encoded = null;
			try
			{
				encoded = EncodeBytesToBytes(source, 0, source.Length, Couchbase.Lite.Support.Base64
					.NoOptions);
			}
			catch (IOException ex)
			{
				System.Diagnostics.Debug.Assert(false, "IOExceptions only come from GZipping, which is turned off: "
					 + ex.Message);
			}
			return encoded;
		}

		/// <summary>
		/// Similar to
		/// <see cref="EncodeBytes(byte[], int, int, int)">EncodeBytes(byte[], int, int, int)
		/// 	</see>
		/// but returns
		/// a byte array instead of instantiating a String. This is more efficient
		/// if you're working with I/O streams and have large data sets to encode.
		/// </summary>
		/// <param name="source">The data to convert</param>
		/// <param name="off">Offset in array where conversion should begin</param>
		/// <param name="len">Length of data to convert</param>
		/// <param name="options">Specified options</param>
		/// <returns>The Base64-encoded data as a String</returns>
		/// <seealso cref="Gzip">Gzip</seealso>
		/// <seealso cref="DoBreakLines">DoBreakLines</seealso>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <exception cref="System.ArgumentNullException">if source array is null</exception>
		/// <exception cref="System.ArgumentException">if source array, offset, or length are invalid
		/// 	</exception>
		/// <since>2.3.1</since>
		public static byte[] EncodeBytesToBytes(byte[] source, int off, int len, int options
			)
		{
			if (source == null)
			{
				throw new ArgumentNullException("Cannot serialize a null array.");
			}
			// end if: null
			if (off < 0)
			{
				throw new ArgumentException("Cannot have negative offset: " + off);
			}
			// end if: off < 0
			if (len < 0)
			{
				throw new ArgumentException("Cannot have length offset: " + len);
			}
			// end if: len < 0
			if (off + len > source.Length)
			{
				throw new ArgumentException(string.Format("Cannot have offset of %d and length of %d with array of length %d"
					, off, len, source.Length));
			}
			// end if: off < 0
			// Compress?
			if ((options & Gzip) != 0)
			{
				ByteArrayOutputStream baos = null;
				GZIPOutputStream gzos = null;
				Base64.OutputStream b64os = null;
				try
				{
					// GZip -> Base64 -> ByteArray
					baos = new ByteArrayOutputStream();
					b64os = new Base64.OutputStream(baos, Encode | options);
					gzos = new GZIPOutputStream(b64os);
					gzos.Write(source, off, len);
					gzos.Close();
				}
				catch (IOException e)
				{
					// end try
					// Catch it and then throw it immediately so that
					// the finally{} block is called for cleanup.
					throw;
				}
				finally
				{
					// end catch
					try
					{
						gzos.Close();
					}
					catch (Exception)
					{
					}
					try
					{
						b64os.Close();
					}
					catch (Exception)
					{
					}
					try
					{
						baos.Close();
					}
					catch (Exception)
					{
					}
				}
				// end finally
				return baos.ToByteArray();
			}
			else
			{
				// end if: compress
				// Else, don't compress. Better not to use streams at all then.
				bool breakLines = (options & DoBreakLines) != 0;
				//int    len43   = len * 4 / 3;
				//byte[] outBuff = new byte[   ( len43 )                      // Main 4:3
				//                           + ( (len % 3) > 0 ? 4 : 0 )      // Account for padding
				//                           + (breakLines ? ( len43 / MAX_LINE_LENGTH ) : 0) ]; // New lines
				// Try to determine more precisely how big the array needs to be.
				// If we get it right, we don't have to do an array copy, and
				// we save a bunch of memory.
				int encLen = (len / 3) * 4 + (len % 3 > 0 ? 4 : 0);
				// Bytes needed for actual encoding
				if (breakLines)
				{
					encLen += encLen / MaxLineLength;
				}
				// Plus extra newline characters
				byte[] outBuff = new byte[encLen];
				int d = 0;
				int e = 0;
				int len2 = len - 2;
				int lineLength = 0;
				for (; d < len2; d += 3, e += 4)
				{
					Encode3to4(source, d + off, 3, outBuff, e, options);
					lineLength += 4;
					if (breakLines && lineLength >= MaxLineLength)
					{
						outBuff[e + 4] = NewLine;
						e++;
						lineLength = 0;
					}
				}
				// end if: end of line
				// en dfor: each piece of array
				if (d < len)
				{
					Encode3to4(source, d + off, len - d, outBuff, e, options);
					e += 4;
				}
				// end if: some padding needed
				// Only resize array if we didn't guess it right.
				if (e <= outBuff.Length - 1)
				{
					// If breaking lines and the last byte falls right at
					// the line length (76 bytes per line), there will be
					// one extra byte, and the array will need to be resized.
					// Not too bad of an estimate on array size, I'd say.
					byte[] finalOut = new byte[e];
					System.Array.Copy(outBuff, 0, finalOut, 0, e);
					//System.err.println("Having to resize array from " + outBuff.length + " to " + e );
					return finalOut;
				}
				else
				{
					//System.err.println("No need to resize array.");
					return outBuff;
				}
			}
		}

		// end else: don't compress
		// end encodeBytesToBytes
		/// <summary>
		/// Decodes four bytes from array <var>source</var>
		/// and writes the resulting bytes (up to three of them)
		/// to <var>destination</var>.
		/// </summary>
		/// <remarks>
		/// Decodes four bytes from array <var>source</var>
		/// and writes the resulting bytes (up to three of them)
		/// to <var>destination</var>.
		/// The source and destination arrays can be manipulated
		/// anywhere along their length by specifying
		/// <var>srcOffset</var> and <var>destOffset</var>.
		/// This method does not check to make sure your arrays
		/// are large enough to accomodate <var>srcOffset</var> + 4 for
		/// the <var>source</var> array or <var>destOffset</var> + 3 for
		/// the <var>destination</var> array.
		/// This method returns the actual number of bytes that
		/// were converted from the Base64 encoding.
		/// <p>This is the lowest level of the decoding methods with
		/// all possible parameters.</p>
		/// </remarks>
		/// <param name="source">the array to convert</param>
		/// <param name="srcOffset">the index where conversion begins</param>
		/// <param name="destination">the array to hold the conversion</param>
		/// <param name="destOffset">the index where output will be put</param>
		/// <param name="options">alphabet type is pulled from this (standard, url-safe, ordered)
		/// 	</param>
		/// <returns>the number of decoded bytes converted</returns>
		/// <exception cref="System.ArgumentNullException">if source or destination arrays are null
		/// 	</exception>
		/// <exception cref="System.ArgumentException">
		/// if srcOffset or destOffset are invalid
		/// or there is not enough room in the array.
		/// </exception>
		/// <since>1.3</since>
		private static int Decode4to3(byte[] source, int srcOffset, byte[] destination, int
			 destOffset, int options)
		{
			// Lots of error checking and exception throwing
			if (source == null)
			{
				throw new ArgumentNullException("Source array was null.");
			}
			// end if
			if (destination == null)
			{
				throw new ArgumentNullException("Destination array was null.");
			}
			// end if
			if (srcOffset < 0 || srcOffset + 3 >= source.Length)
			{
				throw new ArgumentException(string.Format("Source array with length %d cannot have offset of %d and still process four bytes."
					, source.Length, srcOffset));
			}
			// end if
			if (destOffset < 0 || destOffset + 2 >= destination.Length)
			{
				throw new ArgumentException(string.Format("Destination array with length %d cannot have offset of %d and still store three bytes."
					, destination.Length, destOffset));
			}
			// end if
			byte[] Decodabet = GetDecodabet(options);
			// Example: Dk==
			if (source[srcOffset + 2] == EqualsSign)
			{
				// Two ways to do the same thing. Don't know which way I like best.
				//int outBuff =   ( ( DECODABET[ source[ srcOffset    ] ] << 24 ) >>>  6 )
				//              | ( ( DECODABET[ source[ srcOffset + 1] ] << 24 ) >>> 12 );
				int outBuff = ((Decodabet[source[srcOffset]] & unchecked((int)(0xFF))) << 18) | (
					(Decodabet[source[srcOffset + 1]] & unchecked((int)(0xFF))) << 12);
				destination[destOffset] = unchecked((byte)((int)(((uint)outBuff) >> 16)));
				return 1;
			}
			else
			{
				// Example: DkL=
				if (source[srcOffset + 3] == EqualsSign)
				{
					// Two ways to do the same thing. Don't know which way I like best.
					//int outBuff =   ( ( DECODABET[ source[ srcOffset     ] ] << 24 ) >>>  6 )
					//              | ( ( DECODABET[ source[ srcOffset + 1 ] ] << 24 ) >>> 12 )
					//              | ( ( DECODABET[ source[ srcOffset + 2 ] ] << 24 ) >>> 18 );
					int outBuff = ((Decodabet[source[srcOffset]] & unchecked((int)(0xFF))) << 18) | (
						(Decodabet[source[srcOffset + 1]] & unchecked((int)(0xFF))) << 12) | ((Decodabet
						[source[srcOffset + 2]] & unchecked((int)(0xFF))) << 6);
					destination[destOffset] = unchecked((byte)((int)(((uint)outBuff) >> 16)));
					destination[destOffset + 1] = unchecked((byte)((int)(((uint)outBuff) >> 8)));
					return 2;
				}
				else
				{
					// Example: DkLE
					// Two ways to do the same thing. Don't know which way I like best.
					//int outBuff =   ( ( DECODABET[ source[ srcOffset     ] ] << 24 ) >>>  6 )
					//              | ( ( DECODABET[ source[ srcOffset + 1 ] ] << 24 ) >>> 12 )
					//              | ( ( DECODABET[ source[ srcOffset + 2 ] ] << 24 ) >>> 18 )
					//              | ( ( DECODABET[ source[ srcOffset + 3 ] ] << 24 ) >>> 24 );
					int outBuff = ((Decodabet[source[srcOffset]] & unchecked((int)(0xFF))) << 18) | (
						(Decodabet[source[srcOffset + 1]] & unchecked((int)(0xFF))) << 12) | ((Decodabet
						[source[srcOffset + 2]] & unchecked((int)(0xFF))) << 6) | ((Decodabet[source[srcOffset
						 + 3]] & unchecked((int)(0xFF))));
					destination[destOffset] = unchecked((byte)(outBuff >> 16));
					destination[destOffset + 1] = unchecked((byte)(outBuff >> 8));
					destination[destOffset + 2] = unchecked((byte)(outBuff));
					return 3;
				}
			}
		}

		// end decodeToBytes
		/// <summary>
		/// Low-level access to decoding ASCII characters in
		/// the form of a byte array.
		/// </summary>
		/// <remarks>
		/// Low-level access to decoding ASCII characters in
		/// the form of a byte array. <strong>Ignores GUNZIP option, if
		/// it's set.</strong> This is not generally a recommended method,
		/// although it is used internally as part of the decoding process.
		/// Special case: if len = 0, an empty array is returned. Still,
		/// if you need more speed and reduced memory footprint (and aren't
		/// gzipping), consider this method.
		/// </remarks>
		/// <param name="source">The Base64 encoded data</param>
		/// <returns>decoded data</returns>
		/// <since>2.3.1</since>
		/// <exception cref="System.IO.IOException"></exception>
		public static byte[] Decode(byte[] source)
		{
			byte[] decoded = null;
			//        try {
			decoded = Decode(source, 0, source.Length, Couchbase.Lite.Support.Base64.NoOptions
				);
			//        } catch( java.io.IOException ex ) {
			//            assert false : "IOExceptions only come from GZipping, which is turned off: " + ex.getMessage();
			//        }
			return decoded;
		}

		/// <summary>
		/// Low-level access to decoding ASCII characters in
		/// the form of a byte array.
		/// </summary>
		/// <remarks>
		/// Low-level access to decoding ASCII characters in
		/// the form of a byte array. <strong>Ignores GUNZIP option, if
		/// it's set.</strong> This is not generally a recommended method,
		/// although it is used internally as part of the decoding process.
		/// Special case: if len = 0, an empty array is returned. Still,
		/// if you need more speed and reduced memory footprint (and aren't
		/// gzipping), consider this method.
		/// </remarks>
		/// <param name="source">The Base64 encoded data</param>
		/// <param name="off">The offset of where to begin decoding</param>
		/// <param name="len">The length of characters to decode</param>
		/// <param name="options">Can specify options such as alphabet type to use</param>
		/// <returns>decoded data</returns>
		/// <exception cref="System.IO.IOException">If bogus characters exist in source data</exception>
		/// <since>1.3</since>
		public static byte[] Decode(byte[] source, int off, int len, int options)
		{
			// Lots of error checking and exception throwing
			if (source == null)
			{
				throw new ArgumentNullException("Cannot decode null source array.");
			}
			// end if
			if (off < 0 || off + len > source.Length)
			{
				throw new ArgumentException(string.Format("Source array with length %d cannot have offset of %d and process %d bytes."
					, source.Length, off, len));
			}
			// end if
			if (len == 0)
			{
				return new byte[0];
			}
			else
			{
				if (len < 4)
				{
					throw new ArgumentException("Base64-encoded string must have at least four characters, but length specified was "
						 + len);
				}
			}
			// end if
			byte[] Decodabet = GetDecodabet(options);
			int len34 = len * 3 / 4;
			// Estimate on array size
			byte[] outBuff = new byte[len34];
			// Upper limit on size of output
			int outBuffPosn = 0;
			// Keep track of where we're writing
			byte[] b4 = new byte[4];
			// Four byte buffer from source, eliminating white space
			int b4Posn = 0;
			// Keep track of four byte input buffer
			int i = 0;
			// Source array counter
			byte sbiDecode = 0;
			// Special value from DECODABET
			for (i = off; i < off + len; i++)
			{
				// Loop through source
				sbiDecode = Decodabet[source[i] & unchecked((int)(0xFF))];
				// White space, Equals sign, or legit Base64 character
				// Note the values such as -5 and -9 in the
				// DECODABETs at the top of the file.
				if (sbiDecode >= WhiteSpaceEnc)
				{
					if (sbiDecode >= EqualsSignEnc)
					{
						b4[b4Posn++] = source[i];
						// Save non-whitespace
						if (b4Posn > 3)
						{
							// Time to decode?
							outBuffPosn += Decode4to3(b4, 0, outBuff, outBuffPosn, options);
							b4Posn = 0;
							// If that was the equals sign, break out of 'for' loop
							if (source[i] == EqualsSign)
							{
								break;
							}
						}
					}
				}
				else
				{
					// end if: equals sign
					// end if: quartet built
					// end if: equals sign or better
					// end if: white space, equals sign or better
					// There's a bad input character in the Base64 stream.
					throw new IOException(string.Format("Bad Base64 input character decimal %d in array position %d"
						, ((int)source[i]) & unchecked((int)(0xFF)), i));
				}
			}
			// end else:
			// each input character
			byte[] @out = new byte[outBuffPosn];
			System.Array.Copy(outBuff, 0, @out, 0, outBuffPosn);
			return @out;
		}

		// end decode
		/// <summary>
		/// Decodes data from Base64 notation, automatically
		/// detecting gzip-compressed data and decompressing it.
		/// </summary>
		/// <remarks>
		/// Decodes data from Base64 notation, automatically
		/// detecting gzip-compressed data and decompressing it.
		/// </remarks>
		/// <param name="s">the string to decode</param>
		/// <returns>the decoded data</returns>
		/// <exception cref="System.IO.IOException">If there is a problem</exception>
		/// <since>1.4</since>
		public static byte[] Decode(string s)
		{
			return Decode(s, NoOptions);
		}

		/// <summary>
		/// Decodes data from Base64 notation, automatically
		/// detecting gzip-compressed data and decompressing it.
		/// </summary>
		/// <remarks>
		/// Decodes data from Base64 notation, automatically
		/// detecting gzip-compressed data and decompressing it.
		/// </remarks>
		/// <param name="s">the string to decode</param>
		/// <param name="options">encode options such as URL_SAFE</param>
		/// <returns>the decoded data</returns>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <exception cref="System.ArgumentNullException">if <tt>s</tt> is null</exception>
		/// <since>1.4</since>
		public static byte[] Decode(string s, int options)
		{
			if (s == null)
			{
				throw new ArgumentNullException("Input string was null.");
			}
			// end if
			byte[] bytes;
			try
			{
				bytes = Sharpen.Runtime.GetBytesForString(s, PreferredEncoding);
			}
			catch (UnsupportedEncodingException)
			{
				// end try
				bytes = Sharpen.Runtime.GetBytesForString(s);
			}
			// end catch
			//</change>
			// Decode
			bytes = Decode(bytes, 0, bytes.Length, options);
			// Check to see if it's gzip-compressed
			// GZIP Magic Two-Byte Number: 0x8b1f (35615)
			bool dontGunzip = (options & DontGunzip) != 0;
			if ((bytes != null) && (bytes.Length >= 4) && (!dontGunzip))
			{
				int head = ((int)bytes[0] & unchecked((int)(0xff))) | ((bytes[1] << 8) & unchecked(
					(int)(0xff00)));
				if (GZIPInputStream.GzipMagic == head)
				{
					ByteArrayInputStream bais = null;
					GZIPInputStream gzis = null;
					ByteArrayOutputStream baos = null;
					byte[] buffer = new byte[2048];
					int length = 0;
					try
					{
						baos = new ByteArrayOutputStream();
						bais = new ByteArrayInputStream(bytes);
						gzis = new GZIPInputStream(bais);
						while ((length = gzis.Read(buffer)) >= 0)
						{
							baos.Write(buffer, 0, length);
						}
						// end while: reading input
						// No error? Get new bytes.
						bytes = baos.ToByteArray();
					}
					catch (IOException e)
					{
						// end try
						Sharpen.Runtime.PrintStackTrace(e);
					}
					finally
					{
						// Just return originally-decoded bytes
						// end catch
						try
						{
							baos.Close();
						}
						catch (Exception)
						{
						}
						try
						{
							gzis.Close();
						}
						catch (Exception)
						{
						}
						try
						{
							bais.Close();
						}
						catch (Exception)
						{
						}
					}
				}
			}
			// end finally
			// end if: gzipped
			// end if: bytes.length >= 2
			return bytes;
		}

		// end decode
		/// <summary>
		/// Attempts to decode Base64 data and deserialize a Java
		/// Object within.
		/// </summary>
		/// <remarks>
		/// Attempts to decode Base64 data and deserialize a Java
		/// Object within. Returns <tt>null</tt> if there was an error.
		/// </remarks>
		/// <param name="encodedObject">The Base64 data to decode</param>
		/// <returns>The decoded and deserialized object</returns>
		/// <exception cref="System.ArgumentNullException">if encodedObject is null</exception>
		/// <exception cref="System.IO.IOException">if there is a general error</exception>
		/// <exception cref="System.TypeLoadException">
		/// if the decoded object is of a
		/// class that cannot be found by the JVM
		/// </exception>
		/// <since>1.5</since>
		public static object DecodeToObject(string encodedObject)
		{
			return DecodeToObject(encodedObject, NoOptions, null);
		}

		/// <summary>
		/// Attempts to decode Base64 data and deserialize a Java
		/// Object within.
		/// </summary>
		/// <remarks>
		/// Attempts to decode Base64 data and deserialize a Java
		/// Object within. Returns <tt>null</tt> if there was an error.
		/// If <tt>loader</tt> is not null, it will be the class loader
		/// used when deserializing.
		/// </remarks>
		/// <param name="encodedObject">The Base64 data to decode</param>
		/// <param name="options">Various parameters related to decoding</param>
		/// <param name="loader">Optional class loader to use in deserializing classes.</param>
		/// <returns>The decoded and deserialized object</returns>
		/// <exception cref="System.ArgumentNullException">if encodedObject is null</exception>
		/// <exception cref="System.IO.IOException">if there is a general error</exception>
		/// <exception cref="System.TypeLoadException">
		/// if the decoded object is of a
		/// class that cannot be found by the JVM
		/// </exception>
		/// <since>2.3.4</since>
		public static object DecodeToObject(string encodedObject, int options, ClassLoader
			 loader)
		{
			// Decode and gunzip if necessary
			byte[] objBytes = Decode(encodedObject, options);
			ByteArrayInputStream bais = null;
			ObjectInputStream ois = null;
			object obj = null;
			try
			{
				bais = new ByteArrayInputStream(objBytes);
				// If no custom class loader is provided, use Java's builtin OIS.
				if (loader == null)
				{
					ois = new ObjectInputStream(bais);
				}
				else
				{
					// end if: no loader provided
					// Else make a customized object input stream that uses
					// the provided class loader.
					ois = new _ObjectInputStream_1358(loader, bais);
				}
				// Class loader knows of this class.
				// end else: not null
				// end resolveClass
				// end ois
				// end else: no custom class loader
				obj = ois.ReadObject();
			}
			catch (IOException e)
			{
				// end try
				throw;
			}
			catch (TypeLoadException e)
			{
				// Catch and throw in order to execute finally{}
				// end catch
				throw;
			}
			finally
			{
				// Catch and throw in order to execute finally{}
				// end catch
				try
				{
					bais.Close();
				}
				catch (Exception)
				{
				}
				try
				{
					ois.Close();
				}
				catch (Exception)
				{
				}
			}
			// end finally
			return obj;
		}

		private sealed class _ObjectInputStream_1358 : ObjectInputStream
		{
			public _ObjectInputStream_1358(ClassLoader loader, InputStream baseArg1) : base(baseArg1
				)
			{
				this.loader = loader;
			}

			/// <exception cref="System.IO.IOException"></exception>
			/// <exception cref="System.TypeLoadException"></exception>
			protected override Type ResolveClass(ObjectStreamClass streamClass)
			{
				Type c = Sharpen.Runtime.GetType(streamClass.GetName(), false, loader);
				if (c == null)
				{
					return base.ResolveClass(streamClass);
				}
				else
				{
					return c;
				}
			}

			private readonly ClassLoader loader;
		}

		// end decodeObject
		/// <summary>Convenience method for encoding data to a file.</summary>
		/// <remarks>
		/// Convenience method for encoding data to a file.
		/// <p>As of v 2.3, if there is a error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned false, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="dataToEncode">byte array of data to encode in base64 form</param>
		/// <param name="filename">Filename for saving encoded data</param>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <exception cref="System.ArgumentNullException">if dataToEncode is null</exception>
		/// <since>2.1</since>
		public static void EncodeToFile(byte[] dataToEncode, string filename)
		{
			if (dataToEncode == null)
			{
				throw new ArgumentNullException("Data to encode was null.");
			}
			// end iff
			Base64.OutputStream bos = null;
			try
			{
				bos = new Base64.OutputStream(new FileOutputStream(filename), Couchbase.Lite.Support.Base64
					.Encode);
				bos.Write(dataToEncode);
			}
			catch (IOException e)
			{
				// end try
				throw;
			}
			finally
			{
				// Catch and throw to execute finally{} block
				// end catch: java.io.IOException
				try
				{
					bos.Close();
				}
				catch (Exception)
				{
				}
			}
		}

		// end finally
		// end encodeToFile
		/// <summary>Convenience method for decoding data to a file.</summary>
		/// <remarks>
		/// Convenience method for decoding data to a file.
		/// <p>As of v 2.3, if there is a error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned false, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="dataToDecode">Base64-encoded data as a string</param>
		/// <param name="filename">Filename for saving decoded data</param>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <since>2.1</since>
		public static void DecodeToFile(string dataToDecode, string filename)
		{
			Base64.OutputStream bos = null;
			try
			{
				bos = new Base64.OutputStream(new FileOutputStream(filename), Couchbase.Lite.Support.Base64
					.Decode);
				bos.Write(Sharpen.Runtime.GetBytesForString(dataToDecode, PreferredEncoding));
			}
			catch (IOException e)
			{
				// end try
				throw;
			}
			finally
			{
				// Catch and throw to execute finally{} block
				// end catch: java.io.IOException
				try
				{
					bos.Close();
				}
				catch (Exception)
				{
				}
			}
		}

		// end finally
		// end decodeToFile
		/// <summary>
		/// Convenience method for reading a base64-encoded
		/// file and decoding it.
		/// </summary>
		/// <remarks>
		/// Convenience method for reading a base64-encoded
		/// file and decoding it.
		/// <p>As of v 2.3, if there is a error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned false, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="filename">Filename for reading encoded data</param>
		/// <returns>decoded byte array</returns>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <since>2.1</since>
		public static byte[] DecodeFromFile(string filename)
		{
			byte[] decodedData = null;
			Base64.InputStream bis = null;
			try
			{
				// Set up some useful variables
				FilePath file = new FilePath(filename);
				byte[] buffer = null;
				int length = 0;
				int numBytes = 0;
				// Check for size of file
				if (file.Length() > int.MaxValue)
				{
					throw new IOException("File is too big for this convenience method (" + file.Length
						() + " bytes).");
				}
				// end if: file too big for int index
				buffer = new byte[(int)file.Length()];
				// Open a stream
				bis = new Base64.InputStream(new BufferedInputStream(new FileInputStream(file)), 
					Couchbase.Lite.Support.Base64.Decode);
				// Read until done
				while ((numBytes = bis.Read(buffer, length, 4096)) >= 0)
				{
					length += numBytes;
				}
				// end while
				// Save in a variable to return
				decodedData = new byte[length];
				System.Array.Copy(buffer, 0, decodedData, 0, length);
			}
			catch (IOException e)
			{
				// end try
				throw;
			}
			finally
			{
				// Catch and release to execute finally{}
				// end catch: java.io.IOException
				try
				{
					bis.Close();
				}
				catch (Exception)
				{
				}
			}
			// end finally
			return decodedData;
		}

		// end decodeFromFile
		/// <summary>
		/// Convenience method for reading a binary file
		/// and base64-encoding it.
		/// </summary>
		/// <remarks>
		/// Convenience method for reading a binary file
		/// and base64-encoding it.
		/// <p>As of v 2.3, if there is a error,
		/// the method will throw an java.io.IOException. <b>This is new to v2.3!</b>
		/// In earlier versions, it just returned false, but
		/// in retrospect that's a pretty poor way to handle it.</p>
		/// </remarks>
		/// <param name="filename">Filename for reading binary data</param>
		/// <returns>base64-encoded string</returns>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <since>2.1</since>
		public static string EncodeFromFile(string filename)
		{
			string encodedData = null;
			Base64.InputStream bis = null;
			try
			{
				// Set up some useful variables
				FilePath file = new FilePath(filename);
				byte[] buffer = new byte[Math.Max((int)(file.Length() * 1.4 + 1), 40)];
				// Need max() for math on small files (v2.2.1); Need +1 for a few corner cases (v2.3.5)
				int length = 0;
				int numBytes = 0;
				// Open a stream
				bis = new Base64.InputStream(new BufferedInputStream(new FileInputStream(file)), 
					Couchbase.Lite.Support.Base64.Encode);
				// Read until done
				while ((numBytes = bis.Read(buffer, length, 4096)) >= 0)
				{
					length += numBytes;
				}
				// end while
				// Save in a variable to return
				encodedData = Sharpen.Runtime.GetStringForBytes(buffer, 0, length, Couchbase.Lite.Support.Base64
					.PreferredEncoding);
			}
			catch (IOException e)
			{
				// end try
				throw;
			}
			finally
			{
				// Catch and release to execute finally{}
				// end catch: java.io.IOException
				try
				{
					bis.Close();
				}
				catch (Exception)
				{
				}
			}
			// end finally
			return encodedData;
		}

		// end encodeFromFile
		/// <summary>Reads <tt>infile</tt> and encodes it to <tt>outfile</tt>.</summary>
		/// <remarks>Reads <tt>infile</tt> and encodes it to <tt>outfile</tt>.</remarks>
		/// <param name="infile">Input file</param>
		/// <param name="outfile">Output file</param>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <since>2.2</since>
		public static void EncodeFileToFile(string infile, string outfile)
		{
			string encoded = Couchbase.Lite.Support.Base64.EncodeFromFile(infile);
			OutputStream @out = null;
			try
			{
				@out = new BufferedOutputStream(new FileOutputStream(outfile));
				@out.Write(Sharpen.Runtime.GetBytesForString(encoded, "US-ASCII"));
			}
			catch (IOException e)
			{
				// Strict, 7-bit output.
				// end try
				throw;
			}
			finally
			{
				// Catch and release to execute finally{}
				// end catch
				try
				{
					@out.Close();
				}
				catch (Exception)
				{
				}
			}
		}

		// end finally
		// end encodeFileToFile
		/// <summary>Reads <tt>infile</tt> and decodes it to <tt>outfile</tt>.</summary>
		/// <remarks>Reads <tt>infile</tt> and decodes it to <tt>outfile</tt>.</remarks>
		/// <param name="infile">Input file</param>
		/// <param name="outfile">Output file</param>
		/// <exception cref="System.IO.IOException">if there is an error</exception>
		/// <since>2.2</since>
		public static void DecodeFileToFile(string infile, string outfile)
		{
			byte[] decoded = Couchbase.Lite.Support.Base64.DecodeFromFile(infile);
			OutputStream @out = null;
			try
			{
				@out = new BufferedOutputStream(new FileOutputStream(outfile));
				@out.Write(decoded);
			}
			catch (IOException e)
			{
				// end try
				throw;
			}
			finally
			{
				// Catch and release to execute finally{}
				// end catch
				try
				{
					@out.Close();
				}
				catch (Exception)
				{
				}
			}
		}

		/// <summary>
		/// A
		/// <see cref="InputStream">InputStream</see>
		/// will read data from another
		/// <tt>java.io.InputStream</tt>, given in the constructor,
		/// and encode/decode to/from Base64 notation on the fly.
		/// </summary>
		/// <seealso cref="Base64">Base64</seealso>
		/// <since>1.3</since>
		public class InputStream : FilterInputStream
		{
			private bool encode;

			private int position;

			private byte[] buffer;

			private int bufferLength;

			private int numSigBytes;

			private int lineLength;

			private bool breakLines;

			private int options;

			private byte[] decodabet;

			/// <summary>
			/// Constructs a
			/// <see cref="InputStream">InputStream</see>
			/// in DECODE mode.
			/// </summary>
			/// <param name="in">the <tt>java.io.InputStream</tt> from which to read data.</param>
			/// <since>1.3</since>
			public InputStream(System.IO.InputStream @in) : this(@in, Decode)
			{
			}

			/// <summary>
			/// Constructs a
			/// <see cref="InputStream">InputStream</see>
			/// in
			/// either ENCODE or DECODE mode.
			/// <p>
			/// Valid options:<pre>
			/// ENCODE or DECODE: Encode or Decode as data is read.
			/// DO_BREAK_LINES: break lines at 76 characters
			/// (only meaningful when encoding)</i>
			/// </pre>
			/// <p>
			/// Example: <code>new Base64.InputStream( in, Base64.DECODE )</code>
			/// </summary>
			/// <param name="in">the <tt>java.io.InputStream</tt> from which to read data.</param>
			/// <param name="options">Specified options</param>
			/// <seealso cref="Base64.Encode">Base64.Encode</seealso>
			/// <seealso cref="Base64.Decode">Base64.Decode</seealso>
			/// <seealso cref="Base64.DoBreakLines">Base64.DoBreakLines</seealso>
			/// <since>2.0</since>
			public InputStream(System.IO.InputStream @in, int options) : base(@in)
			{
				// end finally
				// end decodeFileToFile
				// Encoding or decoding
				// Current position in the buffer
				// Small buffer holding converted data
				// Length of buffer (3 or 4)
				// Number of meaningful bytes in the buffer
				// Break lines at less than 80 characters
				// Record options used to create the stream.
				// Local copies to avoid extra method calls
				// end constructor
				this.options = options;
				// Record for later
				this.breakLines = (options & DoBreakLines) > 0;
				this.encode = (options & Encode) > 0;
				this.bufferLength = encode ? 4 : 3;
				this.buffer = new byte[bufferLength];
				this.position = -1;
				this.lineLength = 0;
				this.decodabet = GetDecodabet(options);
			}

			// end constructor
			/// <summary>
			/// Reads enough of the input stream to convert
			/// to/from Base64 and returns the next byte.
			/// </summary>
			/// <remarks>
			/// Reads enough of the input stream to convert
			/// to/from Base64 and returns the next byte.
			/// </remarks>
			/// <returns>next byte</returns>
			/// <since>1.3</since>
			/// <exception cref="System.IO.IOException"></exception>
			public override int Read()
			{
				// Do we need to get data?
				if (position < 0)
				{
					if (encode)
					{
						byte[] b3 = new byte[3];
						int numBinaryBytes = 0;
						for (int i = 0; i < 3; i++)
						{
							int b = @in.Read();
							// If end of stream, b is -1.
							if (b >= 0)
							{
								b3[i] = unchecked((byte)b);
								numBinaryBytes++;
							}
							else
							{
								break;
							}
						}
						// out of for loop
						// end else: end of stream
						// end for: each needed input byte
						if (numBinaryBytes > 0)
						{
							Encode3to4(b3, 0, numBinaryBytes, buffer, 0, options);
							position = 0;
							numSigBytes = 4;
						}
						else
						{
							// end if: got data
							return -1;
						}
					}
					else
					{
						// Must be end of stream
						// end else
						// end if: encoding
						// Else decoding
						byte[] b4 = new byte[4];
						int i = 0;
						for (i = 0; i < 4; i++)
						{
							// Read four "meaningful" bytes:
							int b = 0;
							do
							{
								b = @in.Read();
							}
							while (b >= 0 && ((sbyte)decodabet[b & unchecked((int)(0x7f))]) <= WhiteSpaceEnc);
							if (b < 0)
							{
								break;
							}
							// Reads a -1 if end of stream
							// end if: end of stream
							b4[i] = unchecked((byte)b);
						}
						// end for: each needed input byte
						if (i == 4)
						{
							numSigBytes = Decode4to3(b4, 0, buffer, 0, options);
							position = 0;
						}
						else
						{
							// end if: got four characters
							if (i == 0)
							{
								return -1;
							}
							else
							{
								// end else if: also padded correctly
								// Must have broken out from above.
								throw new IOException("Improperly padded Base64 input.");
							}
						}
					}
				}
				// end
				// end else: decode
				// end else: get data
				// Got data?
				if (position >= 0)
				{
					// End of relevant data?
					if (position >= numSigBytes)
					{
						return -1;
					}
					// end if: got data
					if (encode && breakLines && lineLength >= MaxLineLength)
					{
						lineLength = 0;
						return '\n';
					}
					else
					{
						// end if
						lineLength++;
						// This isn't important when decoding
						// but throwing an extra "if" seems
						// just as wasteful.
						int b = buffer[position++];
						if (position >= bufferLength)
						{
							position = -1;
						}
						// end if: end
						return b & unchecked((int)(0xFF));
					}
				}
				else
				{
					// This is how you "cast" a byte that's
					// intended to be unsigned.
					// end else
					// end if: position >= 0
					// Else error
					throw new IOException("Error in Base64 code reading stream.");
				}
			}

			// end else
			// end read
			/// <summary>
			/// Calls
			/// <see cref="Read()">Read()</see>
			/// repeatedly until the end of stream
			/// is reached or <var>len</var> bytes are read.
			/// Returns number of bytes read into array or -1 if
			/// end of stream is encountered.
			/// </summary>
			/// <param name="dest">array to hold values</param>
			/// <param name="off">offset for array</param>
			/// <param name="len">max number of bytes to read into array</param>
			/// <returns>bytes read into array or -1 if end of stream is encountered.</returns>
			/// <since>1.3</since>
			/// <exception cref="System.IO.IOException"></exception>
			public override int Read(byte[] dest, int off, int len)
			{
				int i;
				int b;
				for (i = 0; i < len; i++)
				{
					b = Read();
					if (b >= 0)
					{
						dest[off + i] = unchecked((byte)b);
					}
					else
					{
						if (i == 0)
						{
							return -1;
						}
						else
						{
							break;
						}
					}
				}
				// Out of 'for' loop
				// Out of 'for' loop
				// end for: each byte read
				return i;
			}
			// end read
		}

		/// <summary>
		/// A
		/// <see cref="OutputStream">OutputStream</see>
		/// will write data to another
		/// <tt>java.io.OutputStream</tt>, given in the constructor,
		/// and encode/decode to/from Base64 notation on the fly.
		/// </summary>
		/// <seealso cref="Base64">Base64</seealso>
		/// <since>1.3</since>
		public class OutputStream : FilterOutputStream
		{
			private bool encode;

			private int position;

			private byte[] buffer;

			private int bufferLength;

			private int lineLength;

			private bool breakLines;

			private byte[] b4;

			private bool suspendEncoding;

			private int options;

			private byte[] decodabet;

			/// <summary>
			/// Constructs a
			/// <see cref="OutputStream">OutputStream</see>
			/// in ENCODE mode.
			/// </summary>
			/// <param name="out">the <tt>java.io.OutputStream</tt> to which data will be written.
			/// 	</param>
			/// <since>1.3</since>
			public OutputStream(System.IO.OutputStream @out) : this(@out, Encode)
			{
			}

			/// <summary>
			/// Constructs a
			/// <see cref="OutputStream">OutputStream</see>
			/// in
			/// either ENCODE or DECODE mode.
			/// <p>
			/// Valid options:<pre>
			/// ENCODE or DECODE: Encode or Decode as data is read.
			/// DO_BREAK_LINES: don't break lines at 76 characters
			/// (only meaningful when encoding)</i>
			/// </pre>
			/// <p>
			/// Example: <code>new Base64.OutputStream( out, Base64.ENCODE )</code>
			/// </summary>
			/// <param name="out">the <tt>java.io.OutputStream</tt> to which data will be written.
			/// 	</param>
			/// <param name="options">Specified options.</param>
			/// <seealso cref="Base64.Encode">Base64.Encode</seealso>
			/// <seealso cref="Base64.Decode">Base64.Decode</seealso>
			/// <seealso cref="Base64.DoBreakLines">Base64.DoBreakLines</seealso>
			/// <since>1.3</since>
			public OutputStream(System.IO.OutputStream @out, int options) : base(@out)
			{
				// end inner class InputStream
				// Scratch used in a few places
				// Record for later
				// Local copies to avoid extra method calls
				// end constructor
				this.breakLines = (options & DoBreakLines) != 0;
				this.encode = (options & Encode) != 0;
				this.bufferLength = encode ? 3 : 4;
				this.buffer = new byte[bufferLength];
				this.position = 0;
				this.lineLength = 0;
				this.suspendEncoding = false;
				this.b4 = new byte[4];
				this.options = options;
				this.decodabet = GetDecodabet(options);
			}

			// end constructor
			/// <summary>
			/// Writes the byte to the output stream after
			/// converting to/from Base64 notation.
			/// </summary>
			/// <remarks>
			/// Writes the byte to the output stream after
			/// converting to/from Base64 notation.
			/// When encoding, bytes are buffered three
			/// at a time before the output stream actually
			/// gets a write() call.
			/// When decoding, bytes are buffered four
			/// at a time.
			/// </remarks>
			/// <param name="theByte">the byte to write</param>
			/// <since>1.3</since>
			/// <exception cref="System.IO.IOException"></exception>
			public override void Write(int theByte)
			{
				// Encoding suspended?
				if (suspendEncoding)
				{
					this.@out.Write(theByte);
					return;
				}
				// end if: supsended
				// Encode?
				if (encode)
				{
					buffer[position++] = unchecked((byte)theByte);
					if (position >= bufferLength)
					{
						// Enough to encode.
						this.@out.Write(Encode3to4(b4, buffer, bufferLength, options));
						lineLength += 4;
						if (breakLines && lineLength >= MaxLineLength)
						{
							this.@out.Write(NewLine);
							lineLength = 0;
						}
						// end if: end of line
						position = 0;
					}
				}
				else
				{
					// end if: enough to output
					// end if: encoding
					// Else, Decoding
					// Meaningful Base64 character?
					if (decodabet[theByte & unchecked((int)(0x7f))] > WhiteSpaceEnc)
					{
						buffer[position++] = unchecked((byte)theByte);
						if (position >= bufferLength)
						{
							// Enough to output.
							int len = Base64.Decode4to3(buffer, 0, b4, 0, options);
							@out.Write(b4, 0, len);
							position = 0;
						}
					}
					else
					{
						// end if: enough to output
						// end if: meaningful base64 character
						if (decodabet[theByte & unchecked((int)(0x7f))] != WhiteSpaceEnc)
						{
							throw new IOException("Invalid character in Base64 data.");
						}
					}
				}
			}

			// end else: not white space either
			// end else: decoding
			// end write
			/// <summary>
			/// Calls
			/// <see cref="Write(int)">Write(int)</see>
			/// repeatedly until <var>len</var>
			/// bytes are written.
			/// </summary>
			/// <param name="theBytes">array from which to read bytes</param>
			/// <param name="off">offset for array</param>
			/// <param name="len">max number of bytes to read into array</param>
			/// <since>1.3</since>
			/// <exception cref="System.IO.IOException"></exception>
			public override void Write(byte[] theBytes, int off, int len)
			{
				// Encoding suspended?
				if (suspendEncoding)
				{
					this.@out.Write(theBytes, off, len);
					return;
				}
				// end if: supsended
				for (int i = 0; i < len; i++)
				{
					Write(theBytes[off + i]);
				}
			}

			// end for: each byte written
			// end write
			/// <summary>Method added by PHIL.</summary>
			/// <remarks>
			/// Method added by PHIL. [Thanks, PHIL. -Rob]
			/// This pads the buffer without closing the stream.
			/// </remarks>
			/// <exception cref="System.IO.IOException">if there's an error.</exception>
			public virtual void FlushBase64()
			{
				if (position > 0)
				{
					if (encode)
					{
						@out.Write(Encode3to4(b4, buffer, position, options));
						position = 0;
					}
					else
					{
						// end if: encoding
						throw new IOException("Base64 input not properly padded.");
					}
				}
			}

			// end else: decoding
			// end if: buffer partially full
			// end flush
			/// <summary>Flushes and closes (I think, in the superclass) the stream.</summary>
			/// <remarks>Flushes and closes (I think, in the superclass) the stream.</remarks>
			/// <since>1.3</since>
			/// <exception cref="System.IO.IOException"></exception>
			public override void Close()
			{
				// 1. Ensure that pending characters are written
				FlushBase64();
				// 2. Actually close the stream
				// Base class both flushes and closes.
				base.Close();
				buffer = null;
				@out = null;
			}

			// end close
			/// <summary>Suspends encoding of the stream.</summary>
			/// <remarks>
			/// Suspends encoding of the stream.
			/// May be helpful if you need to embed a piece of
			/// base64-encoded data in a stream.
			/// </remarks>
			/// <exception cref="System.IO.IOException">if there's an error flushing</exception>
			/// <since>1.5.1</since>
			public virtual void SuspendEncoding()
			{
				FlushBase64();
				this.suspendEncoding = true;
			}

			// end suspendEncoding
			/// <summary>Resumes encoding of the stream.</summary>
			/// <remarks>
			/// Resumes encoding of the stream.
			/// May be helpful if you need to embed a piece of
			/// base64-encoded data in a stream.
			/// </remarks>
			/// <since>1.5.1</since>
			public virtual void ResumeEncoding()
			{
				this.suspendEncoding = false;
			}
			// end resumeEncoding
		}
		// end inner class OutputStream
	}
}
