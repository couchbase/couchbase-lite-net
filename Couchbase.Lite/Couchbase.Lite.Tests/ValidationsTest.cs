/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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
            Database.ValidateDelegate validator = (Revision newRevision, ValidationContext context)=>
            {
                NUnit.Framework.Assert.IsNotNull(newRevision);
                NUnit.Framework.Assert.IsNotNull(context);
                NUnit.Framework.Assert.IsTrue(newRevision.Properties != null || newRevision.
                    IsDeletion);
                this._enclosing.validationCalled = true;
                bool hoopy = newRevision.IsDeletion || (newRevision.Properties.Get("towel"
                ) != null);
                Log.V(ValidationsTest.Tag, string.Format("--- Validating %s --> %b", newRevision.
                    Properties, hoopy));
                if (!hoopy)
                {
                    context.Reject("Where's your towel?");
                }
                return hoopy;
            };
			database.SetValidation("hoopy", validator);
			// POST a valid new document:
			IDictionary<string, object> props = new Dictionary<string, object>();
			props["name"] = "Zaphod Beeblebrox";
			props["towel"] = "velvet";
			RevisionInternal rev = new RevisionInternal(props, database);
			Status status = new Status();
			validationCalled = false;
			rev = database.PutRevision(rev, null, false, status);
			NUnit.Framework.Assert.IsTrue(validationCalled);
            NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
			// PUT a valid update:
			props["head_count"] = 3;
			rev.SetProperties(props);
			validationCalled = false;
			rev = database.PutRevision(rev, rev.GetRevId(), false, status);
			NUnit.Framework.Assert.IsTrue(validationCalled);
            NUnit.Framework.Assert.AreEqual(StatusCode.Created, status.GetCode());
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
                gotExpectedError = (e.GetCBLStatus().GetCode() == StatusCode.Forbidden);
			}
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			// POST an invalid new document:
			props = new Dictionary<string, object>();
			props["name"] = "Vogon";
			props["poetry"] = true;
			rev = new RevisionInternal(props, database);
			validationCalled = false;
			gotExpectedError = false;
			try
			{
				rev = database.PutRevision(rev, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
                gotExpectedError = (e.GetCBLStatus().GetCode() == StatusCode.Forbidden);
			}
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
			// PUT a valid new document with an ID:
			props = new Dictionary<string, object>();
			props["_id"] = "ford";
			props["name"] = "Ford Prefect";
			props["towel"] = "terrycloth";
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
			props["_id"] = "petunias";
			props["name"] = "Pot of Petunias";
			rev = new RevisionInternal(props, database);
			validationCalled = false;
			gotExpectedError = false;
			try
			{
				rev = database.PutRevision(rev, null, false, status);
			}
			catch (CouchbaseLiteException e)
			{
                gotExpectedError = (e.GetCBLStatus().GetCode() == StatusCode.Forbidden);
			}
			NUnit.Framework.Assert.IsTrue(validationCalled);
			NUnit.Framework.Assert.IsTrue(gotExpectedError);
		}
	}
}
