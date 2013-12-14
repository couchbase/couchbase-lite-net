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

#if __ANDROID__
using System;
using Android.Util;
using Com.Couchbase.Lite.Util;
using Sharpen;

namespace Com.Couchbase.Lite.Util
{
	public class AndroidLogger : Logger
	{
		public virtual void V(string tag, string msg)
		{
			Log.V(tag, msg);
		}

		public virtual void V(string tag, string msg, Exception tr)
		{
			Log.V(tag, msg, tr);
		}

		public virtual void D(string tag, string msg)
		{
			Log.D(tag, msg);
		}

		public virtual void D(string tag, string msg, Exception tr)
		{
			Log.D(tag, msg, tr);
		}

		public virtual void I(string tag, string msg)
		{
			Log.I(tag, msg);
		}

		public virtual void I(string tag, string msg, Exception tr)
		{
			Log.I(tag, msg, tr);
		}

		public virtual void W(string tag, string msg)
		{
			Log.W(tag, msg);
		}

		public virtual void W(string tag, Exception tr)
		{
			Log.W(tag, tr);
		}

		public virtual void W(string tag, string msg, Exception tr)
		{
			Log.W(tag, msg, tr);
		}

		public virtual void E(string tag, string msg)
		{
			Log.E(tag, msg);
		}

		public virtual void E(string tag, string msg, Exception tr)
		{
			Log.E(tag, msg, tr);
		}
	}
}
#endif
