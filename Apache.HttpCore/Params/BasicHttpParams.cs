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
	/// <summary>
	/// Default implementation of
	/// <see cref="HttpParams">HttpParams</see>
	/// interface.
	/// <p>
	/// Please note access to the internal structures of this class is not
	/// synchronized and therefore this class may be thread-unsafe.
	/// </summary>
	/// <since>4.0</since>
	[System.Serializable]
	[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
		)]
	public class BasicHttpParams : AbstractHttpParams, ICloneable
	{
		private const long serialVersionUID = -7086398485908701455L;

		/// <summary>Map of HTTP parameters that this collection contains.</summary>
		/// <remarks>Map of HTTP parameters that this collection contains.</remarks>
		private readonly IDictionary<string, object> parameters = new ConcurrentHashMap<string
			, object>();

		public BasicHttpParams() : base()
		{
		}

		public override object GetParameter(string name)
		{
			return this.parameters.Get(name);
		}

		public override HttpParams SetParameter(string name, object value)
		{
			if (name == null)
			{
				return this;
			}
			if (value != null)
			{
				this.parameters.Put(name, value);
			}
			else
			{
				Sharpen.Collections.Remove(this.parameters, name);
			}
			return this;
		}

		public override bool RemoveParameter(string name)
		{
			//this is to avoid the case in which the key has a null value
			if (this.parameters.ContainsKey(name))
			{
				Sharpen.Collections.Remove(this.parameters, name);
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>Assigns the value to all the parameter with the given names</summary>
		/// <param name="names">array of parameter names</param>
		/// <param name="value">parameter value</param>
		public virtual void SetParameters(string[] names, object value)
		{
			foreach (string name in names)
			{
				SetParameter(name, value);
			}
		}

		/// <summary>
		/// Is the parameter set?
		/// <p>
		/// Uses
		/// <see cref="GetParameter(string)">GetParameter(string)</see>
		/// (which is overrideable) to
		/// fetch the parameter value, if any.
		/// <p>
		/// Also @see
		/// <see cref="IsParameterSetLocally(string)">IsParameterSetLocally(string)</see>
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <returns>true if parameter is defined and non-null</returns>
		public virtual bool IsParameterSet(string name)
		{
			return GetParameter(name) != null;
		}

		/// <summary>
		/// Is the parameter set in this object?
		/// <p>
		/// The parameter value is fetched directly.
		/// </summary>
		/// <remarks>
		/// Is the parameter set in this object?
		/// <p>
		/// The parameter value is fetched directly.
		/// <p>
		/// Also @see
		/// <see cref="IsParameterSet(string)">IsParameterSet(string)</see>
		/// </remarks>
		/// <param name="name">parameter name</param>
		/// <returns>true if parameter is defined and non-null</returns>
		public virtual bool IsParameterSetLocally(string name)
		{
			return this.parameters.Get(name) != null;
		}

		/// <summary>Removes all parameters from this collection.</summary>
		/// <remarks>Removes all parameters from this collection.</remarks>
		public virtual void Clear()
		{
			this.parameters.Clear();
		}

		/// <summary>Creates a copy of these parameters.</summary>
		/// <remarks>
		/// Creates a copy of these parameters.
		/// This implementation calls
		/// <see cref="Clone()">Clone()</see>
		/// .
		/// </remarks>
		/// <returns>
		/// a new set of params holding a copy of the
		/// <i>local</i> parameters in this object.
		/// </returns>
		/// <exception cref="System.NotSupportedException">if the clone() fails</exception>
		public override HttpParams Copy()
		{
			return (HttpParams)Clone();
		}

		/// <summary>Clones the instance.</summary>
		/// <remarks>
		/// Clones the instance.
		/// Uses
		/// <see cref="CopyParams(HttpParams)">CopyParams(HttpParams)</see>
		/// to copy the parameters.
		/// </remarks>
		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			Org.Apache.Http.Params.BasicHttpParams clone = (Org.Apache.Http.Params.BasicHttpParams
				)base.Clone();
			CopyParams(clone);
			return clone;
		}

		/// <summary>Copies the locally defined parameters to the argument parameters.</summary>
		/// <remarks>
		/// Copies the locally defined parameters to the argument parameters.
		/// This method is called from
		/// <see cref="Clone()">Clone()</see>
		/// .
		/// </remarks>
		/// <param name="target">the parameters to which to copy</param>
		/// <since>4.2</since>
		public virtual void CopyParams(HttpParams target)
		{
			foreach (KeyValuePair<string, object> me in this.parameters.EntrySet())
			{
				target.SetParameter(me.Key, me.Value);
			}
		}

		/// <summary>Returns the current set of names.</summary>
		/// <remarks>
		/// Returns the current set of names.
		/// Changes to the underlying HttpParams are not reflected
		/// in the set - it is a snapshot.
		/// </remarks>
		/// <returns>the names, as a Set<String></returns>
		/// <since>4.2</since>
		public override ICollection<string> GetNames()
		{
			return new HashSet<string>(this.parameters.Keys);
		}
	}
}
