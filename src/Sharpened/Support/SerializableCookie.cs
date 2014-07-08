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
using System.IO;
using Apache.Http.Impl.Cookie;
using Sharpen;

namespace Couchbase.Lite.Support
{
	/// <summary>
	/// A wrapper class around
	/// <see cref="Apache.Http.Cookie.Cookie">Apache.Http.Cookie.Cookie</see>
	/// and/or
	/// <see cref="Apache.Http.Impl.Cookie.BasicClientCookie">Apache.Http.Impl.Cookie.BasicClientCookie
	/// 	</see>
	/// designed for use in
	/// <see cref="PersistentCookieStore">PersistentCookieStore</see>
	/// .
	/// </summary>
	[System.Serializable]
	public class SerializableCookie
	{
		private const long serialVersionUID = 6374381828722046732L;

		[System.NonSerialized]
		private readonly Apache.Http.Cookie.Cookie cookie;

		[System.NonSerialized]
		private BasicClientCookie clientCookie;

		public SerializableCookie(Apache.Http.Cookie.Cookie cookie)
		{
			this.cookie = cookie;
		}

		public virtual Apache.Http.Cookie.Cookie GetCookie()
		{
			Apache.Http.Cookie.Cookie bestCookie = cookie;
			if (clientCookie != null)
			{
				bestCookie = clientCookie;
			}
			return bestCookie;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void WriteObject(ObjectOutputStream @out)
		{
			@out.WriteObject(cookie.GetName());
			@out.WriteObject(cookie.GetValue());
			@out.WriteObject(cookie.GetComment());
			@out.WriteObject(cookie.GetDomain());
			@out.WriteObject(cookie.GetExpiryDate());
			@out.WriteObject(cookie.GetPath());
			@out.WriteInt(cookie.GetVersion());
			@out.WriteBoolean(cookie.IsSecure());
		}

		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="System.TypeLoadException"></exception>
		private void ReadObject(ObjectInputStream @in)
		{
			string name = (string)@in.ReadObject();
			string value = (string)@in.ReadObject();
			clientCookie = new BasicClientCookie(name, value);
			clientCookie.SetComment((string)@in.ReadObject());
			clientCookie.SetDomain((string)@in.ReadObject());
			clientCookie.SetExpiryDate((DateTime)@in.ReadObject());
			clientCookie.SetPath((string)@in.ReadObject());
			clientCookie.SetVersion(@in.ReadInt());
			clientCookie.SetSecure(@in.ReadBoolean());
		}
	}
}
