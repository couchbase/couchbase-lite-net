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
using System.Diagnostics;

namespace Couchbase.Util
{
	public class SystemLogger : Logger
	{
		public virtual void V(string tag, string msg)
		{
            Trace.TraceInformation(tag + ": " + msg);
		}

		public virtual void V(string tag, string msg, Exception tr)
		{
            Trace.TraceInformation(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void D(string tag, string msg)
		{
            Debug.WriteLine(tag + ": " + msg);
		}

		public virtual void D(string tag, string msg, Exception tr)
		{
            Debug.WriteLine(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void I(string tag, string msg)
		{
            Trace.TraceInformation(tag + ": " + msg);
		}

		public virtual void I(string tag, string msg, Exception tr)
		{
            Trace.TraceInformation(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void W(string tag, string msg)
		{
			Trace.TraceWarning(tag + ": " + msg);
		}

		public virtual void W(string tag, Exception tr)
		{
			Trace.TraceWarning(tag + ": " + "\n" + GetStackTraceString(tr));
		}

		public virtual void W(string tag, string msg, Exception tr)
		{
			Trace.TraceWarning(tag + ": " + msg + "\n" + GetStackTraceString(tr));
		}

		public virtual void E(string tag, string msg)
		{
            Trace.TraceError(tag + ": " + msg);
		}

		public virtual void E(string tag, string msg, Exception tr)
		{
            Trace.TraceError(tag + ": " + msg + "\n" + GetStackTraceString(tr));
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
