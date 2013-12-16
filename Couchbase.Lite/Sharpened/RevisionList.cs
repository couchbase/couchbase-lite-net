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

using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>An ordered list of TDRevisions</summary>
	[System.Serializable]
	public class RevisionList : AList<RevisionInternal>
	{
		public RevisionList() : base()
		{
		}

		/// <summary>Allow converting to RevisionList from List<RevisionInternal></summary>
		/// <param name="list"></param>
		public RevisionList(IList<RevisionInternal> list) : base(list)
		{
		}

		public virtual RevisionInternal RevWithDocIdAndRevId(string docId, string revId)
		{
			IEnumerator<RevisionInternal> iterator = GetEnumerator();
			while (iterator.HasNext())
			{
				RevisionInternal rev = iterator.Next();
				if (docId.Equals(rev.GetDocId()) && revId.Equals(rev.GetRevId()))
				{
					return rev;
				}
			}
			return null;
		}

		public virtual IList<string> GetAllDocIds()
		{
			IList<string> result = new AList<string>();
			IEnumerator<RevisionInternal> iterator = GetEnumerator();
			while (iterator.HasNext())
			{
				RevisionInternal rev = iterator.Next();
				result.AddItem(rev.GetDocId());
			}
			return result;
		}

		public virtual IList<string> GetAllRevIds()
		{
			IList<string> result = new AList<string>();
			IEnumerator<RevisionInternal> iterator = GetEnumerator();
			while (iterator.HasNext())
			{
				RevisionInternal rev = iterator.Next();
				result.AddItem(rev.GetRevId());
			}
			return result;
		}

		public virtual void SortBySequence()
		{
			this.Sort(new _IComparer_82());
		}

		private sealed class _IComparer_82 : IComparer<RevisionInternal>
		{
			public _IComparer_82()
			{
			}

			public int Compare(RevisionInternal rev1, RevisionInternal rev2)
			{
				return Misc.TDSequenceCompare(rev1.GetSequence(), rev2.GetSequence());
			}
		}

		public virtual void Limit(int limit)
		{
			if (Count > limit)
			{
				RemoveRange(limit, Count);
			}
		}
	}
}
