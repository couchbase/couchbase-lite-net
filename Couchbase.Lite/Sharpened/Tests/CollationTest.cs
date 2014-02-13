//
// CollationTest.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
using Couchbase.Lite;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class CollationTest : LiteTestCase
	{
		public static string Tag = "Collation";

		private const int kTDCollateJSON_Unicode = 0;

		private const int kTDCollateJSON_Raw = 1;

		private const int kTDCollateJSON_ASCII = 2;

		// create the same JSON encoding used by TouchDB
		// this lets us test comparisons as they would be encoded
		public virtual string Encode(object obj)
		{
			ObjectWriter mapper = new ObjectWriter();
			try
			{
				byte[] bytes = mapper.WriteValueAsBytes(obj);
				string result = Sharpen.Runtime.GetStringForBytes(bytes);
				return result;
			}
			catch (Exception e)
			{
				Log.E(Tag, "Error encoding JSON", e);
				return null;
			}
		}

		public virtual void TestCollateScalars()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "true", 
				0, "false"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "false"
				, 0, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "null"
				, 0, "17"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "123", 
				0, "1"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "123", 
				0, "0123.0"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "123", 
				0, "\"123\""));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\""
				, 0, "\"123\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\""
				, 0, "\"1235\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\""
				, 0, "\"1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"12\\/34\""
				, 0, "\"12/34\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"\\/1234\""
				, 0, "\"/1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\\/\""
				, 0, "\"1234/\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"a\""
				, 0, "\"A\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"A\""
				, 0, "\"aa\""));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "\"B\""
				, 0, "\"aa\""));
		}

		public virtual void TestCollateASCII()
		{
			int mode = kTDCollateJSON_ASCII;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "true", 
				0, "false"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "false"
				, 0, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "null"
				, 0, "17"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "123", 
				0, "1"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "123", 
				0, "0123.0"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "123", 
				0, "\"123\""));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\""
				, 0, "\"123\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\""
				, 0, "\"1235\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\""
				, 0, "\"1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"12\\/34\""
				, 0, "\"12/34\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"\\/1234\""
				, 0, "\"/1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "\"1234\\/\""
				, 0, "\"1234/\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"A\""
				, 0, "\"a\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"B\""
				, 0, "\"a\""));
		}

		public virtual void TestCollateRaw()
		{
			int mode = kTDCollateJSON_Raw;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "false"
				, 0, "17"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "false"
				, 0, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "null"
				, 0, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "[\"A\"]"
				, 0, "\"A\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "\"A\""
				, 0, "\"a\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "[\"b\"]"
				, 0, "[\"b\",\"c\",\"a\"]"));
		}

		public virtual void TestCollateArrays()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "[]", 0
				, "\"foo\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "[]", 0
				, "[]"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, "[true]"
				, 0, "[true]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "[false]"
				, 0, "[null]"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "[]", 
				0, "[null]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "[123]"
				, 0, "[45]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "[123]"
				, 0, "[45,67]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "[123.4,\"wow\"]"
				, 0, "[123.40,789]"));
		}

		public virtual void TestCollateNestedArray()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSON(mode, 0, "[[]]", 
				0, "[]"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, "[1,[2,3],4]"
				, 0, "[1,[2,3.1],4,5,6]"));
		}

		public virtual void TestCollateUnicodeStrings()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSON(mode, 0, Encode(
				"frÔøΩd"), 0, Encode("frÔøΩd")));
			// Assert.assertEquals(1, TDCollateJSON.testCollateJSON(mode, 0, encode("ÔøΩmÔøΩ"), 0, encode("omo")));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, Encode
				("\t"), 0, Encode(" ")));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSON(mode, 0, Encode
				("\x1"), 0, Encode(" ")));
		}

		public virtual void TestConvertEscape()
		{
			NUnit.Framework.Assert.AreEqual('\\', TDCollateJSON.TestEscape("\\\\"));
			NUnit.Framework.Assert.AreEqual('\t', TDCollateJSON.TestEscape("\\t"));
			NUnit.Framework.Assert.AreEqual('E', TDCollateJSON.TestEscape("\\u0045"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestEscape("\\u0001"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestEscape("\\u0000"));
		}

		public virtual void TestDigitToInt()
		{
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestDigitToInt('1'));
			NUnit.Framework.Assert.AreEqual(7, TDCollateJSON.TestDigitToInt('7'));
			NUnit.Framework.Assert.AreEqual(unchecked((int)(0xc)), TDCollateJSON.TestDigitToInt
				('c'));
			NUnit.Framework.Assert.AreEqual(unchecked((int)(0xc)), TDCollateJSON.TestDigitToInt
				('C'));
		}
	}
}
