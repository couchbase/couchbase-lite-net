//
// JavaCalendar.cs
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
using System;
using System.Globalization;

namespace Sharpen
{
	public class JavaCalendar
	{
		public static int HOUR_OF_DAY = 0;
		public static int MINUTE = 1;
		public static int SECOND = 2;
		public static int MILLISECOND = 3;
		public static int DATE = 4;
		public static int YEAR = 5;
		public static int MONTH = 6;
		public static int WEEK_OF_YEAR = 7;

		DateTime Time;

		public JavaCalendar ()
		{
			Time = DateTime.UtcNow;
		}

		public void Add (int type, int value)
		{
			switch (type) {
			case 0:
				Time.AddHours (value);
				break;
			case 1:
				Time.AddMinutes (value);
				break;
			case 2:
				Time.AddSeconds (value);
				break;
			case 3:
				Time.AddMilliseconds (value);
				break;
			case 5:
				Time.AddYears (value);
				break;
			case 6:
				Time.AddMonths (value);
				break;
			case 7:
				Time.AddDays (7 * value);
				break;
			default:
				throw new NotSupportedException ();
			}
		}

		public JavaCalendar Clone ()
		{
			return (JavaCalendar) MemberwiseClone ();
		}

		public DateTime GetTime ()
		{
			return Time;
		}

		public void Set (int type, int value)
		{
			switch (type) {
			case 0:
				Time.AddHours (value - Time.Hour);
				break;
			case 1:
				Time.AddMinutes (value - Time.Minute);
				break;
			case 2:
				Time.AddSeconds (value - Time.Second);
				break;
			case 3:
				Time.AddMilliseconds (value - Time.Millisecond);
				break;
			case 5:
				Time.AddYears (value - Time.Year);
				break;
			case 6:
				Time.AddMonths (value - Time.Month);
				break;
			default:
				throw new NotSupportedException ();
			}
		}

		public void SetTimeInMillis (long milliseconds)
		{
			Time = new DateTime (milliseconds * TimeSpan.TicksPerMillisecond);
		}
	}

	public class JavaGregorianCalendar : JavaCalendar
	{
		public JavaGregorianCalendar (TimeZoneInfo timezone, CultureInfo culture)
		{
		}
	}
}

