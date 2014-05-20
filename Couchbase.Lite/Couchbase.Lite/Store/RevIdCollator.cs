// 
// RevIdCollator.cs
//
// Author:
//  Pasin Suriyentrakorn <pasin@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc (http://www.couchbase.com)
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
// JsonCollator is ported from iOS CBLCollateRevIDs written by Jens Alfke.
// Original Code : https://github.com/couchbase/couchbase-lite-ios/blob/master/Source/CBL_Revision.m
//
// Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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

namespace Couchbase.Lite
{
    public static class RevIdCollator
    {
        private static Int32 DefaultCollate(string rev1, string rev2) {
            var result = String.Compare(rev1, rev2, StringComparison.Ordinal);
            return result > 0 ? 1 : (result < 0 ? -1 : 0);
        }

        private static Int32 ParseDigits(string rev, Int32 endPos) {
            var result = 0;
            for (var i = 0; i < endPos; i++) {
                var ch = rev[i];
                if (!Char.IsDigit(ch))
                {
                    return 0;
                }
                result = (10 * result) + (ch - '0');
            }
            return result;
        }

        ///
        /// <summary>
        ///     Compare two Revision ID strings.
        /// </summary>
        /// <remarks>
        ///     A proper revision ID consists of a generation number, a hyphen, and an arbitrary suffix.
        ///     Compare the generation numbers numerically, and then the suffixes lexicographically.
        ///     If either string isn't a proper rev ID, fall back to lexicographic comparison.
        /// </remarks>
        /// <param name="rev1">Revision ID string to compare.</param>
        /// <param name="rev2">Revision ID string to compare.</param>
        /// <returns>
        ///     The value 0 if the rev1 string is equal to the rev2 string; 
        ///     a value -1 if the rev1 is less than the rev2 string; 
        ///     a value 1 if this string is greater than the string argument.
        /// </returns>
        ///
        public static Int32 Compare(string rev1, string rev2)
        {
            var dash1 = rev1.IndexOf('-');
            var dash2 = rev2.IndexOf('-');
            if ((dash1 == 1 && dash2 == 1) 
                || dash1 > 8 || dash2 > 8
                || dash1 == -1 || dash2 == -1)
            {
                // Single-digit generation #s, or improper rev IDs; just compare as plain text:
                return DefaultCollate(rev1, rev2);
            }

            // Parse generation numbers. If either is invalid, revert to default collation:
            var gen1 = ParseDigits(rev1, dash1);
            var gen2 = ParseDigits(rev2, dash2);
            if(gen1 == 0 || gen2 == 0)
            {
                return DefaultCollate(rev1, rev2);
            }

            // Compare generation numbers; if they match, compare suffixes:
            var diff = gen1 - gen2;
            var result = diff > 0 ? 1 : (diff < 0 ? -1 : 0);
            if (result != 0)
            {
                return result;
            }
            else
            {
                var suffix1 = dash1 + 1;
                var suffix2 = dash2 + 1;
                if (rev1.Length > suffix1 && rev2.Length > suffix2)
                {
                    // Compare suffixes:
                    return DefaultCollate(rev1.Substring(suffix1), rev2.Substring(suffix2));
                }
                else
                {
                    // Invalid format, fall back to compare as plain text:
                    return DefaultCollate(rev1, rev2);
                }
            }
        }
    }
}

