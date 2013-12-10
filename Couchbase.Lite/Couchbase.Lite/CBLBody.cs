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
using System.Collections;
using System.Collections.Generic;
using Couchbase;
using Couchbase.Util;
using Newtonsoft.Json;
using Sharpen;

namespace Couchbase
{
	/// <summary>A request/response/document body, stored as either JSON or a Map<String,Object>
	/// 	</summary>
	public class CBLBody
	{
		private byte[] json;

		private object @object;

		private bool error = false;

		public CBLBody(byte[] json)
		{
			this.json = json;
		}

		public CBLBody(IDictionary<string, object> properties)
		{
			this.@object = properties;
		}

		public CBLBody(IList<object> array)
		{
			this.@object = array;
		}

		public static Couchbase.CBLBody BodyWithProperties(IDictionary<string, object> properties
			)
		{
			Couchbase.CBLBody result = new Couchbase.CBLBody(properties);
			return result;
		}

		public static Couchbase.CBLBody BodyWithJSON(byte[] json)
		{
			Couchbase.CBLBody result = new Couchbase.CBLBody(json);
			return result;
		}

		public virtual bool IsValidJSON()
		{
			if (@object == null)
			{
				GetObject();
			}
			// Yes, this is just like asObject except it doesn't warn.
			if (json == null && !error)
			{
				try
				{
					json = CBLServer.GetObjectMapper().WriteValueAsBytes(@object);
				}
				catch (Exception)
				{
					error = true;
				}
			}
			return (@object != null);
		}

		public virtual byte[] GetJson()
		{
			if (json == null && !error)
			{
				try
				{
					json = CBLServer.GetObjectMapper().WriteValueAsBytes(@object);
				}
				catch (Exception)
				{
					Log.W(CBLDatabase.Tag, "CBLBody: couldn't convert JSON");
					error = true;
				}
			}
			return json;
		}

		public virtual byte[] GetPrettyJson()
		{
			object properties = GetObject();
			if (properties != null)
			{
				ObjectWriter writer = CBLServer.GetObjectMapper().WriterWithDefaultPrettyPrinter(
					);
				try
				{
					json = writer.WriteValueAsBytes(properties);
				}
				catch (Exception)
				{
					error = true;
				}
			}
			return GetJson();
		}

		public virtual string GetJSONString()
		{
			return Sharpen.Runtime.GetStringForBytes(GetJson());
		}

		public virtual object GetObject()
		{
			if (@object == null && !error)
			{
				try
				{
					if (json != null)
					{
						@object = CBLServer.GetObjectMapper().ReadValue<IDictionary>(json);
					}
				}
				catch (Exception e)
				{
					Log.W(CBLDatabase.Tag, "CBLBody: couldn't parse JSON: " + Sharpen.Runtime.GetStringForBytes
						(json), e);
					error = true;
				}
			}
			return @object;
		}

		public virtual IDictionary<string, object> GetProperties()
		{
			object @object = GetObject();
			if (@object is IDictionary)
			{
				return (IDictionary<string, object>)@object;
			}
			return null;
		}

		public virtual object GetPropertyForKey(string key)
		{
			IDictionary<string, object> theProperties = GetProperties();
			return theProperties.Get(key);
		}

		public virtual bool IsError()
		{
			return error;
		}
	}
}
