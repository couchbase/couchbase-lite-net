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
using System.Collections.Generic;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Util
{
	public class Args
	{
		public static void Check(bool expression, string message)
		{
			if (!expression)
			{
				throw new ArgumentException(message);
			}
		}

		public static void Check(bool expression, string message, params object[] args)
		{
			if (!expression)
			{
				throw new ArgumentException(string.Format(message, args));
			}
		}

		public static T NotNull<T>(T argument, string name)
		{
			if (argument == null)
			{
				throw new ArgumentException(name + " may not be null");
			}
			return argument;
		}

		public static T NotEmpty<T>(T argument, string name) where T:CharSequence
		{
			if (argument == null)
			{
				throw new ArgumentException(name + " may not be null");
			}
			if (TextUtils.IsEmpty(argument))
			{
				throw new ArgumentException(name + " may not be empty");
			}
			return argument;
		}

		public static T NotBlank<T>(T argument, string name) where T:CharSequence
		{
			if (argument == null)
			{
				throw new ArgumentException(name + " may not be null");
			}
			if (TextUtils.IsBlank(argument))
			{
				throw new ArgumentException(name + " may not be blank");
			}
			return argument;
		}

		public static T NotEmpty<E, T>(T argument, string name) where T:ICollection<E>
		{
			if (argument == null)
			{
				throw new ArgumentException(name + " may not be null");
			}
			if (argument.IsEmpty())
			{
				throw new ArgumentException(name + " may not be empty");
			}
			return argument;
		}

		public static int Positive(int n, string name)
		{
			if (n <= 0)
			{
				throw new ArgumentException(name + " may not be negative or zero");
			}
			return n;
		}

		public static long Positive(long n, string name)
		{
			if (n <= 0)
			{
				throw new ArgumentException(name + " may not be negative or zero");
			}
			return n;
		}

		public static int NotNegative(int n, string name)
		{
			if (n < 0)
			{
				throw new ArgumentException(name + " may not be negative");
			}
			return n;
		}

		public static long NotNegative(long n, string name)
		{
			if (n < 0)
			{
				throw new ArgumentException(name + " may not be negative");
			}
			return n;
		}
	}
}
