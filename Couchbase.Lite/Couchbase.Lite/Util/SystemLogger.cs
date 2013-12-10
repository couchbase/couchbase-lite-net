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
using System.IO;
using Couchbase.Util;
using Sharpen;
using Sharpen.Logging;

namespace Couchbase.Util
{
	public class SystemLogger : Logger
	{
		private static readonly Logger logger = Logger.GetLogger("com.couchbase.cblite");

		public virtual void V(string tag, string msg)
		{
			logger.Finer(tag + ": " + msg);
		}

		public virtual void V(string tag, string msg, Exception tr)
		{
			logger.Finer(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void D(string tag, string msg)
		{
			logger.Fine(tag + ": " + msg);
		}

		public virtual void D(string tag, string msg, Exception tr)
		{
			logger.Fine(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void I(string tag, string msg)
		{
			logger.Info(tag + ": " + msg);
		}

		public virtual void I(string tag, string msg, Exception tr)
		{
			logger.Info(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void W(string tag, string msg)
		{
			logger.Warning(tag + ": " + msg);
		}

		public virtual void W(string tag, Exception tr)
		{
			logger.Warning(tag + ": " + "\n" + GetStackTraceString(tr));
		}

		public virtual void W(string tag, string msg, Exception tr)
		{
			logger.Warning(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void E(string tag, string msg)
		{
			logger.Severe(tag + ": " + msg);
		}

		public virtual void E(string tag, string msg, Exception tr)
		{
			logger.Severe(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		private static string GetStackTraceString(Exception tr)
		{
			if (tr == null)
			{
				return string.Empty;
			}
			StringWriter stringWriter = new StringWriter();
			PrintWriter printWriter = new PrintWriter(stringWriter);
			Sharpen.Runtime.PrintStackTrace(tr, printWriter);
			return stringWriter.ToString();
		}
	}
}
