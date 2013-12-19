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
    internal class RevisionInternal
	{
		private string docId;

		private string revId;

		private bool deleted;

		private Body body;

		private long sequence;

		private Database database;

        public RevisionInternal(String docId, String revId, Boolean deleted, Database database)
		{
			// TODO: get rid of this field!
			this.docId = docId;
			this.revId = revId;
			this.deleted = deleted;
			this.database = database;
		}

		public RevisionInternal(Body body, Database database)
            : this((string)body.GetPropertyForKey("_id"), (string)body.GetPropertyForKey("_rev"), (body.HasValueForKey("_deleted") && (bool)body.GetPropertyForKey("_deleted")), database)
		{
			this.body = body;
		}

        public RevisionInternal(IDictionary<String, Object> properties, Database database)
            : this(new Body(properties), database) { }

        public IDictionary<String, Object> GetProperties()
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

		public object GetPropertyForKey(string key)
		{
			IDictionary<string, object> prop = GetProperties();
			if (prop == null)
			{
				return null;
			}
			return GetProperties().Get(key);
		}

		public void SetProperties(IDictionary<string, object> properties)
		{
			body = new Body(properties);
		}

		public IEnumerable<Byte> GetJson()
		{
			IEnumerable<Byte> result = null;
			if (body != null)
			{
				result = body.GetJson();
			}
			return result;
		}

		public void SetJson(IEnumerable<Byte> json)
		{
			body = new Body(json);
		}

		public override bool Equals(object o)
		{
			bool result = false;
			if (o is RevisionInternal)
			{
				RevisionInternal other = (RevisionInternal)o;
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

		public string GetDocId()
		{
			return docId;
		}

		public void SetDocId(string docId)
		{
			this.docId = docId;
		}

		public string GetRevId()
		{
			return revId;
		}

		public void SetRevId(string revId)
		{
			this.revId = revId;
		}

		public bool IsDeleted()
		{
			return deleted;
		}

		public void SetDeleted(bool deleted)
		{
			this.deleted = deleted;
		}

		public Body GetBody()
		{
			return body;
		}

		public void SetBody(Body body)
		{
			this.body = body;
		}

        public RevisionInternal CopyWithDocID(String docId, String revId)
		{
			System.Diagnostics.Debug.Assert(((docId != null) && (revId != null)));
			System.Diagnostics.Debug.Assert(((this.docId == null) || (this.docId.Equals(docId
				))));
            RevisionInternal result = new RevisionInternal(docId, revId, deleted, database);
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

		public void SetSequence(long sequence)
		{
			this.sequence = sequence;
		}

		public long GetSequence()
		{
			return sequence;
		}

		public override string ToString()
		{
			return "{" + this.docId + " #" + this.revId + (deleted ? "DEL" : string.Empty) + "}";
		}

		/// <summary>Generation number: 1 for a new document, 2 for the 2nd revision, ...</summary>
		/// <remarks>
		/// Generation number: 1 for a new document, 2 for the 2nd revision, ...
		/// Extracted from the numeric prefix of the revID.
		/// </remarks>
		public int GetGeneration()
		{
			return GenerationFromRevID(revId);
		}

		public static int GenerationFromRevID(string revID)
		{
			int generation = 0;
            int dashPos = revID.IndexOf("-", StringComparison.InvariantCultureIgnoreCase);
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
            var st1 = new StringTokenizer(revId1, "-");
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
