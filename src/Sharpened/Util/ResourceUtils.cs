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
using System.Collections.Generic;
using Couchbase.Lite.Util;
using Sharpen;
using Sharpen.Jar;

namespace Couchbase.Lite.Util
{
    public class ResourceUtils
    {
        /// <summary>List directory contents for a resource folder.</summary>
        /// <remarks>List directory contents for a resource folder. Not recursive.</remarks>
        /// <author>Andrew Reslan</author>
        /// <param name="clazz">Any java class that lives in the same place as the resources folder
        ///     </param>
        /// <param name="path">Should end with "/", but not start with one.</param>
        /// <returns>An array of the name of each member item, or null if path does not denote a directory
        ///     </returns>
        /// <exception cref="Sharpen.URISyntaxException">Sharpen.URISyntaxException</exception>
        /// <exception cref="System.IO.IOException">System.IO.IOException</exception>
        public static string[] GetResourceListing(Type clazz, string path)
        {
            Uri dirURL = clazz.GetClassLoader().GetResource(path);
            if (dirURL != null && dirURL.Scheme.Equals("file"))
            {
                return new FilePath(dirURL.ToURI()).List();
            }
            if (dirURL != null && dirURL.Scheme.Equals("jar"))
            {
                string jarPath = Sharpen.Runtime.Substring(dirURL.AbsolutePath, 5, dirURL.AbsolutePath
                    .IndexOf("!"));
                JarFile jar = new JarFile(URLDecoder.Decode(jarPath, "UTF-8"));
                Enumeration<JarEntry> entries = ((Enumeration<JarEntry>)jar.Entries());
                ICollection<string> result = new HashSet<string>();
                while (entries.MoveNext())
                {
                    string name = entries.Current.GetName();
                    if (name.StartsWith(path))
                    {
                        string entry = Sharpen.Runtime.Substring(name, path.Length);
                        int checkSubdir = entry.IndexOf("/");
                        if (checkSubdir >= 0)
                        {
                            // if it is a subdirectory, we just return the directory name
                            entry = Sharpen.Runtime.Substring(entry, 0, checkSubdir);
                        }
                        result.AddItem(entry);
                    }
                }
                return Sharpen.Collections.ToArray(result, new string[result.Count]);
            }
            throw new NotSupportedException("Cannot list files for URL " + dirURL);
        }
    }
}
