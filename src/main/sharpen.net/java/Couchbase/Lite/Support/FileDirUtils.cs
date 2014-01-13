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
