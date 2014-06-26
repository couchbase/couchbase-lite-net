//
// RevisionInternal.cs
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
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite.Internal
{
	/// <summary>Stores information about a revision -- its docID, revID, and whether it's deleted.
	/// 	</summary>
	/// <remarks>
	/// Stores information about a revision -- its docID, revID, and whether it's deleted.
	/// It can also store the sequence number and document contents (they can be added after creation).
	/// </remarks>
	public class RevisionInternal
	{
		private string docId;

		private string revId;

		private bool deleted;

		private bool missing;

		private Body body;

		private long sequence;

		private Database database;

		public RevisionInternal(string docId, string revId, bool deleted, Database database
			)
		{
			// TODO: get rid of this field!
			this.docId = docId;
			this.revId = revId;
			this.deleted = deleted;
			this.database = database;
		}

		public RevisionInternal(Body body, Database database) : this((string)body.GetPropertyForKey
			("_id"), (string)body.GetPropertyForKey("_rev"), (((bool)body.GetPropertyForKey(
			"_deleted") != null) && ((bool)body.GetPropertyForKey("_deleted") == true)), database
			)
		{
			this.body = body;
		}

		public RevisionInternal(IDictionary<string, object> properties, Database database
			) : this(new Body(properties), database)
		{
		}

		public virtual IDictionary<string, object> GetProperties()
		{
			IDictionary<string, object> result = null;
			if (body != null)
			{
				IDictionary<string, object> prop;
				try
				{
					prop = body.GetProperties();
				}
				catch (InvalidOperationException)
				{
					// handle when both object and json are null for this body
					return null;
				}
				if (result == null)
				{
					result = new Dictionary<string, object>();
				}
				result.PutAll(prop);
			}
			return result;
		}

		public virtual object GetPropertyForKey(string key)
		{
			IDictionary<string, object> prop = GetProperties();
			if (prop == null)
			{
				return null;
			}
			return prop.Get(key);
		}

		public virtual void SetProperties(IDictionary<string, object> properties)
		{
			this.body = new Body(properties);
		}

		public virtual byte[] GetJson()
		{
			byte[] result = null;
			if (body != null)
			{
				result = body.GetJson();
			}
			return result;
		}

		public virtual void SetJson(byte[] json)
		{
			this.body = new Body(json);
		}

		public override bool Equals(object o)
		{
			bool result = false;
			if (o is Couchbase.Lite.Internal.RevisionInternal)
			{
				Couchbase.Lite.Internal.RevisionInternal other = (Couchbase.Lite.Internal.RevisionInternal
					)o;
				if (docId.Equals(other.docId) && revId.Equals(other.revId))
				{
					result = true;
				}
			}
			return result;
		}

		public override int GetHashCode()
		{
			return docId.GetHashCode() ^ revId.GetHashCode();
		}

		public virtual string GetDocId()
		{
			return docId;
		}

		public virtual void SetDocId(string docId)
		{
			this.docId = docId;
		}

		public virtual string GetRevId()
		{
			return revId;
		}

		public virtual void SetRevId(string revId)
		{
			this.revId = revId;
		}

		public virtual bool IsDeleted()
		{
			return deleted;
		}

		public virtual void SetDeleted(bool deleted)
		{
			this.deleted = deleted;
		}

		public virtual Body GetBody()
		{
			return body;
		}

		public virtual void SetBody(Body body)
		{
			this.body = body;
		}

		public virtual bool IsMissing()
		{
			return missing;
		}

		public virtual void SetMissing(bool missing)
		{
			this.missing = missing;
		}

		public virtual Couchbase.Lite.Internal.RevisionInternal CopyWithDocID(string docId
			, string revId)
		{
			System.Diagnostics.Debug.Assert(((docId != null) && (revId != null)));
			System.Diagnostics.Debug.Assert(((this.docId == null) || (this.docId.Equals(docId
				))));
			Couchbase.Lite.Internal.RevisionInternal result = new Couchbase.Lite.Internal.RevisionInternal
				(docId, revId, deleted, database);
			IDictionary<string, object> unmodifiableProperties = GetProperties();
			IDictionary<string, object> properties = new Dictionary<string, object>();
			if (unmodifiableProperties != null)
			{
				properties.PutAll(unmodifiableProperties);
			}
			properties.Put("_id", docId);
			properties.Put("_rev", revId);
			result.SetProperties(properties);
			return result;
		}

		public virtual void SetSequence(long sequence)
		{
			this.sequence = sequence;
		}

		public virtual long GetSequence()
		{
			return sequence;
		}

		public override string ToString()
		{
			return "{" + this.docId + " #" + this.revId + (deleted ? "DEL" : string.Empty) + 
				"}";
		}

		/// <summary>Generation number: 1 for a new document, 2 for the 2nd revision, ...</summary>
		/// <remarks>
		/// Generation number: 1 for a new document, 2 for the 2nd revision, ...
		/// Extracted from the numeric prefix of the revID.
		/// </remarks>
		public virtual int GetGeneration()
		{
			return GenerationFromRevID(revId);
		}

		public static int GenerationFromRevID(string revID)
		{
			int generation = 0;
			int dashPos = revID.IndexOf("-");
			if (dashPos > 0)
			{
				generation = System.Convert.ToInt32(Sharpen.Runtime.Substring(revID, 0, dashPos));
			}
			return generation;
		}

		public static int CBLCollateRevIDs(string revId1, string revId2)
		{
			string rev1GenerationStr = null;
			string rev2GenerationStr = null;
			string rev1Hash = null;
			string rev2Hash = null;
			StringTokenizer st1 = new StringTokenizer(revId1, "-");
			try
			{
				rev1GenerationStr = st1.NextToken();
				rev1Hash = st1.NextToken();
			}
			catch (Exception)
			{
			}
			StringTokenizer st2 = new StringTokenizer(revId2, "-");
			try
			{
				rev2GenerationStr = st2.NextToken();
				rev2Hash = st2.NextToken();
			}
			catch (Exception)
			{
			}
			// improper rev IDs; just compare as plain text:
			if (rev1GenerationStr == null || rev2GenerationStr == null)
			{
				return revId1.CompareToIgnoreCase(revId2);
			}
			int rev1Generation;
			int rev2Generation;
			try
			{
				rev1Generation = System.Convert.ToInt32(rev1GenerationStr);
				rev2Generation = System.Convert.ToInt32(rev2GenerationStr);
			}
			catch (FormatException)
			{
				// improper rev IDs; just compare as plain text:
				return revId1.CompareToIgnoreCase(revId2);
			}
			// Compare generation numbers; if they match, compare suffixes:
			if (rev1Generation.CompareTo(rev2Generation) != 0)
			{
				return rev1Generation.CompareTo(rev2Generation);
			}
			else
			{
				if (rev1Hash != null && rev2Hash != null)
				{
					// compare suffixes if possible
					return Sharpen.Runtime.CompareOrdinal(rev1Hash, rev2Hash);
				}
				else
				{
					// just compare as plain text:
					return revId1.CompareToIgnoreCase(revId2);
				}
			}
		}

		public static int CBLCompareRevIDs(string revId1, string revId2)
		{
			System.Diagnostics.Debug.Assert((revId1 != null));
			System.Diagnostics.Debug.Assert((revId2 != null));
			return CBLCollateRevIDs(revId1, revId2);
		}
	}
}
