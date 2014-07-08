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
//using Android.Database.Sqlite;
using Android.OS;
using Couchbase.Lite;
using Sharpen;

namespace Couchbase.Lite
{
	public class TDCollateJSON
	{
		public static void RegisterCustomCollators(SQLiteDatabase database)
		{
			NativeRegisterCustomCollators(database, Build.VERSION.SdkInt);
		}

		public static int CompareStringsUnicode(string a, string b)
		{
			Collator c = Collator.GetInstance();
			int res = c.Compare(a, b);
			return res;
		}

		private static void NativeRegisterCustomCollators(SQLiteDatabase database, int sdkVersion
			)
		{
		}

		//FIXME only public for now until tests are moved int same package
		public static int TestCollateJSON(int mode, int len1, string string1, int len2, string
			 string2)
		{
		}

		public static char TestEscape(string source)
		{
		}

		public static int TestDigitToInt(int digit)
		{
		}

		/// <summary>
		/// Convenience wrapper around testCollateJSON which calculates lengths based on string lengths
		/// of params.
		/// </summary>
		/// <remarks>
		/// Convenience wrapper around testCollateJSON which calculates lengths based on string lengths
		/// of params.
		/// </remarks>
		public static int TestCollateJSONWrapper(int mode, string string1, string string2
			)
		{
			return TestCollateJSON(mode, string1.Length, string1, string2.Length, string2);
		}

		static TDCollateJSON()
		{
			Runtime.LoadLibrary("com_couchbase_touchdb_TDCollateJSON");
		}
	}
}
