//
// RevisionList.cs
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
	/// <summary>An ordered list of TDRevisions</summary>
	[System.Serializable]
    internal class RevisionList : AList<RevisionInternal>
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
            while (iterator.MoveNext())
			{
                var rev = iterator.Current;
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
			while (iterator.MoveNext())
			{
                RevisionInternal rev = iterator.Current;
				result.AddItem(rev.GetDocId());
			}
			return result;
		}

		public virtual IList<string> GetAllRevIds()
		{
			IList<string> result = new AList<string>();
			IEnumerator<RevisionInternal> iterator = GetEnumerator();
			while (iterator.MoveNext())
			{
                RevisionInternal rev = iterator.Current;
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
