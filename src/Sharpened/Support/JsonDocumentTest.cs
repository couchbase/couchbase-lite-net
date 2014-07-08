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
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class JsonDocumentTest : LiteTestCase
	{
		/// <exception cref="System.Exception"></exception>
		public virtual void TestJsonObject()
		{
			IDictionary<string, object> dict = new Dictionary<string, object>();
			dict.Put("id", "01234567890");
			dict.Put("foo", "bar");
			dict.Put("int", 5);
			dict.Put("double", 3.5);
			dict.Put("bool", true);
			dict.Put("date", new DateTime().ToString());
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(dict);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(dict, jsdoc.JsonObject());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestJsonArray()
		{
			IList<object> array = new AList<object>();
			array.AddItem("01234567890");
			array.AddItem("bar");
			array.AddItem(5);
			array.AddItem(3.5);
			array.AddItem(true);
			array.AddItem(new DateTime().ToString());
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(array);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(array, jsdoc.JsonObject());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestStringFragment()
		{
			string fragment = "01234567890";
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(fragment);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(fragment, jsdoc.JsonObject());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestBooleanFragment()
		{
			bool fragment = true;
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(fragment);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(fragment, jsdoc.JsonObject());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestIntegerFragment()
		{
			int fragment = 5;
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(fragment);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(fragment, jsdoc.JsonObject());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDoubleFragment()
		{
			double fragment = 3.5;
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(fragment);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(fragment, jsdoc.JsonObject());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestDateFragment()
		{
			DateTime fragment = new DateTime();
			ObjectWriter mapper = new ObjectWriter();
			byte[] json = mapper.WriteValueAsBytes(fragment);
			JsonDocument jsdoc = new JsonDocument(json);
			NUnit.Framework.Assert.AreEqual(fragment, Sharpen.Extensions.CreateDate((long)jsdoc
				.JsonObject()));
		}
	}
}
