//
// Runtime.cs
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sharpen
{
    internal class Runtime
    {

        internal static long CurrentTimeMillis ()
        {
            return DateTime.UtcNow.ToMillisecondsSinceEpoch ();
        }
            
        static Hashtable properties;

        public static Hashtable Properties { get { return GetProperties(); } }
        
        public static Hashtable GetProperties ()
        {
            if (properties == null) {
                properties = new Hashtable ();
                properties ["jgit.fs.debug"] = "false";
                //var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile).Trim ();
                //if (string.IsNullOrEmpty (home))
                    var home = Environment.GetFolderPath (Environment.SpecialFolder.Personal).Trim ();
                properties ["user.home"] = home;
                properties ["java.library.path"] = Environment.GetEnvironmentVariable ("PATH");
                if (Path.DirectorySeparatorChar != '\\')
                    properties ["os.name"] = "Unix";
                else
                    properties ["os.name"] = "Windows";
            }
            return properties;
        }

        public static string GetProperty (string key)
        {
            return ((string) GetProperties ()[key]);
        }
        
        public static void SetProperty (string key, string value)
        {
            GetProperties () [key] = value;
        }

        internal static IEnumerable<Byte> GetBytesForString (string str, string encoding)
        {
            return Encoding.GetEncoding (encoding).GetBytes (str);
        }

        internal static void PrintStackTrace (Exception ex)
        {
            Console.WriteLine (ex); // TODO: Replace these Console calls with Logger.
        }

        internal static void PrintStackTrace (Exception ex, TextWriter tw)
        {
            tw.WriteLine (ex);
        }
            
    }
}
