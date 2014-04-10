using System;
using NUnit.Framework;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
	public class CollationTest : LiteTestCase
	{

		private static void TestStrtodEquals(string input, double expectedValue)
		{
			int position = 0;
			double number = StringUtils.Strtod (input, 0, out position);

			Assert.AreEqual(expectedValue, number);
			Assert.AreEqual(input.Length, position);
		}

		private static void TestStrtodEquals(string input, int start, 
			double expectedValue, int expectedPosition)
		{
			int position = 0;
			double number = StringUtils.Strtod (input, start, out position);

			Assert.AreEqual(expectedValue, number);
			Assert.AreEqual(expectedPosition, position);
		}

		[Test]
		public void TestStrtodValidFormat()
		{
			TestStrtodEquals("0", 0.0);
			TestStrtodEquals("0.0", 0.0);
			TestStrtodEquals("1", 1.0);
			TestStrtodEquals("1.0", 1.0);
			TestStrtodEquals("20.0", 20.0);
			TestStrtodEquals(".2", 0.2);
			TestStrtodEquals("1e1", 10.0);
			TestStrtodEquals(".2e1", 2.0);
			TestStrtodEquals("1.2e1", 12.0);
			TestStrtodEquals("+1", 1.0);
			TestStrtodEquals("+.2", 0.2);
			TestStrtodEquals("+1e1", 10.0);
			TestStrtodEquals("+.2e1", 2.0);
			TestStrtodEquals("+1.2e1", 12.0);
			TestStrtodEquals("+1e+1", 10.0);
			TestStrtodEquals("+.2e+1", 2.0);
			TestStrtodEquals("+1.2e+1", 12.0);
			TestStrtodEquals("-0", 0.0);
			TestStrtodEquals("-123", -123.0);
			TestStrtodEquals("-123.123", -123.123);
			TestStrtodEquals("0123", 123.0);
			TestStrtodEquals("00000000000000000000000000000000000000000000000000123", 123.0);
		}

		[Test]
		public void TestStrtodStartEndPosition()
		{
			TestStrtodEquals("###0.00###", 3, 0.0, 7);
			TestStrtodEquals("###+0.0###", 3, 0.0, 7);
			TestStrtodEquals("###-0.0###", 3, 0.0, 7);
			TestStrtodEquals("###+0e0###", 3, 0.0, 7);
			TestStrtodEquals("###-0e0###", 3, 0.0, 7);
			TestStrtodEquals("###0e-0###", 3, 0.0, 7);
			TestStrtodEquals("###.0e0.##", 3, 0.0, 7);
			TestStrtodEquals("###1000###", 3, 1000.0, 7);
			TestStrtodEquals("###1000.50###", 3, 1000.50, 10);
			TestStrtodEquals("###-1000.50###", 3, -1000.50, 11);
			TestStrtodEquals("   0.00 ##", 3, 0.0, 7);
		}

		private void TestCollateConvertEscape(String input, char decoded)
		{
			//TODO : How to test private methods.
			int endPos;
            char result = JsonCollator.ConvertEscape (input, 0, out endPos);

			Assert.AreEqual(decoded, result);
			Assert.AreEqual(input.Length - 1, endPos);
		}

		[Test]
		public void TestCollateConvertEscape() 
		{
			TestCollateConvertEscape("\\\\",    '\\');
			TestCollateConvertEscape("\\t",     '\t');
			TestCollateConvertEscape("\\u0045", 'E');
			TestCollateConvertEscape("\\u0001", (char)1);
			TestCollateConvertEscape("\\u0000", (char)0);
		}

		private static void TestCollateEquals(JsonCollationMode mode, String input1, String input2, int arrayLimit, int expectedValue) 
		{
            int result = JsonCollator.Compare(mode, input1, input2, arrayLimit);
			Assert.AreEqual(expectedValue, result);
		} 

		private static void TestCollateEquals(JsonCollationMode mode, String input1, String input2, int expectedValue) 
		{
			TestCollateEquals(mode, input1, input2, int.MaxValue, expectedValue);
		}

		private String Encode(String str)
		{
			// TODO: Revise iOS Source Code and implement the same.
			return str;
		}

		[Test]
		public void TestCollateScalars() 
		{
			var mode = JsonCollationMode.Unicode;
			TestCollateEquals(mode, "true", "false", 1);
			TestCollateEquals(mode, "false", "true", -1);
			TestCollateEquals(mode, "null", "17", -1);
			TestCollateEquals(mode, "1", "1", 0);
			TestCollateEquals(mode, "123", "1", 1);
			TestCollateEquals(mode, "123", "0123.0", 0);
			TestCollateEquals(mode, "123", "\"123\"", -1);
			TestCollateEquals(mode, "123", "0123", 0);
			TestCollateEquals(mode, "\"1234\"", "\"123\"", 1);
			TestCollateEquals(mode, "\"123\"", "\"1234\"", -1);
			TestCollateEquals(mode, "\"1234\"", "\"1235\"", -1);
			TestCollateEquals(mode, "\"1234\"", "\"1234\"", 0);
			TestCollateEquals(mode, "\"12\\/34\"", "\"12/34\"", 0);
			TestCollateEquals(mode, "\"\\/1234\"", "\"/1234\"", 0);
			TestCollateEquals(mode, "\"1234\\/\"", "\"1234/\"", 0);
			TestCollateEquals(mode, "123", "00000000000000000000000000000000000000000000000000123", 0);
			TestCollateEquals(mode, "\"a\"", "\"A\"", -1);
			TestCollateEquals(mode, "\"A\"", "\"aa\"", -1);
			TestCollateEquals(mode, "\"B\"", "\"aa\"", 1);
		}

		[Test]
		public void TestCollateJsonAscii() 
		{
			var mode = JsonCollationMode.Ascii;

			TestCollateEquals(mode, "true", "false", 1);
			TestCollateEquals(mode, "false", "true", -1);
			TestCollateEquals(mode, "null", "17", -1);
			TestCollateEquals(mode, "123", "1", 1);
			TestCollateEquals(mode, "123", "0123.0", 0);
			TestCollateEquals(mode, "123", "\"123\"", -1);
			TestCollateEquals(mode, "\"1234\"", "\"123\"", 1);
			TestCollateEquals(mode, "\"123\"", "\"1234\"", -1);
			TestCollateEquals(mode, "\"1234\"", "\"1235\"", -1);
			TestCollateEquals(mode, "\"1234\"", "\"1234\"", 0);
			TestCollateEquals(mode, "\"12\\/34\"", "\"12/34\"", 0);
			TestCollateEquals(mode, "\"12/34\"", "\"12\\/34\"", 0);
			TestCollateEquals(mode, "\"\\/1234\"", "\"/1234\"", 0);
			TestCollateEquals(mode, "\"1234\\/\"", "\"1234/\"", 0);
			TestCollateEquals(mode, "\"A\"", "\"a\"", -1);
			TestCollateEquals(mode, "\"B\"", "\"a\"", -1);
		}

		[Test]
		public void TestCollateJsonRaw()
		{
			var mode = JsonCollationMode.Raw;
			TestCollateEquals(mode, "false", "17", 1);
			TestCollateEquals(mode, "false", "true", -1);
			TestCollateEquals(mode, "null", "true", -1);
			TestCollateEquals(mode, "[\"A\"]", "\"A\"", -1);
			TestCollateEquals(mode, "\"A\"", "\"a\"", -1);
			TestCollateEquals(mode, "[\"b\"]", "[\"b\",\"c\",\"a\"]", -1);
		}
			
		[Test]
		public void TestCollateArrays()
		{
			var mode = JsonCollationMode.Unicode;
			TestCollateEquals(mode, "[]", "\"foo\"", 1);
			TestCollateEquals(mode, "[]", "[]", 0);
			TestCollateEquals(mode, "[true]", "[true]", 0);
			TestCollateEquals(mode, "[false]", "[null]", 1);
			TestCollateEquals(mode, "[]", "[null]", -1);
			TestCollateEquals(mode, "[123]", "[45]", 1);
			TestCollateEquals(mode, "[123]", "[45,67]", 1);
			TestCollateEquals(mode, "[123.4,\"wow\"]", "[123.40,789]", 1);
			TestCollateEquals(mode, "[5,\"wow\"]", "[5,\"wow\"]", 0);
			TestCollateEquals(mode, "[5,\"wow\"]", "1", 1);
			TestCollateEquals(mode, "1", "[5,\"wow\"]", -1);
		}

		[Test]
		public void TestCollateNestedArrays()
		{
			var mode = JsonCollationMode.Unicode;
			TestCollateEquals(mode, "[[]]", "[]", 1);
			TestCollateEquals(mode, "[1,[2,3],4]", "[1,[2,3.1],4,5,6]", -1);
		}

		[Test]
		public void TestCollateJsonUnicode()
		{
			var mode = JsonCollationMode.Unicode;
			TestCollateEquals(mode, Encode("\"fréd\""), Encode("\"fréd\""), 0);
			TestCollateEquals(mode, Encode("\"ømø\""), Encode("\"omo\""), 1);

			//TODO: Comfirm or fix this case.
            //TestCollateEquals(mode, Encode("\"\\t\""), Encode("\" \""), -1);
			TestCollateEquals(mode, Encode("\"\\u0001\""), Encode("\" \""), -1);
		}

		[Test]
		public void TestCollateJsonLimited()
		{
			var mode = JsonCollationMode.Unicode;

			TestCollateEquals(mode, "[5,\"wow\"]", "[4,\"wow\"]", 1, 1);
			TestCollateEquals(mode, "[5,\"wow\"]", "[5,\"wow\"]", 1, 0);
			TestCollateEquals(mode, "[5,\"wow\"]", "[5,\"MOM\"]", 1, 0);
			TestCollateEquals(mode, "[5,\"wow\"]", "[5]", 1, 0);
			TestCollateEquals(mode, "[5,\"wow\"]", "[5,\"MOM\"]", 2, 1);
		}
	}
}

