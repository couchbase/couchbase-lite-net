//
// MessageFormat.cs
//
// Author:
//  Zachary Gramana  <zack@xamarin.com>
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
/*
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
using System.Text;
using System.Collections.Generic;

namespace Sharpen
{
    using System;

    internal class MessageFormat
    {
        public static string Format (string message, params object[] args)
        {
            StringBuilder sb = new StringBuilder ();
            bool inQuote = false;
            bool inPlaceholder = false;
            int argStartPos = 0;
            List<string> placeholderArgs = new List<string> (3);
            
            for (int n=0; n<message.Length; n++) {
                char c = message[n];
                if (c == '\'') {
                    if (!inQuote)
                        inQuote = true;
                    else if (n > 0 && message [n-1] == '\'') {
                        inQuote = false;
                    }
                    else {
                        inQuote = false;
                        continue;
                    }
                }
                else if (c == '{' && !inQuote) {
                    inPlaceholder = true;
                    argStartPos = n + 1;
                    continue;
                }
                else if (c == '}' && !inQuote && inPlaceholder) {
                    inPlaceholder = false;
                    placeholderArgs.Add (message.Substring (argStartPos, n - argStartPos));
                    AddFormatted (sb, placeholderArgs, args);
                    placeholderArgs.Clear ();
                    continue;
                }
                else if (c == ',' && inPlaceholder) {
                    placeholderArgs.Add (message.Substring (argStartPos, n - argStartPos));
                    argStartPos = n + 1;
                    continue;
                }
                else if (inPlaceholder)
                    continue;
                
                sb.Append (c);
            }
            return sb.ToString ();
        }

        static void AddFormatted (StringBuilder sb, List<string> placeholderArgs, object[] args)
        {
            if (placeholderArgs.Count > 3)
                throw new ArgumentException ("Invalid format pattern: {" + string.Join (",", placeholderArgs.ToArray()) + "}");
                
            int narg;
            if (!int.TryParse (placeholderArgs[0], out narg))
                throw new ArgumentException ("Invalid argument index: " + placeholderArgs[0]);
            if (narg < 0 || narg >= args.Length)
                throw new ArgumentException ("Invalid argument index: " + narg);
            
            object arg = args [narg];
            sb.Append (arg);
            
            // TODO: handle format types and styles
        }
    }
}
