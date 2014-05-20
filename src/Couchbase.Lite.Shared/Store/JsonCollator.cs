// 
// JsonCollator.cs
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
using System.Text;
using System.Globalization;
using Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    public enum JsonCollationMode 
    {
        Unicode,
        Ascii,
        Raw
    }

    public class JsonCollator
    {
        // Types of values, ordered according to CouchDB collation order (see view_collation.js tests)
        enum ValueType
        {
            EndArray = 0,
            EndObject = 1,
            Comma = 2,
            Colon = 3,
            Null = 4,
            False = 5,
            True = 6,
            Number = 7,
            String = 8,
            Array = 9,
            Object = 10,
            Illegal = 11
        }

        private static readonly int[] rawOrderOfValueType = {
            -4, -3, -2, -1, 2, 1, 3, 0, 6, 5, 4, 7
        };

        private static ValueType ValueTypeOf(char c) {
            switch (c) 
            {
            case 'n': 
                return ValueType.Null;
            case 'f': 
                return ValueType.False;
            case 't': 
                return ValueType.True;
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
            case '-':
                return ValueType.Number;
            case '"':           
                return ValueType.String;
            case ']':           
                return ValueType.EndArray;
            case '}':           
                return ValueType.EndObject;
            case ',':           
                return ValueType.Comma;
            case ':':           
                return ValueType.Colon;
            case '[':           
                return ValueType.Array;
            case '{':           
                return ValueType.Object;
            default:
                // TODO :Warn(@"Unexpected character '%c' parsing JSON", c);
                return ValueType.Illegal;
            }
        }

        private static int RawOrderOfValueType(ValueType type) 
        {
            return rawOrderOfValueType[(int)type];
        }

        private static int Cmp(int n1, int n2)
        {
            int diff = n1 - n2;
            return diff > 0 ? 1 : (diff < 0 ? -1 : 0);
        }

        private static int DCmp(double n1, double n2)
        {
            double diff = n1 - n2;
            return diff > 0.0 ? 1 : (diff < 0.0 ? -1 : 0);
        }

        private static double ReadNumber(String str, int start, out int endOfNumber) 
        {
            return StringUtils.Strtod (str, start, out endOfNumber);
        }

        private static int DigitToInt(char ch) 
        {
            int d = ch - '0';
            if (d < 10) 
                return d;

            d = ch - 'a';
            if (d < 6) 
                return d + 10;

            d = ch - 'A';
            if (d < 6) 
                return d + 10;

            return -1;
        }
            
        internal static char ConvertEscape(String str, int start, out int endPos) 
        {
            var index = start + 1;

            if (index >= str.Length) 
            {
                endPos = index;
                return '\0';
            }

            var c = str[index];

            switch (c) {
            case 'u':
                // \u is a Unicode escape; 4 hex digits follow.
                int uIndex = index + 1;

                // TODO: Check Valid Digit
                if (uIndex + 3 < str.Length) 
                {
                    int uc = (DigitToInt(str[uIndex + 0]) << 12) |
                        (DigitToInt(str[uIndex + 1]) << 8) |
                        (DigitToInt(str[uIndex + 2]) << 4) |
                        (DigitToInt(str[uIndex + 3]));

                    if (uc > 127) 
                    {
                        // TODO: Warn(@"CBLCollateJSON can't correctly compare \\u%.4s", digits);
                    }

                    endPos = uIndex + 3;
                    return (char)uc;
                } 
                break;
            case 'b': 
                c = '\b';
                break;
            case 'n':
                c = '\n';
                break;
            case 'r':
                c = '\r';
                break;
            case 't':
                c = '\t';
                break;
            }
            endPos = index;
            return c;
        }

        private static String CreateStringFromJSON(String str, int start, out int endPos) 
        {
            var sb = new StringBuilder();

            var index = ++start;
            for (; index < str.Length; ++index) 
            {
                var c = str [index];
                if (c == '"')
                    break;
                if (c == '\\') 
                {
                    int endEscapePos;
                    c = ConvertEscape (str, index, out endEscapePos);
                    index = endEscapePos;
                    sb.Append(c);
                } 
                else
                    sb.Append(c);
            }

            endPos = index < str.Length ? index : index - 1;

            return sb.ToString();
        }

        private static int CompareStringsAscii(String str1, int start1, out int endPos1, 
            String str2, int start2, out int endPos2) {

            var result = 0;

            var index1 = start1;
            var index2 = start2;

            while (true) 
            {
                ++index1;
                ++index2;

                var c1 = index1 < str1.Length ? str1 [index1] : '\0';
                var c2 = index2 < str2.Length ? str2 [index2] : '\0';

                // If one string ends, the other is greater; if both end, they're equal:
                if (c1 == '"') 
                {
                    if (c2 == '"')
                        break;
                    else 
                    {
                        result = -1;
                        break;
                    }
                } 
                else if (c2 == '"') 
                {
                    result = 1;
                } 
                else if (c1 == '\0') 
                {
                    if (c2 == '\0')
                        break;
                    else
                        result = -1;
                }

                if (c1 == '\\') 
                {
                    int endEsc1;
                    c1 = ConvertEscape (str1, index1, out endEsc1);
                    index1 = endEsc1;
                }

                if (c2 == '\\') 
                {
                    int endEsc2;
                    c2 = ConvertEscape (str2, index2, out endEsc2);
                    index2 = endEsc2;
                }

                int s = Cmp (c1, c2);

                if (s != 0) {
                    // TODO: Check endPos1 and endPos2
                    endPos1 = index1;
                    endPos2 = index2;
                    return s;
                }
                    
            }

            endPos1 = index1;
            endPos2 = index2;

            return result;
        }

        private static int CompareStringsUnicode(String str1, int start1, out int endPos1, 
            String str2, int start2, out int endPos2) 
        {
            var s1 = CreateStringFromJSON(str1, start1, out endPos1);
            var s2 = CreateStringFromJSON(str2, start2, out endPos2);

            // TODO: Detect current localization and use the corresponding CompareInfo
            var comp = CultureInfo.InvariantCulture.CompareInfo;
            var sk1 = comp.GetSortKey(s1);
            var sk2 = comp.GetSortKey(s2);
            return SortKey.Compare(sk1, sk2);
        }

        ///
        /// <summary>
        ///     Compare two JSON Strings.
        /// </summary>
        /// <remarks>
        ///     The comparison result is based on the specification linked below.
        ///     http://wiki.apache.org/couchdb/View_collation#Collation_Specification.
        /// 
        ///     The method assumes that both input JSON strings parameters do not include 
        ///     any whitespace characters between each part of the JSON components.
        /// </remarks>
        /// <param name="mode">
        ///     JsonCollationMode.Unicode: Unicode JSON String
        ///     JsonCollationMode.Ascii: ASCII JSON String
        ///     JsonCollationMode.Raw:  Raw JSON String
        /// </param>
        /// <param name="param1">the first json string to compare.</param>
        /// <param name="param2">the second json string to compare.</param>
        /// <param name="arrayLimit">the maximum number of the array element inside the json string to compare.</param>
        /// <returns>
        ///     The value 0 if the param1 string is equal to the param2 string; 
        ///     a value -1 if the param1 is less than the param2 string; 
        ///     a value 1 if this string is greater than the string argument.
        /// </returns>
        /// 
        public static int Compare(JsonCollationMode mode, String param1, String param2, int arrayLimit)
        {
            int index1 = 0;
            int index2 = 0;
            int depth = 0;
            int arrayIndex = 0;
            int diff;

            do 
            {
                var type1 = ValueTypeOf(index1 < param1.Length ? param1[index1] : '\0');
                var type2 = ValueTypeOf(index2 < param2.Length ? param2[index2] : '\0');

                if (type1 != type2)
                {
                    if (depth == 1 && (type1 == ValueType.Comma || type2 == ValueType.Comma))
                    {
                        if (++arrayIndex >= arrayLimit) 
                            return 0;
                    }

                    if (mode != JsonCollationMode.Raw)
                        return Cmp((int)type1, (int)type2);
                    else
                        return Cmp(RawOrderOfValueType(type1), RawOrderOfValueType(type2));
                }
                else 
                {
                    switch(type1)
                    {
                    case ValueType.Null:
                    case ValueType.True:
                        index1 += 4;
                        index2 += 4;
                        break;
                    case ValueType.False:
                        index1 += 5;
                        index2 += 5;
                        break;
                    case ValueType.Number:
                        int next1;
                        int next2;
                        diff = DCmp(ReadNumber(param1, index1, out next1), 
                            ReadNumber(param2, index2, out next2));
                        if (diff != 0) 
                            return diff; 

                        index1 = next1;
                        index2 = next2;

                        break;
                    case ValueType.String:
                        int endPos1;
                        int endPos2;
                        if (mode == JsonCollationMode.Unicode)
                            diff = CompareStringsUnicode(param1, index1, out endPos1, param2, index2, out endPos2);
                        else
                            diff = CompareStringsAscii(param1, index1, out endPos1, param2, index2, out endPos2);

                        if (diff != 0) 
                            return diff;

                        index1 = endPos1 + 1;
                        index2 = endPos2 + 1;

                        break;
                    case ValueType.Array:
                    case ValueType.Object:
                        ++index1;
                        ++index2;
                        ++depth;
                        break;
                    case ValueType.EndArray:
                    case ValueType.EndObject:
                        ++index1;
                        ++index2;
                        --depth;
                        break;
                    case ValueType.Comma:
                        if (depth == 1 && (++arrayIndex >= arrayLimit))
                            return 0;
                        ++index1;
                        ++index2;
                        break;
                    case ValueType.Colon:
                        ++index1;
                        ++index2;
                        break;
                    case ValueType.Illegal:
                        return 0;
                    }
                }

            } while (depth > 0);

            return 0;
        }
    }
}
