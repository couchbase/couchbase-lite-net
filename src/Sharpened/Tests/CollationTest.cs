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
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "true"
				, "false"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "false"
				, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "null"
				, "17"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "123"
				, "1"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "123"
				, "0123.0"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "123"
				, "\"123\""));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\""
				, "\"123\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\""
				, "\"1235\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\""
				, "\"1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"12\\/34\""
				, "\"12/34\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"\\/1234\""
				, "\"/1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\\/\""
				, "\"1234/\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"a\""
				, "\"A\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"A\""
				, "\"aa\""));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"B\""
				, "\"aa\""));
		}

		public virtual void TestCollateASCII()
		{
			int mode = kTDCollateJSON_ASCII;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "true"
				, "false"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "false"
				, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "null"
				, "17"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "123"
				, "1"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "123"
				, "0123.0"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "123"
				, "\"123\""));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\""
				, "\"123\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\""
				, "\"1235\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\""
				, "\"1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"12\\/34\""
				, "\"12/34\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"\\/1234\""
				, "\"/1234\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "\"1234\\/\""
				, "\"1234/\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"A\""
				, "\"a\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"B\""
				, "\"a\""));
		}

		public virtual void TestCollateRaw()
		{
			int mode = kTDCollateJSON_Raw;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "false"
				, "17"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "false"
				, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "null"
				, "true"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "[\"A\"]"
				, "\"A\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "\"A\""
				, "\"a\""));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "[\"b\"]"
				, "[\"b\",\"c\",\"a\"]"));
		}

		public virtual void TestCollateArrays()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "[]"
				, "\"foo\""));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "[]"
				, "[]"));
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, "[true]"
				, "[true]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "[false]"
				, "[null]"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "[]"
				, "[null]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "[123]"
				, "[45]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "[123]"
				, "[45,67]"));
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "[123.4,\"wow\"]"
				, "[123.40,789]"));
		}

		public virtual void TestCollateNestedArray()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(1, TDCollateJSON.TestCollateJSONWrapper(mode, "[[]]"
				, "[]"));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, "[1,[2,3],4]"
				, "[1,[2,3.1],4,5,6]"));
		}

		public virtual void TestCollateUnicodeStrings()
		{
			int mode = kTDCollateJSON_Unicode;
			NUnit.Framework.Assert.AreEqual(0, TDCollateJSON.TestCollateJSONWrapper(mode, Encode
				("frÔøΩd"), Encode("frÔøΩd")));
			// Assert.assertEquals(1, TDCollateJSON.testCollateJSONWrapper(mode, encode("ÔøΩmÔøΩ"), encode("omo")));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, Encode
				("\t"), Encode(" ")));
			NUnit.Framework.Assert.AreEqual(-1, TDCollateJSON.TestCollateJSONWrapper(mode, Encode
				("\x1"), Encode(" ")));
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

		public virtual void TestCollateRevIds()
		{
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("1-foo", "1-foo"), 
				0);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("2-bar", "1-foo"), 
				1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("1-foo", "2-bar"), 
				-1);
			// Multi-digit:
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("123-bar", "456-foo"
				), -1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("456-foo", "123-bar"
				), 1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("456-foo", "456-foo"
				), 0);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("456-foo", "456-foofoo"
				), -1);
			// Different numbers of digits:
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("89-foo", "123-bar"
				), -1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("123-bar", "89-foo"
				), 1);
			// Edge cases:
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("123-", "89-"), 1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("123-a", "123-a"), 
				0);
			// Invalid rev IDs:
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("-a", "-b"), -1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("-", "-"), 0);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds(string.Empty, string.Empty
				), 0);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds(string.Empty, "-b")
				, -1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("bogus", "yo"), -1);
			NUnit.Framework.Assert.AreEqual(RevCollator.TestCollateRevIds("bogus-x", "yo-y"), 
				-1);
		}
	}
}
