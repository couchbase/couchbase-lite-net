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
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Util;
using Sharpen;
using System.Text;

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
            if (exp < now)
			{
                Log.W(Database.Tag, string.Format("{0} assertion for {1} expired: {2}", GetType(), emailAddress, exp));
				return true;
			}
			return false;
		}

		public virtual string AssertionForSite(Uri site)
		{
            var assertion = AssertionForEmailAndSite(emailAddress, site);
			if (assertion == null)
			{
                Log.W(Database.Tag, String.Format("{0} {1} no assertion found for: {2}", GetType(), emailAddress, site));
				return null;
			}

            var result = ParseAssertion(assertion);
			return IsAssertionExpired (result) ? null : assertion;
		}

        public override bool UsesCookieBasedLogin {
            get { return true; }
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
                    origin = originURL.ToString().ToLower();
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
            var result = new Dictionary<string, object>();
            var components = assertion.Split('.');
			// split on "."
			if (components.Length < 4)
			{
				throw new ArgumentException("Invalid assertion given, only " + components.Length 
					+ " found.  Expected 4+");
			}

            var component1Decoded = Encoding.UTF8.GetString(Convert.FromBase64String(components[1]));
            var component3Decoded = Encoding.UTF8.GetString(Convert.FromBase64String(components[3]));
			try
			{
                var mapper = Manager.GetObjectMapper();

                var component1Json = mapper.ReadValue<IDictionary<Object, Object>>(component1Decoded);
                var principal = (IDictionary<Object, Object>)component1Json.Get("principal");

				result.Put(AssertionFieldEmail, principal.Get("email"));

                var component3Json = mapper.ReadValue<IDictionary<Object, Object>>(component3Decoded);
				result.Put(AssertionFieldOrigin, component3Json.Get("aud"));

                var expObject = (long)component3Json.Get("exp");
				Log.D(Database.Tag, "PersonaAuthorizer exp: " + expObject + " class: " + expObject.GetType());
                var expDate = Extensions.CreateDate(expObject);
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
            key.AddItem(site.ToString().ToLower());
			Log.D(Database.Tag, "PersonaAuthorizer looking up key: " + key + " from list of assertions"
				);
			return assertions.Get(key);
		}
	}
}
