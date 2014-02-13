//
// FileDirUtils.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
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
/**
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

using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class FileDirUtils
	{
		public static bool RemoveItemIfExists(string path)
		{
			FilePath f = new FilePath(path);
			return f.Delete() || !f.Exists();
		}

		public static bool DeleteRecursive(FilePath fileOrDirectory)
		{
			if (fileOrDirectory.IsDirectory())
			{
				foreach (FilePath child in fileOrDirectory.ListFiles())
				{
					DeleteRecursive(child);
				}
			}
			bool result = fileOrDirectory.Delete() || !fileOrDirectory.Exists();
			return result;
		}

		public static string GetDatabaseNameFromPath(string path)
		{
			int lastSlashPos = path.LastIndexOf("/");
			int extensionPos = path.LastIndexOf(".");
			if (lastSlashPos < 0 || extensionPos < 0 || extensionPos < lastSlashPos)
			{
				Log.E(Database.Tag, "Unable to determine database name from path");
				return null;
			}
			return Sharpen.Runtime.Substring(path, lastSlashPos + 1, extensionPos);
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static void CopyFile(FilePath sourceFile, FilePath destFile)
		{
			if (!destFile.Exists())
			{
				destFile.CreateNewFile();
			}
			FileChannel source = null;
			FileChannel destination = null;
			try
			{
				source = new FileInputStream(sourceFile).GetChannel();
				destination = new FileOutputStream(destFile).GetChannel();
				destination.TransferFrom(source, 0, source.Size());
			}
			finally
			{
				if (source != null)
				{
					source.Close();
				}
				if (destination != null)
				{
					destination.Close();
				}
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static void CopyFolder(FilePath src, FilePath dest)
		{
			if (src.IsDirectory())
			{
				//if directory not exists, create it
				if (!dest.Exists())
				{
					dest.Mkdir();
				}
				//list all the directory contents
				string[] files = src.List();
				foreach (string file in files)
				{
					//construct the src and dest file structure
					FilePath srcFile = new FilePath(src, file);
					FilePath destFile = new FilePath(dest, file);
					//recursive copy
					CopyFolder(srcFile, destFile);
				}
			}
			else
			{
				CopyFile(src, dest);
			}
		}
	}
}
