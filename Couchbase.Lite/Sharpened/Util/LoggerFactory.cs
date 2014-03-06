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

using System;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Util
{
	public class LoggerFactory
	{
		public static Logger CreateLogger()
		{
			string classname = string.Empty;
			string resource = "services/com.couchbase.lite.util.Logger";
			try
			{
				InputStream inputStream = Sharpen.Thread.CurrentThread().GetContextClassLoader().
					GetResourceAsStream(resource);
				if (inputStream == null)
				{
					// Return default System logger.
					Log.D(Database.Tag, "Unable to load " + resource + " falling back to SystemLogger"
						);
					return new SystemLogger();
				}
				byte[] bytes = TextUtils.Read(inputStream);
				classname = Sharpen.Runtime.GetStringForBytes(bytes);
				if (classname == null || classname.IsEmpty())
				{
					// Return default System logger.
					Log.D(Database.Tag, "Unable to load " + resource + " falling back to SystemLogger"
						);
					return new SystemLogger();
				}
				Log.D(Database.Tag, "Loading logger: " + classname);
				Type clazz = Sharpen.Runtime.GetType(classname);
				Logger logger = (Logger)System.Activator.CreateInstance(clazz);
				return logger;
			}
			catch (Exception e)
			{
				throw new RuntimeException("Failed to logger.  Resource: " + resource + " classname: "
					 + classname, e);
			}
		}
	}
}
