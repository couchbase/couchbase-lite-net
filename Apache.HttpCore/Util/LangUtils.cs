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

using Sharpen;

namespace Org.Apache.Http.Util
{
	/// <summary>
	/// A set of utility methods to help produce consistent
	/// <see cref="object.Equals(object)">equals</see>
	/// and
	/// <see cref="object.GetHashCode()">hashCode</see>
	/// methods.
	/// </summary>
	/// <since>4.0</since>
	public sealed class LangUtils
	{
		public const int HashSeed = 17;

		public const int HashOffset = 37;

		/// <summary>Disabled default constructor.</summary>
		/// <remarks>Disabled default constructor.</remarks>
		private LangUtils()
		{
		}

		public static int HashCode(int seed, int hashcode)
		{
			return seed * HashOffset + hashcode;
		}

		public static int HashCode(int seed, bool b)
		{
			return HashCode(seed, b ? 1 : 0);
		}

		public static int HashCode(int seed, object obj)
		{
			return HashCode(seed, obj != null ? obj.GetHashCode() : 0);
		}

		/// <summary>Check if two objects are equal.</summary>
		/// <remarks>Check if two objects are equal.</remarks>
		/// <param name="obj1">
		/// first object to compare, may be
		/// <code>null</code>
		/// </param>
		/// <param name="obj2">
		/// second object to compare, may be
		/// <code>null</code>
		/// </param>
		/// <returns>
		/// 
		/// <code>true</code>
		/// if the objects are equal or both null
		/// </returns>
		public static bool Equals(object obj1, object obj2)
		{
			return obj1 == null ? obj2 == null : obj1.Equals(obj2);
		}

		/// <summary>Check if two object arrays are equal.</summary>
		/// <remarks>
		/// Check if two object arrays are equal.
		/// <p>
		/// <ul>
		/// <li>If both parameters are null, return
		/// <code>true</code>
		/// </li>
		/// <li>If one parameter is null, return
		/// <code>false</code>
		/// </li>
		/// <li>If the array lengths are different, return
		/// <code>false</code>
		/// </li>
		/// <li>Compare array elements using .equals(); return
		/// <code>false</code>
		/// if any comparisons fail.</li>
		/// <li>Return
		/// <code>true</code>
		/// </li>
		/// </ul>
		/// </remarks>
		/// <param name="a1">
		/// first array to compare, may be
		/// <code>null</code>
		/// </param>
		/// <param name="a2">
		/// second array to compare, may be
		/// <code>null</code>
		/// </param>
		/// <returns>
		/// 
		/// <code>true</code>
		/// if the arrays are equal or both null
		/// </returns>
		public static bool Equals(object[] a1, object[] a2)
		{
			if (a1 == null)
			{
				return a2 == null;
			}
			else
			{
				if (a2 != null && a1.Length == a2.Length)
				{
					for (int i = 0; i < a1.Length; i++)
					{
						if (!Equals(a1[i], a2[i]))
						{
							return false;
						}
					}
					return true;
				}
				else
				{
					return false;
				}
			}
		}
	}
}
