//
// FileDirUtils.cs
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

using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;
using System;

namespace Couchbase.Lite.Util
{
    internal class FileDirUtils
    {
        const string Tag = "FileDirUtils";

        public static string GetDatabaseNameFromPath(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name == null) {
                Log.E(Database.Tag, "Unable to determine database name from path");
            }
            return name;
        }

        public static bool RemoveItemIfExists(string path)
        {
            FilePath f = new FilePath(path);
            return f.Delete() || !f.Exists();
        }

        /// <exception cref="System.IO.IOException"></exception>
        public static void CopyFile(FileInfo sourceFile, FileInfo destFile)
        {
            if (!File.Exists(destFile.FullName))
            {
                File.Open (destFile.FullName, FileMode.CreateNew).Close ();
            }

            sourceFile.CopyTo(destFile.FullName);
        }

        public static bool DeleteRecursive (FilePath attachmentsFile)
        {
            var success = true;
            try {
                Directory.Delete (attachmentsFile.GetPath (), true);
            } catch (Exception ex) {
                Log.V(Tag, "Error deleting the '{0}' directory.".Fmt(attachmentsFile.GetAbsolutePath()), ex);
                success = false;
            }
            return success;
        }

        /// <exception cref="System.IO.IOException"></exception>
        public static void CopyFolder(FileSystemInfo sourcePath, FileSystemInfo destinationPath)
        {
            var sourceDirectory = sourcePath as DirectoryInfo;
            if (sourceDirectory != null)
            {
                var destPath = Path.Combine(Path.GetDirectoryName(destinationPath.FullName), Path.GetFileName(sourceDirectory.Name));
                var destinationDirectory = new DirectoryInfo(destPath);

                //if directory not exists, create it
                if (!destinationDirectory.Exists)
                {
                    destinationDirectory.Create();
                }
                //list all the directory contents
                var fileInfos = sourceDirectory.GetFileSystemInfos();
                foreach (var fileInfo in fileInfos)
                {
                    //construct the src and dest file structure
                    //recursive copy
                    CopyFolder(fileInfo, destinationDirectory);
                }
            }
            else
            {
                CopyFile((FileInfo)sourcePath, (FileInfo)destinationPath);
            }
        }
    }
}
