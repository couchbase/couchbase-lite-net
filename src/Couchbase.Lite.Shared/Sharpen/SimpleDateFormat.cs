//
// SimpleDateFormat.cs
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
namespace Sharpen
{
    using System;
    using System.Globalization;

    internal class SimpleDateFormat : DateFormat
    {
        string format;

        CultureInfo Culture {
            get; set;
        }

        bool Lenient {
            get; set;
        }
        
        public SimpleDateFormat (): this ("g")
        {
        }

        public SimpleDateFormat (string format): this (format, CultureInfo.CurrentCulture)
        {
        }

        public SimpleDateFormat (string format, CultureInfo c)
        {
            Culture = c;
            this.format = format.Replace ("EEE", "ddd");
            this.format = this.format.Replace ("Z", "zzz");
            SetTimeZone (TimeZoneInfo.Local);
        }

        public bool IsLenient ()
        {
            return Lenient;
        }

        public void SetLenient (bool lenient)
        {
            Lenient = lenient;
        }

        public override DateTime Parse (string value)
        {
            if (IsLenient ())
                return DateTime.Parse (value);
            else
                return DateTime.ParseExact (value, format, Culture);
        }

        public override string Format (DateTime date)
        {
            date += GetTimeZone().BaseUtcOffset;
            return date.ToString (format);
        }
        
        public string Format (long date)
        {
            return Extensions.MillisToDateTimeOffset (date, (int)GetTimeZone ().BaseUtcOffset.TotalMinutes).DateTime.ToString (format);
        }
    }
}
