//
// Runtime.cs
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text;
using System.Threading;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using System.Linq;

namespace Sharpen
{
	public class Runtime
	{
		private static Runtime instance;
		private List<ShutdownHook> shutdownHooks = new List<ShutdownHook> ();

		internal void AddShutdownHook (Runnable r)
		{
			ShutdownHook item = new ShutdownHook ();
			item.Runnable = r;
			this.shutdownHooks.Add (item);
		}

		internal int AvailableProcessors ()
		{
			return Environment.ProcessorCount;
		}

		internal static long CurrentTimeMillis ()
		{
			return DateTime.UtcNow.ToMillisecondsSinceEpoch ();
		}

		internal SystemProcess Exec (string[] cmd, string[] envp, FilePath dir)
		{
			try {
				ProcessStartInfo psi = new ProcessStartInfo ();
				psi.FileName = cmd[0];
				psi.Arguments = string.Join (" ", cmd, 1, cmd.Length - 1);
				if (dir != null) {
					psi.WorkingDirectory = dir.GetPath ();
				}
				psi.UseShellExecute = false;
				psi.RedirectStandardInput = true;
				psi.RedirectStandardError = true;
				psi.RedirectStandardOutput = true;
				psi.CreateNoWindow = true;
				if (envp != null) {
					foreach (string str in envp) {
						int index = str.IndexOf ('=');
						psi.EnvironmentVariables[str.Substring (0, index)] = str.Substring (index + 1);
					}
				}
				return SystemProcess.Start (psi);
			} catch (System.ComponentModel.Win32Exception ex) {
				throw new IOException (ex.Message);
			}
		}

		internal static string Getenv (string var)
		{
			return Environment.GetEnvironmentVariable (var);
		}

		internal static IDictionary<string, string> GetEnv ()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string> ();
			foreach (DictionaryEntry v in Environment.GetEnvironmentVariables ()) {
				dictionary[(string)v.Key] = (string)v.Value;
			}
			return dictionary;
		}

		internal static IPAddress GetLocalHost ()
		{
			return Dns.GetHostEntry (Dns.GetHostName ()).AddressList[0];
		}
		
		static Hashtable properties;

        public static Hashtable Properties { get { return GetProperties(); } }
		
		public static Hashtable GetProperties ()
		{
			if (properties == null) {
				properties = new Hashtable ();
				properties ["jgit.fs.debug"] = "false";
				var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile).Trim ();
				if (string.IsNullOrEmpty (home))
					home = Environment.GetFolderPath (Environment.SpecialFolder.Personal).Trim ();
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

		public static Runtime GetRuntime ()
		{
			if (instance == null) {
				instance = new Runtime ();
			}
			return instance;
		}

		internal static int IdentityHashCode (object ob)
		{
			return RuntimeHelpers.GetHashCode (ob);
		}

		internal long MaxMemory ()
		{
			return int.MaxValue;
		}

		private class ShutdownHook
		{
			public Sharpen.Runnable Runnable;

			~ShutdownHook ()
			{
				this.Runnable.Run ();
			}
		}
		
		internal static void DeleteCharAt (StringBuilder sb, int index)
		{
			sb.Remove (index, 1);
		}
		
		internal static IEnumerable<Byte> GetBytesForString (string str)
		{
			return Encoding.UTF8.GetBytes (str);
		}

		internal static IEnumerable<Byte> GetBytesForString (string str, string encoding)
		{
			return Encoding.GetEncoding (encoding).GetBytes (str);
		}

		internal static FieldInfo[] GetDeclaredFields (Type t)
		{
			return t.GetFields (BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
		}

		internal static void NotifyAll (object ob)
		{
			Monitor.PulseAll (ob);
		}

		internal static void PrintStackTrace (Exception ex)
		{
            Console.WriteLine (ex); // TODO: Replace these Console calls with Logger.
		}

		internal static void PrintStackTrace (Exception ex, TextWriter tw)
		{
			tw.WriteLine (ex);
		}

		internal static string Substring (string str, int index)
		{
			return str.Substring (index);
		}

		internal static string Substring (string str, int index, int endIndex)
		{
			return str.Substring (index, endIndex - index);
		}

		internal static void Wait (object ob)
		{
			Monitor.Wait (ob);
		}

		internal static bool Wait (object ob, long milis)
		{
			return Monitor.Wait (ob, (int)milis);
		}
		
		internal static Type GetType (string name)
		{
			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies ()) {
				Type t = a.GetType (name);
				if (t != null)
					return t;
			}
			throw new InvalidOperationException ("Type not found: " + name);
		}
		
		internal static void SetCharAt (StringBuilder sb, int index, char c)
		{
			sb [index] = c;
		}
		
		internal static bool EqualsIgnoreCase (string s1, string s2)
		{
			return s1.Equals (s2, StringComparison.CurrentCultureIgnoreCase);
		}
		
		internal static long NanoTime ()
		{
			return Environment.TickCount * 1000 * 1000;
		}
		
		internal static int CompareOrdinal (string s1, string s2)
		{
			return string.CompareOrdinal (s1, s2);
		}

		internal static string GetStringForBytes (IEnumerable<Byte> chars)
		{
            return Encoding.UTF8.GetString (chars.ToArray());
		}

		internal static string GetStringForBytes (IEnumerable<Byte> chars, string encoding)
		{
            return GetEncoding (encoding).GetString (chars.ToArray());
		}

		internal static string GetStringForBytes (IEnumerable<Byte> chars, int start, int len)
		{
            return Encoding.UTF8.GetString (chars.ToArray(), start, len);
		}

		internal static string GetStringForBytes (IEnumerable<Byte> chars, int start, int len, string encoding)
		{
            return GetEncoding (encoding).Decode (chars.ToArray(), start, len);
		}
		
		internal static Encoding GetEncoding (string name)
		{
//			Encoding e = Encoding.GetEncoding (name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
			Encoding e = Encoding.GetEncoding (name.Replace ('_','-'));
			if (e is UTF8Encoding)
				return new UTF8Encoding (false, true);
			return e;
		}
	}
}
