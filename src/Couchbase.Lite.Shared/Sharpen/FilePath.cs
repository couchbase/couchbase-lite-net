//
// FilePath.cs
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
namespace Sharpen
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;

	internal class FilePath
	{
        private readonly string path;
		private static long tempCounter;

		public FilePath ()
		{
		}

		public FilePath (string path)
			: this ((string) null, path)
		{

		}

		public FilePath (FilePath other, string child)
			: this ((string) other, child)
		{

		}

		public FilePath (string other, string child)
		{
			if (other == null) {
				this.path = child;
			} else {
                while (!String.IsNullOrEmpty (child) && (child [0] == Path.DirectorySeparatorChar || child [0] == Path.AltDirectorySeparatorChar))
					child = child.Substring (1);

				if (!string.IsNullOrEmpty(other) && other[other.Length - 1] == Path.VolumeSeparatorChar)
					other += Path.DirectorySeparatorChar;

				this.path = Path.Combine (other, child);
			}
		}
		
		public static implicit operator FilePath (string name)
		{
			return new FilePath (name);
		}

		public static implicit operator string (FilePath filePath)
		{
			return filePath == null ? null : filePath.path;
		}
		
        public static implicit operator FilePath (FileInfo file)
        {
            return new FilePath(file.FullName);
        }

        public static implicit operator FileInfo (FilePath file)
        {
            return new FileInfo(file.path);
        }

		public override bool Equals (object obj)
		{
			FilePath other = obj as FilePath;
			if (other == null)
				return false;
			return GetCanonicalPath () == other.GetCanonicalPath ();
		}
		
		public override int GetHashCode ()
		{
			return path.GetHashCode ();
		}

        public bool CanRead ()
        {
            return FileHelper.Instance.CanRead (this);
        }

		public bool CanWrite ()
		{
			return FileHelper.Instance.CanWrite (this);
		}

		public bool CreateNewFile ()
		{
			try {
                File.Open (path, FileMode.CreateNew).Close ();
				return true;
			} catch {
				return false;
			}
		}

		public static FilePath CreateTempFile ()
		{
			return new FilePath (Path.GetTempFileName ());
		}

		public static FilePath CreateTempFile (string prefix, string suffix)
		{
			return CreateTempFile (prefix, suffix, null);
		}

		public static FilePath CreateTempFile (string prefix, string suffix, FilePath directory)
		{
			string file;
			if (prefix == null) {
				throw new ArgumentNullException ("prefix");
			}
			if (prefix.Length < 3) {
				throw new ArgumentException ("prefix must have at least 3 characters");
			}
			string str = (directory == null) ? Path.GetTempPath () : directory.GetPath ();
			do {
				file = Path.Combine (str, prefix + Interlocked.Increment (ref tempCounter) + suffix);
			} while (File.Exists (file));
			
			new FileOutputStream (file).Close ();
			return new FilePath (file);
		}

		public bool Delete ()
		{
			try {
				return FileHelper.Instance.Delete (this);
			} catch (Exception exception) {
				Console.WriteLine (exception);
				return false;
			}
		}

		public void DeleteOnExit ()
		{
		}

		public bool Exists ()
		{
			return FileHelper.Instance.Exists (this);
		}

		public FilePath GetAbsoluteFile ()
		{
			return new FilePath (Path.GetFullPath (path));
		}

		public string GetAbsolutePath ()
		{
			return Path.GetFullPath (path);
		}

		public FilePath GetCanonicalFile ()
		{
			return new FilePath (GetCanonicalPath ());
		}

		public string GetCanonicalPath ()
		{
			string p = Path.GetFullPath (path);
			p.TrimEnd (Path.DirectorySeparatorChar);
			return p;
		}

		public string GetName ()
		{
			return Path.GetFileName (path);
		}

		public FilePath GetParentFile ()
		{
			return new FilePath (Path.GetDirectoryName (path));
		}

		public string GetPath ()
		{
			return path;
		}

		public bool IsAbsolute ()
		{
			return Path.IsPathRooted (path);
		}

		public bool IsDirectory ()
		{
			return FileHelper.Instance.IsDirectory (this);
		}

		public bool IsFile ()
		{
			return FileHelper.Instance.IsFile (this);
		}

		public long LastModified ()
		{
			return FileHelper.Instance.LastModified (this);
		}

		public long Length ()
		{
			return FileHelper.Instance.Length (this);
		}

		public string[] List ()
		{
			return List (null);
		}

		public string[] List (FilenameFilter filter)
		{
			try {
				if (IsFile ())
					return null;
				List<string> list = new List<string> ();
				foreach (string filePth in Directory.GetFileSystemEntries (path)) {
					string fileName = Path.GetFileName (filePth);
					if ((filter == null) || filter.Accept (this, fileName)) {
						list.Add (fileName);
					}
				}
				return list.ToArray ();
			} catch {
				return null;
			}
		}

		public FilePath[] ListFiles ()
		{
			try {
				if (IsFile ())
					return null;
				List<FilePath> list = new List<FilePath> ();
				foreach (string filePath in Directory.GetFileSystemEntries (path)) {
					list.Add (new FilePath (filePath));
				}
				return list.ToArray ();
			} catch {
				return null;
			}
		}

		static void MakeDirWritable (string dir)
		{
			FileHelper.Instance.MakeDirWritable (dir);
		}

		static void MakeFileWritable (string file)
		{
			FileHelper.Instance.MakeFileWritable (file);
		}

		public bool Mkdir ()
		{
			try {
				if (Directory.Exists (path))
					return false;
				Directory.CreateDirectory (path);
				return true;
			} catch (Exception) {
				return false;
			}
		}

		public bool Mkdirs ()
		{
			try {
				if (Directory.Exists (path))
					return false;
				Directory.CreateDirectory (this.path);
				return true;
			} catch {
				return false;
			}
		}

		public bool RenameTo (FilePath file)
		{
			return RenameTo (file.path);
		}

		public bool RenameTo (string name)
		{
			return FileHelper.Instance.RenameTo (this, name);
		}

		public bool SetLastModified (long milis)
		{
			return FileHelper.Instance.SetLastModified(this, milis);
		}

		public bool SetReadOnly ()
		{
			return FileHelper.Instance.SetReadOnly (this);
		}
		
		public Uri ToURI ()
		{
			return new Uri (path);
		}
		
		// Don't change the case of this method, since ngit does reflection on it
		public bool canExecute ()
		{
			return FileHelper.Instance.CanExecute (this);
		}
		
		// Don't change the case of this method, since ngit does reflection on it
		public bool setExecutable (bool exec)
		{
			return FileHelper.Instance.SetExecutable (this, exec);
		}
		
		public string GetParent ()
		{
			string p = Path.GetDirectoryName (path);
			if (string.IsNullOrEmpty(p) || p == path)
				return null;
			else
				return p;
		}

		public override string ToString ()
		{
			return path;
		}
		
		static internal string pathSeparator {
			get { return Path.PathSeparator.ToString (); }
		}

		static internal char pathSeparatorChar {
			get { return Path.PathSeparator; }
		}

		static internal char separatorChar {
			get { return Path.DirectorySeparatorChar; }
		}

		static internal string separator {
			get { return Path.DirectorySeparatorChar.ToString (); }
		}
	}
}
