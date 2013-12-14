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
using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http.Params
{
	/// <summary>Abstract base class for parameter collections.</summary>
	/// <remarks>
	/// Abstract base class for parameter collections.
	/// Type specific setters and getters are mapped to the abstract,
	/// generic getters and setters.
	/// </remarks>
	/// <since>4.0</since>
	[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
		)]
	public abstract class AbstractHttpParams : HttpParams, HttpParamsNames
	{
		/// <summary>Instantiates parameters.</summary>
		/// <remarks>Instantiates parameters.</remarks>
		protected internal AbstractHttpParams() : base()
		{
		}

		public virtual long GetLongParameter(string name, long defaultValue)
		{
			object param = GetParameter(name);
			if (param == null)
			{
				return defaultValue;
			}
			return ((long)param);
		}

		public virtual HttpParams SetLongParameter(string name, long value)
		{
			SetParameter(name, Sharpen.Extensions.ValueOf(value));
			return this;
		}

		public virtual int GetIntParameter(string name, int defaultValue)
		{
			object param = GetParameter(name);
			if (param == null)
			{
				return defaultValue;
			}
			return ((int)param);
		}

		public virtual HttpParams SetIntParameter(string name, int value)
		{
			SetParameter(name, Sharpen.Extensions.ValueOf(value));
			return this;
		}

		public virtual double GetDoubleParameter(string name, double defaultValue)
		{
			object param = GetParameter(name);
			if (param == null)
			{
				return defaultValue;
			}
			return ((double)param);
		}

		public virtual HttpParams SetDoubleParameter(string name, double value)
		{
			SetParameter(name, double.ValueOf(value));
			return this;
		}

		public virtual bool GetBooleanParameter(string name, bool defaultValue)
		{
			object param = GetParameter(name);
			if (param == null)
			{
				return defaultValue;
			}
			return ((bool)param);
		}

		public virtual HttpParams SetBooleanParameter(string name, bool value)
		{
			SetParameter(name, value ? true : false);
			return this;
		}

		public virtual bool IsParameterTrue(string name)
		{
			return GetBooleanParameter(name, false);
		}

		public virtual bool IsParameterFalse(string name)
		{
			return !GetBooleanParameter(name, false);
		}

		/// <summary>
		/// <inheritDoc></inheritDoc>
		/// <p/>
		/// Dummy implementation - must be overridden by subclasses.
		/// </summary>
		/// <since>4.2</since>
		/// <exception cref="System.NotSupportedException">- always</exception>
		public virtual ICollection<string> GetNames()
		{
			throw new NotSupportedException();
		}

		public abstract HttpParams Copy();

		public abstract object GetParameter(string arg1);

		public abstract bool RemoveParameter(string arg1);

		public abstract HttpParams SetParameter(string arg1, object arg2);
	}
}
