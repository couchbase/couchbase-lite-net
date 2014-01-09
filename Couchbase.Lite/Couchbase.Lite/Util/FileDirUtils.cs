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
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	public class FileDirUtils
	{
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
                var fileInfos = sourceDirectory.EnumerateFileSystemInfos();
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
