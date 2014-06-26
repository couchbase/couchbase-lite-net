//
// PersonaAuthorizer.cs
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
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Auth
{
	public class PersonaAuthorizer : Authorizer
	{
		public const string LoginParameterAssertion = "assertion";

		private static IDictionary<IList<string>, string> assertions;

		public const string AssertionFieldEmail = "email";

		public const string AssertionFieldOrigin = "origin";

		public const string AssertionFieldExpiration = "exp";

		public const string QueryParameter = "personaAssertion";

		private bool skipAssertionExpirationCheck;

		private string emailAddress;

		public PersonaAuthorizer(string emailAddress)
		{
			// set to true to skip checking whether assertions have expired (useful for testing)
			this.emailAddress = emailAddress;
		}

		public virtual void SetSkipAssertionExpirationCheck(bool skipAssertionExpirationCheck
			)
		{
			this.skipAssertionExpirationCheck = skipAssertionExpirationCheck;
		}

		public virtual bool IsSkipAssertionExpirationCheck()
		{
			return skipAssertionExpirationCheck;
		}

		public virtual string GetEmailAddress()
		{
			return emailAddress;
		}

		protected internal virtual bool IsAssertionExpired(IDictionary<string, object> parsedAssertion
			)
		{
			if (this.IsSkipAssertionExpirationCheck() == true)
			{
				return false;
			}
			DateTime exp;
			exp = (DateTime)parsedAssertion.Get(AssertionFieldExpiration);
			DateTime now = new DateTime();
			if (exp.Before(now))
			{
				Log.W(Database.Tag, string.Format("%s assertion for %s expired: %s", this.GetType
					(), this.emailAddress, exp));
				return true;
			}
			return false;
		}

		public virtual string AssertionForSite(Uri site)
		{
			string assertion = AssertionForEmailAndSite(this.emailAddress, site);
			if (assertion == null)
			{
				Log.W(Database.Tag, string.Format("%s %s no assertion found for: %s", this.GetType
					(), this.emailAddress, site));
				return null;
			}
			IDictionary<string, object> result = ParseAssertion(assertion);
			if (IsAssertionExpired(result))
			{
				return null;
			}
			return assertion;
		}

		public override bool UsesCookieBasedLogin()
		{
			return true;
		}

		public override IDictionary<string, string> LoginParametersForSite(Uri site)
		{
			IDictionary<string, string> loginParameters = new Dictionary<string, string>();
			string assertion = AssertionForSite(site);
			if (assertion != null)
			{
				loginParameters.Put(LoginParameterAssertion, assertion);
				return loginParameters;
			}
			else
			{
				return null;
			}
		}

		public override string LoginPathForSite(Uri site)
		{
			return "/_persona";
		}

		public static string RegisterAssertion(string assertion)
		{
			lock (typeof(PersonaAuthorizer))
			{
				string email;
				string origin;
				IDictionary<string, object> result = ParseAssertion(assertion);
				email = (string)result.Get(AssertionFieldEmail);
				origin = (string)result.Get(AssertionFieldOrigin);
				// Normalize the origin URL string:
				try
				{
					Uri originURL = new Uri(origin);
					if (origin == null)
					{
						throw new ArgumentException("Invalid assertion, origin was null");
					}
					origin = originURL.ToExternalForm().ToLower();
				}
				catch (UriFormatException e)
				{
					string message = "Error registering assertion: " + assertion;
					Log.E(Database.Tag, message, e);
					throw new ArgumentException(message, e);
				}
				return RegisterAssertion(assertion, email, origin);
			}
		}

		/// <summary>
		/// don't use this!! this was factored out for testing purposes, and had to be
		/// made public since tests are in their own package.
		/// </summary>
		/// <remarks>
		/// don't use this!! this was factored out for testing purposes, and had to be
		/// made public since tests are in their own package.
		/// </remarks>
		public static string RegisterAssertion(string assertion, string email, string origin
			)
		{
			lock (typeof(PersonaAuthorizer))
			{
				IList<string> key = new AList<string>();
				key.AddItem(email);
				key.AddItem(origin);
				if (assertions == null)
				{
					assertions = new Dictionary<IList<string>, string>();
				}
				Log.D(Database.Tag, "PersonaAuthorizer registering key: " + key);
				assertions.Put(key, assertion);
				return email;
			}
		}

		public static IDictionary<string, object> ParseAssertion(string assertion)
		{
			// https://github.com/mozilla/id-specs/blob/prod/browserid/index.md
			// http://self-issued.info/docs/draft-jones-json-web-token-04.html
			IDictionary<string, object> result = new Dictionary<string, object>();
			string[] components = assertion.Split("\\.");
			// split on "."
			if (components.Length < 4)
			{
				throw new ArgumentException("Invalid assertion given, only " + components.Length 
					+ " found.  Expected 4+");
			}
			string component1Decoded = Sharpen.Runtime.GetStringForBytes(Base64.Decode(components
				[1], Base64.Default));
			string component3Decoded = Sharpen.Runtime.GetStringForBytes(Base64.Decode(components
				[3], Base64.Default));
			try
			{
				ObjectWriter mapper = new ObjectWriter();
				IDictionary<object, object> component1Json = mapper.ReadValue<IDictionary>(component1Decoded
					);
				IDictionary<object, object> principal = (IDictionary<object, object>)component1Json
					.Get("principal");
				result.Put(AssertionFieldEmail, principal.Get("email"));
				IDictionary<object, object> component3Json = mapper.ReadValue<IDictionary>(component3Decoded
					);
				result.Put(AssertionFieldOrigin, component3Json.Get("aud"));
				long expObject = (long)component3Json.Get("exp");
				Log.D(Database.Tag, "PersonaAuthorizer exp: " + expObject + " class: " + expObject
					.GetType());
				DateTime expDate = Sharpen.Extensions.CreateDate(expObject);
				result.Put(AssertionFieldExpiration, expDate);
			}
			catch (IOException e)
			{
				string message = "Error parsing assertion: " + assertion;
				Log.E(Database.Tag, message, e);
				throw new ArgumentException(message, e);
			}
			return result;
		}

		public static string AssertionForEmailAndSite(string email, Uri site)
		{
			IList<string> key = new AList<string>();
			key.AddItem(email);
			key.AddItem(site.ToExternalForm().ToLower());
			Log.D(Database.Tag, "PersonaAuthorizer looking up key: " + key + " from list of assertions"
				);
			return assertions.Get(key);
		}
	}
}
