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

using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http.Params
{
	/// <summary>
	/// HttpParams interface represents a collection of immutable values that define
	/// a runtime behavior of a component.
	/// </summary>
	/// <remarks>
	/// HttpParams interface represents a collection of immutable values that define
	/// a runtime behavior of a component. HTTP parameters should be simple objects:
	/// integers, doubles, strings, collections and objects that remain immutable
	/// at runtime. HttpParams is expected to be used in 'write once - read many' mode.
	/// Once initialized, HTTP parameters are not expected to mutate in
	/// the course of HTTP message processing.
	/// <p>
	/// The purpose of this interface is to define a behavior of other components.
	/// Usually each complex component has its own HTTP parameter collection.
	/// <p>
	/// Instances of this interface can be linked together to form a hierarchy.
	/// In the simplest form one set of parameters can use content of another one
	/// to obtain default values of parameters not present in the local set.
	/// </remarks>
	/// <since>4.0</since>
	[System.ObsoleteAttribute(@"(4.3) use configuration classes provided 'org.apache.http.config' and 'org.apache.http.client.config'"
		)]
	public interface HttpParams
	{
		/// <summary>Obtains the value of the given parameter.</summary>
		/// <remarks>Obtains the value of the given parameter.</remarks>
		/// <param name="name">the parent name.</param>
		/// <returns>
		/// an object that represents the value of the parameter,
		/// <code>null</code> if the parameter is not set or if it
		/// is explicitly set to <code>null</code>
		/// </returns>
		/// <seealso cref="SetParameter(string, object)">SetParameter(string, object)</seealso>
		object GetParameter(string name);

		/// <summary>Assigns the value to the parameter with the given name.</summary>
		/// <remarks>Assigns the value to the parameter with the given name.</remarks>
		/// <param name="name">parameter name</param>
		/// <param name="value">parameter value</param>
		HttpParams SetParameter(string name, object value);

		/// <summary>Creates a copy of these parameters.</summary>
		/// <remarks>Creates a copy of these parameters.</remarks>
		/// <returns>a new set of parameters holding the same values as this one</returns>
		HttpParams Copy();

		/// <summary>Removes the parameter with the specified name.</summary>
		/// <remarks>Removes the parameter with the specified name.</remarks>
		/// <param name="name">parameter name</param>
		/// <returns>true if the parameter existed and has been removed, false else.</returns>
		bool RemoveParameter(string name);

		/// <summary>
		/// Returns a
		/// <see cref="long">long</see>
		/// parameter value with the given name.
		/// If the parameter is not explicitly set, the default value is returned.
		/// </summary>
		/// <param name="name">the parent name.</param>
		/// <param name="defaultValue">the default value.</param>
		/// <returns>
		/// a
		/// <see cref="long">long</see>
		/// that represents the value of the parameter.
		/// </returns>
		/// <seealso cref="SetLongParameter(string, long)">SetLongParameter(string, long)</seealso>
		long GetLongParameter(string name, long defaultValue);

		/// <summary>
		/// Assigns a
		/// <see cref="long">long</see>
		/// to the parameter with the given name
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="value">parameter value</param>
		HttpParams SetLongParameter(string name, long value);

		/// <summary>
		/// Returns an
		/// <see cref="int">int</see>
		/// parameter value with the given name.
		/// If the parameter is not explicitly set, the default value is returned.
		/// </summary>
		/// <param name="name">the parent name.</param>
		/// <param name="defaultValue">the default value.</param>
		/// <returns>
		/// a
		/// <see cref="int">int</see>
		/// that represents the value of the parameter.
		/// </returns>
		/// <seealso cref="SetIntParameter(string, int)">SetIntParameter(string, int)</seealso>
		int GetIntParameter(string name, int defaultValue);

		/// <summary>
		/// Assigns an
		/// <see cref="int">int</see>
		/// to the parameter with the given name
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="value">parameter value</param>
		HttpParams SetIntParameter(string name, int value);

		/// <summary>
		/// Returns a
		/// <see cref="double">double</see>
		/// parameter value with the given name.
		/// If the parameter is not explicitly set, the default value is returned.
		/// </summary>
		/// <param name="name">the parent name.</param>
		/// <param name="defaultValue">the default value.</param>
		/// <returns>
		/// a
		/// <see cref="double">double</see>
		/// that represents the value of the parameter.
		/// </returns>
		/// <seealso cref="SetDoubleParameter(string, double)">SetDoubleParameter(string, double)
		/// 	</seealso>
		double GetDoubleParameter(string name, double defaultValue);

		/// <summary>
		/// Assigns a
		/// <see cref="double">double</see>
		/// to the parameter with the given name
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="value">parameter value</param>
		HttpParams SetDoubleParameter(string name, double value);

		/// <summary>
		/// Returns a
		/// <see cref="bool">bool</see>
		/// parameter value with the given name.
		/// If the parameter is not explicitly set, the default value is returned.
		/// </summary>
		/// <param name="name">the parent name.</param>
		/// <param name="defaultValue">the default value.</param>
		/// <returns>
		/// a
		/// <see cref="bool">bool</see>
		/// that represents the value of the parameter.
		/// </returns>
		/// <seealso cref="SetBooleanParameter(string, bool)">SetBooleanParameter(string, bool)
		/// 	</seealso>
		bool GetBooleanParameter(string name, bool defaultValue);

		/// <summary>
		/// Assigns a
		/// <see cref="bool">bool</see>
		/// to the parameter with the given name
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="value">parameter value</param>
		HttpParams SetBooleanParameter(string name, bool value);

		/// <summary>Checks if a boolean parameter is set to <code>true</code>.</summary>
		/// <remarks>Checks if a boolean parameter is set to <code>true</code>.</remarks>
		/// <param name="name">parameter name</param>
		/// <returns>
		/// <tt>true</tt> if the parameter is set to value <tt>true</tt>,
		/// <tt>false</tt> if it is not set or set to <code>false</code>
		/// </returns>
		bool IsParameterTrue(string name);

		/// <summary>Checks if a boolean parameter is not set or <code>false</code>.</summary>
		/// <remarks>Checks if a boolean parameter is not set or <code>false</code>.</remarks>
		/// <param name="name">parameter name</param>
		/// <returns>
		/// <tt>true</tt> if the parameter is either not set or
		/// set to value <tt>false</tt>,
		/// <tt>false</tt> if it is set to <code>true</code>
		/// </returns>
		bool IsParameterFalse(string name);
	}
}
