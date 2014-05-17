//
// Body.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Sharpen;

namespace Couchbase.Lite.Internal
{
	/// <summary>A request/response/document body, stored as either JSON or a Map<String,Object>
	/// 	</summary>
	public class Body
	{
		private byte[] json;

		private object @object;

		public Body(byte[] json)
		{
			this.json = json;
		}

		public Body(IDictionary<string, object> properties)
		{
			this.@object = properties;
		}

		public Body(IList<object> array)
		{
			this.@object = array;
		}

		public static Couchbase.Lite.Internal.Body BodyWithProperties(IDictionary<string, 
			object> properties)
		{
			Couchbase.Lite.Internal.Body result = new Couchbase.Lite.Internal.Body(properties
				);
			return result;
		}

		public static Couchbase.Lite.Internal.Body BodyWithJSON(byte[] json)
		{
			Couchbase.Lite.Internal.Body result = new Couchbase.Lite.Internal.Body(json);
			return result;
		}

		public virtual byte[] GetJson()
		{
			if (json == null)
			{
				LazyLoadJsonFromObject();
			}
			return json;
		}

		private void LazyLoadJsonFromObject()
		{
			if (@object == null)
			{
				throw new InvalidOperationException("Both json and object are null for this body: "
					 + this);
			}
			try
			{
				json = Manager.GetObjectMapper().WriteValueAsBytes(@object);
			}
			catch (IOException e)
			{
				throw new RuntimeException(e);
			}
		}

		public virtual object GetObject()
		{
			if (@object == null)
			{
				LazyLoadObjectFromJson();
			}
			return @object;
		}

		private void LazyLoadObjectFromJson()
		{
			if (json == null)
			{
				throw new InvalidOperationException("Both object and json are null for this body: "
					 + this);
			}
			try
			{
				@object = Manager.GetObjectMapper().ReadValue<object>(json);
			}
			catch (IOException e)
			{
				throw new RuntimeException(e);
			}
		}

		public virtual bool IsValidJSON()
		{
			if (@object == null)
			{
				bool gotException = false;
				if (json == null)
				{
					throw new InvalidOperationException("Both object and json are null for this body: "
						 + this);
				}
				try
				{
					@object = Manager.GetObjectMapper().ReadValue<object>(json);
				}
				catch (IOException)
				{
				}
			}
			return @object != null;
		}

		public virtual byte[] GetPrettyJson()
		{
			object properties = GetObject();
			if (properties != null)
			{
				ObjectWriter writer = Manager.GetObjectMapper().WriterWithDefaultPrettyPrinter();
				try
				{
					json = writer.WriteValueAsBytes(properties);
				}
				catch (IOException e)
				{
					throw new RuntimeException(e);
				}
			}
			return GetJson();
		}

		public virtual string GetJSONString()
		{
			return Sharpen.Runtime.GetStringForBytes(GetJson());
		}

		public virtual IDictionary<string, object> GetProperties()
		{
			object @object = GetObject();
			if (@object is IDictionary)
			{
				IDictionary<string, object> map = (IDictionary<string, object>)@object;
				return Sharpen.Collections.UnmodifiableMap(map);
			}
			return null;
		}

		public virtual object GetPropertyForKey(string key)
		{
			IDictionary<string, object> theProperties = GetProperties();
			if (theProperties == null)
			{
				return null;
			}
			return theProperties.Get(key);
		}
	}
}
