//
// FileHelper.cs
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
using System.IO;
using System.Reflection;
using System.Security.AccessControl;

namespace Sharpen
{
    class FileHelper
    {
        public static FileHelper Instance { get; set; }
        
        static FileHelper ()
        {
            Instance = new FileHelper ();
        }

        public virtual bool CanExecute (FilePath path)
        {
            return false;
        }

        public virtual bool CanWrite (FilePath path)
        {
            return ((File.GetAttributes (path) & FileAttributes.ReadOnly) == 0);
        }
        
        public virtual bool CanRead (FilePath path)
        {
            return Exists(path) && ((File.GetAttributes (path) & FileAttributes.Offline) == 0);
        }

        public virtual bool Delete (FilePath path)
        {
            if (Directory.Exists (path)) {
                if (Directory.GetFileSystemEntries (path).Length != 0)
                    return false;
                MakeDirWritable (path);
                Directory.Delete (path, true);
                return true;
            }
            else if (File.Exists(path)) {
                MakeFileWritable (path);
                File.Delete (path);
                return true;
            }
            return false;
        }
        
        public virtual bool Exists (FilePath path)
        {
            return (File.Exists (path) || Directory.Exists (path));
        }
        
        public virtual bool IsDirectory (FilePath path)
        {
            return Directory.Exists (path);
        }

        public virtual bool IsFile (FilePath path)
        {
            return File.Exists (path);
        }

        public virtual long LastModified (FilePath path)
        {
            if (IsFile(path)) {
                var info2 = new FileInfo(path);
                return info2.Exists ? info2.LastWriteTimeUtc.ToMillisecondsSinceEpoch() : 0;
            } else if (IsDirectory (path)) {
                var info = new DirectoryInfo(path);
                return info.Exists ? info.LastWriteTimeUtc.ToMillisecondsSinceEpoch() : 0;
            }
            return 0;
        }

        public virtual long Length (FilePath path)
        {
            // If you call .Length on a file that doesn't exist, an exception is thrown
            var info2 = new FileInfo (path);
            return info2.Exists ? info2.Length : 0;
        }

        public virtual void MakeDirWritable (FilePath path)
        {
            foreach (string file in Directory.GetFiles (path)) {
                MakeFileWritable (file);
            }
            foreach (string subdir in Directory.GetDirectories (path)) {
                MakeDirWritable (subdir);
            }
        }

        public virtual void MakeFileWritable (FilePath file)
        {
            FileAttributes fileAttributes = File.GetAttributes (file);
            if ((fileAttributes & FileAttributes.ReadOnly) != 0) {
                fileAttributes &= ~FileAttributes.ReadOnly;
                File.SetAttributes (file, fileAttributes);
            }
        }

        public virtual bool RenameTo (FilePath path, string name)
        {
            try {
                File.Move (path, name);
                return true;
            } catch {
                return false;
            }
        }

        public virtual bool SetExecutable (FilePath path, bool exec)
        {
            return false;
        }

        public virtual bool SetReadOnly (FilePath path)
        {
            try {
                var fileAttributes = File.GetAttributes (path) | FileAttributes.ReadOnly;
                File.SetAttributes (path, fileAttributes);
                return true;
            } catch {
                return false;
            }
        }

        public virtual bool SetLastModified(FilePath path, long milis)
        {
            try {
                DateTime utcDateTime = Extensions.MillisToDateTimeOffset(milis, 0L).UtcDateTime;
                if (IsFile(path)) {
                    var info2 = new FileInfo(path);
                    info2.LastWriteTimeUtc = utcDateTime;
                    return true;
                } else if (IsDirectory(path)) {
                    var info = new DirectoryInfo(path);
                    info.LastWriteTimeUtc = utcDateTime;
                    return true;
                }
            } catch  {

            }
            return false;
        }
    }
}

