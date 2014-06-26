//
// ValidationsTest.cs
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

using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class ValidationsTest : LiteTestCase
	{
		public const string Tag = "Validations";

		internal bool validationCalled = false;

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestValidations()
		{
			Validator validator = new _Validator_19(this);
			database.SetValidation("hoopy", validator);
			// POST a valid new document:
			IDictionary<string, object> props = new Dictionary<string, object>();
			props.Put("name", "Zaphod Beeblebrox");
			props.Put("towel", "velvet");
			RevisionInternal rev = new RevisionInternal(props, database);
			Status status = new Status();
			validationCalled = false;
			rev = database.PutRevision(rev, null, false, status);
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
			// PUT a valid update:
			props.Put("head_count", 3);
			rev.SetProperties(props);
			validationCalled = false;
			rev = database.PutRevision(rev, rev.GetRevId(), false, status);
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.AreEqual(Status.Created, status.GetCode());
			// PUT an invalid update:
			Sharpen.Collections.Remove(props, "towel");
			rev.SetProperties(props);
			validationCalled = false;
			bool gotExpectedError = false;
			try
			{
				rev = database.PutRevision(rev, rev.GetRevId(), false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = (e.GetCBLStatus().GetCode() == Status.Forbidden);
			}
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			// POST an invalid new document:
			props = new Dictionary<string, object>();
			props.Put("name", "Vogon");
			props.Put("poetry", true);
			rev = new RevisionInternal(props, database);
			validationCalled = false;
			gotExpectedError = false;
			try
			{
				rev = database.PutRevision(rev, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = (e.GetCBLStatus().GetCode() == Status.Forbidden);
			}
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			// PUT a valid new document with an ID:
			props = new Dictionary<string, object>();
			props.Put("_id", "ford");
			props.Put("name", "Ford Prefect");
			props.Put("towel", "terrycloth");
			rev = new RevisionInternal(props, database);
			validationCalled = false;
			rev = database.PutRevision(rev, null, false, status);
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.AreEqual("ford", rev.GetDocId());
			// DELETE a document:
			rev = new RevisionInternal(rev.GetDocId(), rev.GetRevId(), true, database);
			NUnit.Framework.Assert.IsTrue(rev.IsDeleted());
			validationCalled = false;
			rev = database.PutRevision(rev, rev.GetRevId(), false, status);
			NUnit.Framework.Assert.IsTrue(validationCalled);
			// PUT an invalid new document:
			props = new Dictionary<string, object>();
			props.Put("_id", "petunias");
			props.Put("name", "Pot of Petunias");
			rev = new RevisionInternal(props, database);
			validationCalled = false;
			gotExpectedError = false;
			try
			{
				rev = database.PutRevision(rev, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
				gotExpectedError = (e.GetCBLStatus().GetCode() == Status.Forbidden);
			}
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
		}

		private sealed class _Validator_19 : Validator
		{
			public _Validator_19(ValidationsTest _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Validate(Revision newRevision, ValidationContext context)
			{
				NUnit.Framework.Assert.IsNotNull(newRevision);
				NUnit.Framework.Assert.IsNotNull(context);
				NUnit.Framework.Assert.IsTrue(newRevision.GetProperties() != null || newRevision.
					IsDeletion());
				this._enclosing.validationCalled = true;
				bool hoopy = newRevision.IsDeletion() || (newRevision.GetProperties().Get("towel"
					) != null);
				Log.V(ValidationsTest.Tag, string.Format("--- Validating %s --> %b", newRevision.
					GetProperties(), hoopy));
				if (!hoopy)
				{
					context.Reject("Where's your towel?");
				}
			}

			private readonly ValidationsTest _enclosing;
		}
	}
}
