// 
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
//using System;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Support
{
	/// <summary>
	/// A wrapper around a json byte array that will parse the data
	/// as lat as possible
	/// </summary>
	public class JsonDocument
	{
		private readonly byte[] json;

		private object cached = null;

		public JsonDocument(byte[] json)
		{
			this.json = json;
		}

		//Return a JSON object from the json data
		//If the Json starts with  '{' or a '[' then no parsing takes place and the
		//data is wrapped in a LazyJsonObject or a LazyJsonArray which will delay parsing until
		//values are requested
		public virtual object JsonObject()
		{
			if (json == null)
			{
				return null;
			}
			if (cached == null)
			{
				object tmp = null;
				if (json[0] == '{')
				{
					tmp = new LazyJsonObject<string, object>(json);
				}
				else
				{
					if (json[0] == '[')
					{
						tmp = new LazyJsonArray<object>(json);
					}
					else
					{
						try
						{
							tmp = Manager.GetObjectMapper().ReadValue<object>(json);
						}
						catch (Exception e)
						{
							//cached will remain null
							Log.W(Database.Tag, "Exception parsing json", e);
						}
					}
				}
				cached = tmp;
			}
			return cached;
		}
	}
}
