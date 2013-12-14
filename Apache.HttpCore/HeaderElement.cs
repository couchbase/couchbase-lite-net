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

using Org.Apache.Http;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>
	/// One element of an HTTP
	/// <see cref="Header">header</see>
	/// value consisting of
	/// a name / value pair and a number of optional name / value parameters.
	/// <p>
	/// Some HTTP headers (such as the set-cookie header) have values that
	/// can be decomposed into multiple elements.  Such headers must be in the
	/// following form:
	/// </p>
	/// <pre>
	/// header  = [ element ] *( "," [ element ] )
	/// element = name [ "=" [ value ] ] *( ";" [ param ] )
	/// param   = name [ "=" [ value ] ]
	/// name    = token
	/// value   = ( token | quoted-string )
	/// token         = 1*&lt;any char except "=", ",", ";", &lt;"&gt; and
	/// white space&gt;
	/// quoted-string = &lt;"&gt; *( text | quoted-char ) &lt;"&gt;
	/// text          = any char except &lt;"&gt;
	/// quoted-char   = "\" char
	/// </pre>
	/// <p>
	/// Any amount of white space is allowed between any part of the
	/// header, element or param and is ignored. A missing value in any
	/// element or param will be stored as the empty
	/// <see cref="string">string</see>
	/// ;
	/// if the "=" is also missing <var>null</var> will be stored instead.
	/// </summary>
	/// <since>4.0</since>
	public interface HeaderElement
	{
		/// <summary>Returns header element name.</summary>
		/// <remarks>Returns header element name.</remarks>
		/// <returns>header element name</returns>
		string GetName();

		/// <summary>Returns header element value.</summary>
		/// <remarks>Returns header element value.</remarks>
		/// <returns>header element value</returns>
		string GetValue();

		/// <summary>Returns an array of name / value pairs.</summary>
		/// <remarks>Returns an array of name / value pairs.</remarks>
		/// <returns>array of name / value pairs</returns>
		NameValuePair[] GetParameters();

		/// <summary>Returns the first parameter with the given name.</summary>
		/// <remarks>Returns the first parameter with the given name.</remarks>
		/// <param name="name">parameter name</param>
		/// <returns>name / value pair</returns>
		NameValuePair GetParameterByName(string name);

		/// <summary>Returns the total count of parameters.</summary>
		/// <remarks>Returns the total count of parameters.</remarks>
		/// <returns>parameter count</returns>
		int GetParameterCount();

		/// <summary>Returns parameter with the given index.</summary>
		/// <remarks>Returns parameter with the given index.</remarks>
		/// <param name="index">index</param>
		/// <returns>name / value pair</returns>
		NameValuePair GetParameter(int index);
	}
}
