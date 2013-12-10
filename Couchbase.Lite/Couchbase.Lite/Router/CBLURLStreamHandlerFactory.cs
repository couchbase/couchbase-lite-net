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
using Couchbase.Router;
using Sharpen;

namespace Couchbase.Router
{
	public class CBLURLStreamHandlerFactory : URLStreamHandlerFactory
	{
		public const string Scheme = "cblite";

		// eg, cblite://
		public virtual URLStreamHandler CreateURLStreamHandler(string protocol)
		{
			if (Scheme.Equals(protocol))
			{
				return new CBLURLHandler();
			}
			return null;
		}

		public static void RegisterSelfIgnoreError()
		{
			try
			{
				Uri.SetURLStreamHandlerFactory(new CBLURLStreamHandlerFactory());
			}
			catch (Error)
			{
			}
		}
		//usually you should never catch an Error
		//but I can't see how to avoid this
	}
}
