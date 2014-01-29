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

using Mono.Data.Sqlite;
using System;
using System.Text;

namespace Couchbase.Lite.Storage
{
    [SqliteFunction(Name = "JSON", FuncType = FunctionType.Collation, Arguments = 2)]
    internal class CouchbaseSqliteCollationFunction : SqliteFunction
    {
        /// <Docs>Implements the custom collection for JSON strings.</Docs>
        /// <summary>
        /// Couchbase custom JSON collation algorithm.
        /// </summary>
        /// <remarks>
        /// This is woefully incomplete.
        /// For full details, see https://github.com/couchbase/couchbase-lite-ios/blob/580c5f65ebda159ce5d0ce1f75adc16955a2a6ff/Source/CBLCollateJSON.m.
        /// </remarks>
        /// <param name="param1">Param1.</param>
        /// <param name="param2">Param2.</param>
        public override Int32 Compare (String param1, String param2)
        {
            // HACK.ZJG: This is woefully incomplete.
            Int32 result;

            var isNumeric = true;
            var raw1 = StripJson(param1, ref isNumeric);
            var raw2 = StripJson(param2, ref isNumeric);

            result = isNumeric 
                     ? Convert.ToInt64(raw1).CompareTo(Convert.ToInt64(raw2))
                     : String.CompareOrdinal(raw1, raw2);

            return result;
        }

        /// <summary>
        /// FIXME: This is a very incorrect implementation of Couchx collation algorithm.
        /// However, it's enough to get started with for now.
        /// </summary>
        /// <returns>The json.</returns>
        /// <param name="jsonString">Json string.</param>
        /// <param name = "isNumeric"></param>
        private String StripJson (string jsonString, ref Boolean isNumeric)
        {
            var rawString = new StringBuilder();

            var previousChar = default(Char);
            var skipChars = 0;

            foreach(var character in jsonString)
            {
                if (skipChars > 0) {
                    skipChars--;
                    continue;
                }

                switch(character) 
                {
                case '\\':
                case '[':
                case ']':
                case '{':
                case '}':
                case '\"':
                case '\'':
                case ':':
                    {
                        break;
                    }
                case 't':
                case 'n':
                case 'r':
                case 'b':
                case 'u':
                    {
                        if (previousChar != '\\') {
                            rawString.Append(character);
                        } else if (previousChar == '\\' && character == 'u') {
                            skipChars = 4; // NOTE.ZJG: Doesn't support escaped unicode characters yet.
                        }
                        break;
                    }
                default: 
                    {
                        rawString.Append(character);
                        break;
                    }
                }
                isNumeric = isNumeric & Char.IsDigit(character);
                previousChar = character;
            }
            return rawString.ToString();
        }
    }
}
