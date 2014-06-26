//
// ValidationContext.cs
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
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Context passed into a Validator.</summary>
	/// <remarks>Context passed into a Validator.</remarks>
	public interface ValidationContext
	{
		/// <summary>The contents of the current revision of the document, or nil if this is a new document.
		/// 	</summary>
		/// <remarks>The contents of the current revision of the document, or nil if this is a new document.
		/// 	</remarks>
		SavedRevision GetCurrentRevision();

		/// <summary>Gets the keys whose values have changed between the current and new Revisions
		/// 	</summary>
		IList<string> GetChangedKeys();

		/// <summary>Rejects the new Revision.</summary>
		/// <remarks>Rejects the new Revision.</remarks>
		void Reject();

		/// <summary>Rejects the new Revision.</summary>
		/// <remarks>Rejects the new Revision. The specified message will be included with the resulting error.
		/// 	</remarks>
		void Reject(string message);

		/// <summary>
		/// Calls the ChangeValidator for each key/value that has changed, passing both the old
		/// and new values.
		/// </summary>
		/// <remarks>
		/// Calls the ChangeValidator for each key/value that has changed, passing both the old
		/// and new values. If any delegate call returns false, the enumeration stops and false is
		/// returned, otherwise true is returned.
		/// </remarks>
		bool ValidateChanges(ChangeValidator changeValidator);
	}

	internal class ValidationContextImpl : ValidationContext
	{
		private Database database;

		private RevisionInternal currentRevision;

		private RevisionInternal newRev;

		private string rejectMessage;

		private IList<string> changedKeys;

		internal ValidationContextImpl(Database database, RevisionInternal currentRevision
			, RevisionInternal newRev)
		{
			this.database = database;
			this.currentRevision = currentRevision;
			this.newRev = newRev;
		}

		internal virtual RevisionInternal GetCurrentRevisionInternal()
		{
			if (currentRevision != null)
			{
				try
				{
					currentRevision = database.LoadRevisionBody(currentRevision, EnumSet.NoneOf<Database.TDContentOptions
						>());
				}
				catch (CouchbaseLiteException e)
				{
					throw new RuntimeException(e);
				}
			}
			return currentRevision;
		}

		public virtual SavedRevision GetCurrentRevision()
		{
			RevisionInternal cur = GetCurrentRevisionInternal();
			return cur != null ? new SavedRevision(database, cur) : null;
		}

		public virtual IList<string> GetChangedKeys()
		{
			if (changedKeys == null)
			{
				changedKeys = new AList<string>();
				IDictionary<string, object> cur = GetCurrentRevision().GetProperties();
				IDictionary<string, object> nuu = newRev.GetProperties();
				foreach (string key in cur.Keys)
				{
					if (!cur.Get(key).Equals(nuu.Get(key)) && !key.Equals("_rev"))
					{
						changedKeys.AddItem(key);
					}
				}
				foreach (string key_1 in nuu.Keys)
				{
					if (cur.Get(key_1) == null && !key_1.Equals("_rev") && !key_1.Equals("_id"))
					{
						changedKeys.AddItem(key_1);
					}
				}
			}
			return changedKeys;
		}

		public virtual void Reject()
		{
			if (rejectMessage == null)
			{
				rejectMessage = "invalid document";
			}
		}

		public virtual void Reject(string message)
		{
			if (rejectMessage == null)
			{
				rejectMessage = message;
			}
		}

		public virtual bool ValidateChanges(ChangeValidator changeValidator)
		{
			IDictionary<string, object> cur = GetCurrentRevision().GetProperties();
			IDictionary<string, object> nuu = newRev.GetProperties();
			foreach (string key in GetChangedKeys())
			{
				if (!changeValidator.ValidateChange(key, cur.Get(key), nuu.Get(key)))
				{
					Reject(string.Format("Illegal change to '%s' property", key));
					return false;
				}
			}
			return true;
		}

		internal virtual string GetRejectMessage()
		{
			return rejectMessage;
		}
	}
}
