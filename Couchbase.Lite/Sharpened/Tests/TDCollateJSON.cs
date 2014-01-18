/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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

using Android.Database.Sqlite;
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

		static TDCollateJSON()
		{
			Runtime.LoadLibrary("com_couchbase_touchdb_TDCollateJSON");
		}
	}
}
