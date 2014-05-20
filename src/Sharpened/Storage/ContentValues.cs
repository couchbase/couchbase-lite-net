//
// ContentValues.cs
// 
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Storage
{
	public sealed class ContentValues
	{
		public const string Tag = "ContentValues";

		/// <summary>Holds the actual values</summary>
		private Dictionary<string, object> mValues;

		/// <summary>Creates an empty set of values using the default initial size</summary>
		public ContentValues()
		{
			// COPY: Copied from android.content.ContentValues
			// Choosing a default size of 8 based on analysis of typical
			// consumption by applications.
			mValues = new Dictionary<string, object>(8);
		}

		/// <summary>Creates an empty set of values using the given initial size</summary>
		/// <param name="size">the initial size of the set of values</param>
		public ContentValues(int size)
		{
			mValues = new Dictionary<string, object>(size, 1.0f);
		}

		/// <summary>Creates a set of values copied from the given set</summary>
		/// <param name="from">the values to copy</param>
		public ContentValues(Couchbase.Lite.Storage.ContentValues from)
		{
			mValues = new Dictionary<string, object>(from.mValues);
		}

		public override bool Equals(object @object)
		{
			if (!(@object is Couchbase.Lite.Storage.ContentValues))
			{
				return false;
			}
			return mValues.Equals(((Couchbase.Lite.Storage.ContentValues)@object).mValues);
		}

		public override int GetHashCode()
		{
			return mValues.GetHashCode();
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, string value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds all values from the passed in ContentValues.</summary>
		/// <remarks>Adds all values from the passed in ContentValues.</remarks>
		/// <param name="other">the ContentValues from which to copy</param>
		public void PutAll(Couchbase.Lite.Storage.ContentValues other)
		{
			mValues.PutAll(other.mValues);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, byte value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, short value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, int value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, long value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, float value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, double value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, bool value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a value to the set.</summary>
		/// <remarks>Adds a value to the set.</remarks>
		/// <param name="key">the name of the value to put</param>
		/// <param name="value">the data for the value to put</param>
		public void Put(string key, byte[] value)
		{
			mValues.Put(key, value);
		}

		/// <summary>Adds a null value to the set.</summary>
		/// <remarks>Adds a null value to the set.</remarks>
		/// <param name="key">the name of the value to make null</param>
		public void PutNull(string key)
		{
			mValues.Put(key, null);
		}

		/// <summary>Returns the number of values.</summary>
		/// <remarks>Returns the number of values.</remarks>
		/// <returns>the number of values</returns>
		public int Size()
		{
			return mValues.Count;
		}

		/// <summary>Remove a single value.</summary>
		/// <remarks>Remove a single value.</remarks>
		/// <param name="key">the name of the value to remove</param>
		public void Remove(string key)
		{
			Sharpen.Collections.Remove(mValues, key);
		}

		/// <summary>Removes all values.</summary>
		/// <remarks>Removes all values.</remarks>
		public void Clear()
		{
			mValues.Clear();
		}

		/// <summary>Returns true if this object has the named value.</summary>
		/// <remarks>Returns true if this object has the named value.</remarks>
		/// <param name="key">the value to check for</param>
		/// <returns>
		/// 
		/// <code>true</code>
		/// if the value is present,
		/// <code>false</code>
		/// otherwise
		/// </returns>
		public bool ContainsKey(string key)
		{
			return mValues.ContainsKey(key);
		}

		/// <summary>Gets a value.</summary>
		/// <remarks>
		/// Gets a value. Valid value types are
		/// <see cref="string">string</see>
		/// ,
		/// <see cref="bool">bool</see>
		/// , and
		/// <see cref="Sharpen.Number">Sharpen.Number</see>
		/// implementations.
		/// </remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the data for the value</returns>
		public object Get(string key)
		{
			return mValues.Get(key);
		}

		/// <summary>Gets a value and converts it to a String.</summary>
		/// <remarks>Gets a value and converts it to a String.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the String for the value</returns>
		public string GetAsString(string key)
		{
			object value = mValues.Get(key);
			return value != null ? value.ToString() : null;
		}

		/// <summary>Gets a value and converts it to a Long.</summary>
		/// <remarks>Gets a value and converts it to a Long.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Long value, or null if the value is missing or cannot be converted</returns>
		public long GetAsLong(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return value != null ? ((Number)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
						return Sharpen.Extensions.ValueOf(value.ToString());
					}
					catch (FormatException)
					{
						Log.E(Tag, "Cannot parse Long value for " + value + " at key " + key);
						return null;
					}
				}
				else
				{
					Log.E(Tag, "Cannot cast value for " + key + " to a Long: " + value, e);
					return null;
				}
			}
		}

		/// <summary>Gets a value and converts it to an Integer.</summary>
		/// <remarks>Gets a value and converts it to an Integer.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Integer value, or null if the value is missing or cannot be converted
		/// 	</returns>
		public int GetAsInteger(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return value != null ? ((Number)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
						return Sharpen.Extensions.ValueOf(value.ToString());
					}
					catch (FormatException)
					{
						Log.E(Tag, "Cannot parse Integer value for " + value + " at key " + key);
						return null;
					}
				}
				else
				{
					Log.E(Tag, "Cannot cast value for " + key + " to a Integer: " + value, e);
					return null;
				}
			}
		}

		/// <summary>Gets a value and converts it to a Short.</summary>
		/// <remarks>Gets a value and converts it to a Short.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Short value, or null if the value is missing or cannot be converted</returns>
		public short GetAsShort(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return value != null ? ((Number)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
						return short.ValueOf(value.ToString());
					}
					catch (FormatException)
					{
						Log.E(Tag, "Cannot parse Short value for " + value + " at key " + key);
						return null;
					}
				}
				else
				{
					Log.E(Tag, "Cannot cast value for " + key + " to a Short: " + value, e);
					return null;
				}
			}
		}

		/// <summary>Gets a value and converts it to a Byte.</summary>
		/// <remarks>Gets a value and converts it to a Byte.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Byte value, or null if the value is missing or cannot be converted</returns>
		public byte GetAsByte(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return value != null ? ((Number)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
						return byte.ValueOf(value.ToString());
					}
					catch (FormatException)
					{
						Log.E(Tag, "Cannot parse Byte value for " + value + " at key " + key);
						return null;
					}
				}
				else
				{
					Log.E(Tag, "Cannot cast value for " + key + " to a Byte: " + value, e);
					return null;
				}
			}
		}

		/// <summary>Gets a value and converts it to a Double.</summary>
		/// <remarks>Gets a value and converts it to a Double.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Double value, or null if the value is missing or cannot be converted
		/// 	</returns>
		public double GetAsDouble(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return value != null ? ((Number)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
						return double.ValueOf(value.ToString());
					}
					catch (FormatException)
					{
						Log.E(Tag, "Cannot parse Double value for " + value + " at key " + key);
						return null;
					}
				}
				else
				{
					Log.E(Tag, "Cannot cast value for " + key + " to a Double: " + value, e);
					return null;
				}
			}
		}

		/// <summary>Gets a value and converts it to a Float.</summary>
		/// <remarks>Gets a value and converts it to a Float.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Float value, or null if the value is missing or cannot be converted</returns>
		public float GetAsFloat(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return value != null ? ((Number)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
						return float.ValueOf(value.ToString());
					}
					catch (FormatException)
					{
						Log.E(Tag, "Cannot parse Float value for " + value + " at key " + key);
						return null;
					}
				}
				else
				{
					Log.E(Tag, "Cannot cast value for " + key + " to a Float: " + value, e);
					return null;
				}
			}
		}

		/// <summary>Gets a value and converts it to a Boolean.</summary>
		/// <remarks>Gets a value and converts it to a Boolean.</remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the Boolean value, or null if the value is missing or cannot be converted
		/// 	</returns>
		public bool GetAsBoolean(string key)
		{
			object value = mValues.Get(key);
			try
			{
				return (bool)value;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					return Sharpen.Extensions.ValueOf(value.ToString());
				}
				else
				{
					if (value is Number)
					{
						return ((Number)value) != 0;
					}
					else
					{
						Log.E(Tag, "Cannot cast value for " + key + " to a Boolean: " + value, e);
						return null;
					}
				}
			}
		}

		/// <summary>Gets a value that is a byte array.</summary>
		/// <remarks>
		/// Gets a value that is a byte array. Note that this method will not convert
		/// any other types to byte arrays.
		/// </remarks>
		/// <param name="key">the value to get</param>
		/// <returns>the byte[] value, or null is the value is missing or not a byte[]</returns>
		public byte[] GetAsByteArray(string key)
		{
			object value = mValues.Get(key);
			if (value is byte[])
			{
				return (byte[])value;
			}
			else
			{
				return null;
			}
		}

		/// <summary>Returns a set of all of the keys and values</summary>
		/// <returns>a set of all of the keys and values</returns>
		public ICollection<KeyValuePair<string, object>> ValueSet()
		{
			return mValues.EntrySet();
		}

		/// <summary>Returns a set of all of the keys</summary>
		/// <returns>a set of all of the keys</returns>
		public ICollection<string> KeySet()
		{
			return mValues.Keys;
		}

		/// <summary>Returns a string containing a concise, human-readable description of this object.
		/// 	</summary>
		/// <remarks>Returns a string containing a concise, human-readable description of this object.
		/// 	</remarks>
		/// <returns>a printable representation of this object.</returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			foreach (string name in mValues.Keys)
			{
				string value = GetAsString(name);
				if (sb.Length > 0)
				{
					sb.Append(" ");
				}
				sb.Append(name + "=" + value);
			}
			return sb.ToString();
		}
	}
}
