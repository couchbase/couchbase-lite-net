//
// FacebookAuthorizer.cs
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
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Auth
{
	public class FacebookAuthorizer : Authorizer
	{
		public const string LoginParameterAccessToken = "access_token";

		public const string QueryParameter = "facebookAccessToken";

		public const string QueryParameterEmail = "email";

		private static IDictionary<IList<string>, string> accessTokens;

		private string emailAddress;

		public FacebookAuthorizer(string emailAddress)
		{
			this.emailAddress = emailAddress;
		}

		public override bool UsesCookieBasedLogin()
		{
			return true;
		}

		public override IDictionary<string, string> LoginParametersForSite(Uri site)
		{
			IDictionary<string, string> loginParameters = new Dictionary<string, string>();
			try
			{
				string accessToken = AccessTokenForEmailAndSite(this.emailAddress, site);
				if (accessToken != null)
				{
					loginParameters.Put(LoginParameterAccessToken, accessToken);
					return loginParameters;
				}
				else
				{
					return null;
				}
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error looking login parameters for site", e);
			}
			return null;
		}

		public override string LoginPathForSite(Uri site)
		{
			return "/_facebook";
		}

		public static string RegisterAccessToken(string accessToken, string email, string
			 origin)
		{
			lock (typeof(FacebookAuthorizer))
			{
				IList<string> key = new AList<string>();
				key.AddItem(email);
				key.AddItem(origin);
				if (accessTokens == null)
				{
					accessTokens = new Dictionary<IList<string>, string>();
				}
				Log.D(Database.Tag, "FacebookAuthorizer registering key: " + key);
				accessTokens.Put(key, accessToken);
				return email;
			}
		}

		public static string AccessTokenForEmailAndSite(string email, Uri site)
		{
			try
			{
				IList<string> key = new AList<string>();
				key.AddItem(email);
				key.AddItem(site.ToExternalForm().ToLower());
				Log.D(Database.Tag, "FacebookAuthorizer looking up key: " + key + " from list of access tokens"
					);
				return accessTokens.Get(key);
			}
			catch (Exception e)
			{
				Log.E(Database.Tag, "Error looking up access token", e);
			}
			return null;
		}
	}
}
