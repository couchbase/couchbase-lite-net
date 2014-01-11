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
using System.Text;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Storage
{
	public sealed class ContentValues
	{
		public const string Tag = "ContentValues";

		/// <summary>Holds the actual values</summary>
		private readonly Dictionary<string, object> mValues;

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
            mValues = new Dictionary<String, Object>(size);
		}

		/// <summary>Creates a set of values copied from the given set</summary>
		/// <param name="from">the values to copy</param>
		public ContentValues(ContentValues from)
		{
			mValues = new Dictionary<string, object>(from.mValues);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is ContentValues))
			{
				return false;
			}
			return mValues.Equals(((ContentValues)obj).mValues);
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
        public void Put(string key, IEnumerable<Byte> value)
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
        public Nullable<Int64> GetAsLong(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return value != null ? ((Int64?)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
                        return Int64.Parse(value.ToString());
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
        public Nullable<Int32> GetAsInteger(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return value != null ? ((Int32?)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
                        return Int32.Parse(value.ToString());
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
        public Nullable<Int16> GetAsShort(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return value != null ? ((Int16?)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
                        return Int16.Parse(value.ToString());
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
        public Nullable<Byte> GetAsByte(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return value != null ? ((Byte?)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
                        return Byte.Parse(value.ToString());
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
        public Nullable<Double> GetAsDouble(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return value != null ? ((Double?)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
                        return Double.Parse(value.ToString());
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
        public Nullable<Single> GetAsFloat(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return value != null ? ((Single?)value) : null;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
					try
					{
                        return Single.Parse(value.ToString());
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
        public Nullable<Boolean> GetAsBoolean(string key)
		{
			object value = mValues.Get(key);
			try
			{
                return (Boolean)value;
			}
			catch (InvalidCastException e)
			{
				if (value is CharSequence)
				{
                    return Boolean.Parse(value.ToString());
				}
				else
				{
                    if (value is IConvertible)
					{
                        return Convert.ToBoolean(value);
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
