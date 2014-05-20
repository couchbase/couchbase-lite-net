//
// StringUtils.cs
//
// Author:
// Pasin Suriyentrakorn  <pasin@couchbase.com>
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

namespace Couchbase.Lite.Util
{
    public class StringUtils
    {
        /* Largest possible base 10 exponent.  Any
        * exponent larger than this will already
        * produce underflow or overflow, so there's
        * no need to worry about additional digits.
        */
        private static int maxExponent = 511;

        /* Table giving binary powers of 10.  Entry 
        * is 10^2^i.  Used to convert decimal
        * exponents into floating-point numbers.
        */
        private static double[] powersOf10 = new double[] { 
            1e1, 1e2, 1e4, 1e8, 1e16, 1e32, 1e64, 1e128, 1e256
        };

        /// <summary>
        ///     Parses the Substring interpreting its content as a floating point number and returns 
        ///     its value as a double.
        ///     
        ///     Ported from C language strtod method.
        ///     Original : http://www.opensource.apple.com/source/tcl/tcl-14/tcl/compat/strtod.c
        /// </summary>
        /// <remarks>
        ///     Parses the Substring interpreting its content as a floating point number and returns 
        ///     its value as a double.
        /// </remarks>
        /// <param name="str">Input string.</param>
        /// <param name="start">Starting index of the substring to be converted to a foloting point number.</param>
        /// <param name="endPosition">Output index pointing to the first character after the number.</param>
        /// <returns>
        ///     The double-precision floating-point representation of the characters in string.
        /// </returns>
        /// 
        public static double Strtod(String str, int start, out int endPosition)
        {
            if (str == null)
                throw new ArgumentNullException("str");

            if (start < 0)
                throw new ArgumentOutOfRangeException("start", "Value cannot be negative.");

            if (start > str.Length)
                throw new ArgumentOutOfRangeException("start", "Value must be less then str.Length");

            int sign = 0;
            int expSign = 0;

            double fraction;
            double dblExp;

            int p = start;
            char c;

            /* Exponent read from "EX" field. */
            int exp = 0;

            /* Exponent that derives from the fractional part. Under normal
             * circumstances, it is the negative of the number of digits in F.
             * However, if I is very long, the last digits of I get dropped
             * (otherwise a long I with a large negative exponent could cause an
             * unnecessary overflow on I alone).  In this case, fracExp is
             * incremented one for each dropped digit. */
            int fracExp = 0;

            /* Number of digits in mantissa. */
            int mantSize = 0;

            /* Number of mantissa digits BEFORE decimal point. */
            int decPt;

            /* Temporarily holds location of exponent in string. */
            int pExp;

            int length = str.Length;

            /* Strip off leading blanks and check for a sign */

            while (p < length && char.IsWhiteSpace(str[p]))
                ++p;
                
            if (p >= length) 
            {
                p = 0;
                fraction = 0.0;
                goto Done;
            }

            if (str[p] == '-') 
            {
                sign = 1;
                ++p;
            } 
            else if (str[p] == '+')
                ++p;

            decPt = -1;
            for (mantSize = 0; ; ++mantSize) {
                if (p >= length) break;

                if (! Char.IsNumber(str[p])) {
                    if (str[p] != '.' || decPt >= 0)
                        break;
                    decPt = mantSize;
                }
                ++p;
            }

            /*
            * Now suck up the digits in the mantissa.  Use two integers to
            * collect 9 digits each (this is faster than using floating-point).
            * If the mantissa has more than 18 digits, ignore the extras, since
            * they can't affect the value anyway.
            */

            pExp = p;
            p -= mantSize;
            if (decPt < 0)
                decPt = mantSize;
            else
                --mantSize; /* One of the digits was the point. */

            /*
            // TODO: Revise this limitation
            if (mantSize > 18) 
                mantSize = 18;
            */

            fracExp = decPt - mantSize;
                
            if (mantSize == 0) 
            {
                fraction = 0.0;
                p = 0;
                goto Done;
            } else {
                int frac1 = 0;
                for (; mantSize > 9; --mantSize) 
                {
                    c = str[p];
                    ++p;

                    if (c == '.')
                    {
                        c = str[p];
                        ++p;
                    }
                        
                    frac1 = 10 * frac1 + (c - '0');
                }

                int frac2 = 0;
                for (; mantSize > 0; --mantSize) 
                {
                    c = str[p];
                    ++p;

                    if (c == '.')
                    {
                        c = str[p];
                        ++p;
                    }
                        
                    frac2 = 10 * frac2 + (c - '0');
                }

                fraction = (double) ((1.0e9 * frac1) + frac2);
            }

            /* Skim off the exponent. */
            p = pExp;
            if (p < str.Length && (str[p] == 'E' || str[p] == 'e')) 
            {
                ++p;

                if (p < length)
                {
                    if (str[p] == '-') 
                    {
                        expSign = 1;
                        ++p;
                    } else if (str[p] == '+')
                        ++p;

                    if (!Char.IsDigit(str[p])) {
                        p = pExp;
                        goto Done;
                    }

                    while (p < length && Char.IsDigit(str[p]))
                    {
                        exp = exp * 10 + (str[p] - '0');
                        ++p;
                    }
                        
                }
            }

            if (expSign != 0)
                exp = fracExp - exp;
            else
                exp = fracExp + exp;

            /*
            * Generate a floating-point number that represents the exponent.
            * Do this by processing the exponent one bit at a time to combine
            * many powers of 2 of 10. Then combine the exponent with the
            * fraction.
            */
            if (exp < 0) 
            {
                expSign = 1;
                exp = -exp;
            } 
            else
                expSign = 0;

            if (exp > maxExponent)
                exp = maxExponent;

            dblExp = 1.0;
            for (int i = 0; exp != 0; exp >>= 1, ++i) 
            {
                if ((exp & 01) != 0)
                    dblExp *= powersOf10[i];
            }
                
            if (expSign != 0)
                fraction /= dblExp;
            else
                fraction *= dblExp;
                
        Done: 
            endPosition = p;
            return sign == 1 ? -fraction : fraction;  
        }

        /// <summary>
        /// Converts an un-padded base64 encoded string into a byte array.
        /// </summary>
        /// <returns>The from unpadded base64 string.</returns>
        /// <remarks>
        /// Ensures that Base64 encoded strings from other platforms have the padding that .NET expects.
        /// </remarks>
        /// <param name="base64String">Base64 string.</param>
        internal static byte[] ConvertFromUnpaddedBase64String (string base64String)
        {
            var strLength = base64String.Length;
            var paddedNewContentBase64 = base64String.PadRight (strLength + strLength % 4, '=');
            var newContents = Convert.FromBase64String (paddedNewContentBase64);
            return newContents;
        }
    }
}
