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

using System;
using System.Globalization;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>Generates a date in the format required by the HTTP protocol.</summary>
	/// <remarks>Generates a date in the format required by the HTTP protocol.</remarks>
	/// <since>4.0</since>
	public class HttpDateGenerator
	{
		/// <summary>Date format pattern used to generate the header in RFC 1123 format.</summary>
		/// <remarks>Date format pattern used to generate the header in RFC 1123 format.</remarks>
		public const string PatternRfc1123 = "EEE, dd MMM yyyy HH:mm:ss zzz";

		/// <summary>The time zone to use in the date header.</summary>
		/// <remarks>The time zone to use in the date header.</remarks>
		public static readonly TimeZoneInfo Gmt = Sharpen.Extensions.GetTimeZone("GMT");

		private readonly DateFormat dateformat;

		private long dateAsLong = 0L;

		private string dateAsText = null;

		public HttpDateGenerator() : base()
		{
			this.dateformat = new SimpleDateFormat(PatternRfc1123, CultureInfo.InvariantCulture
				);
			this.dateformat.SetTimeZone(Gmt);
		}

		public virtual string GetCurrentDate()
		{
			lock (this)
			{
				long now = Runtime.CurrentTimeMillis();
				if (now - this.dateAsLong > 1000)
				{
					// Generate new date string
					this.dateAsText = this.dateformat.Format(Sharpen.Extensions.CreateDate(now));
					this.dateAsLong = now;
				}
				return this.dateAsText;
			}
		}
	}
}
