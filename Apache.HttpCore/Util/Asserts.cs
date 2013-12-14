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
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Util
{
	public class Asserts
	{
		public static void Check(bool expression, string message)
		{
			if (!expression)
			{
				throw new InvalidOperationException(message);
			}
		}

		public static void Check(bool expression, string message, params object[] args)
		{
			if (!expression)
			{
				throw new InvalidOperationException(string.Format(message, args));
			}
		}

		public static void NotNull(object @object, string name)
		{
			if (@object == null)
			{
				throw new InvalidOperationException(name + " is null");
			}
		}

		public static void NotEmpty(CharSequence s, string name)
		{
			if (TextUtils.IsEmpty(s))
			{
				throw new InvalidOperationException(name + " is empty");
			}
		}

		public static void NotBlank(CharSequence s, string name)
		{
			if (TextUtils.IsBlank(s))
			{
				throw new InvalidOperationException(name + " is blank");
			}
		}
	}
}
