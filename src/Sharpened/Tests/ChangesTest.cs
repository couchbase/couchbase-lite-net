//
// ChangesTest.cs
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
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class ChangesTest : LiteTestCase
	{
		private int changeNotifications = 0;

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestChangeNotification()
		{
			Database.ChangeListener changeListener = new _ChangeListener_16(this);
			// add listener to database
			database.AddChangeListener(changeListener);
			// create a document
			IDictionary<string, object> documentProperties = new Dictionary<string, object>();
			documentProperties.Put("foo", 1);
			documentProperties.Put("bar", false);
			documentProperties.Put("baz", "touch");
			Body body = new Body(documentProperties);
			RevisionInternal rev1 = new RevisionInternal(body, database);
			Status status = new Status();
			rev1 = database.PutRevision(rev1, null, false, status);
			NUnit.Framework.Assert.AreEqual(1, changeNotifications);
		}

		private sealed class _ChangeListener_16 : Database.ChangeListener
		{
			public _ChangeListener_16(ChangesTest _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				this._enclosing.changeNotifications++;
			}

			private readonly ChangesTest _enclosing;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestLocalChangesAreNotExternal()
		{
			changeNotifications = 0;
			Database.ChangeListener changeListener = new _ChangeListener_43(this);
			database.AddChangeListener(changeListener);
			// Insert a document locally.
			Document document = database.CreateDocument();
			document.CreateRevision().Save();
			// Make sure that the assertion in changeListener was called.
			NUnit.Framework.Assert.AreEqual(1, changeNotifications);
		}

		private sealed class _ChangeListener_43 : Database.ChangeListener
		{
			public _ChangeListener_43(ChangesTest _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				this._enclosing.changeNotifications++;
				NUnit.Framework.Assert.IsFalse(@event.IsExternal());
			}

			private readonly ChangesTest _enclosing;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual void TestPulledChangesAreExternal()
		{
			changeNotifications = 0;
			Database.ChangeListener changeListener = new _ChangeListener_61(this);
			database.AddChangeListener(changeListener);
			// Insert a document as if it came from a remote source.
			RevisionInternal rev = new RevisionInternal("docId", "1-rev", false, database);
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("_id", rev.GetDocId());
			properties.Put("_rev", rev.GetRevId());
			rev.SetProperties(properties);
			database.ForceInsert(rev, Arrays.AsList(rev.GetRevId()), GetReplicationURL());
			// Make sure that the assertion in changeListener was called.
			NUnit.Framework.Assert.AreEqual(1, changeNotifications);
		}

		private sealed class _ChangeListener_61 : Database.ChangeListener
		{
			public _ChangeListener_61(ChangesTest _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Changed(Database.ChangeEvent @event)
			{
				this._enclosing.changeNotifications++;
				NUnit.Framework.Assert.IsTrue(@event.IsExternal());
			}

			private readonly ChangesTest _enclosing;
		}
	}
}
