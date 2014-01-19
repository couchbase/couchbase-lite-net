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
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Standard query options for views.</summary>
    internal class QueryOptions
	{
		private object startKey = null;

		private object endKey = null;

		private IEnumerable<object> keys = null;

		private int skip = 0;

		private int limit = int.MaxValue;

		private int groupLevel = 0;

		private EnumSet<TDContentOptions> contentOptions = EnumSet.NoneOf<TDContentOptions
			>();

		private bool descending = false;

		private bool includeDocs = false;

		private bool includeDeletedDocs = false;

		private bool updateSeq = false;

		private bool inclusiveEnd = true;

		private bool reduce = false;

		private bool reduceSpecified = false;

		private bool group = false;

		private IndexUpdateMode stale;

        private AllDocsMode allDocsMode;

		// only works with _all_docs, not regular views
		public virtual object GetStartKey()
		{
			return startKey;
		}

		public virtual void SetStartKey(object startKey)
		{
			this.startKey = startKey;
		}

		public virtual object GetEndKey()
		{
			return endKey;
		}

		public virtual void SetEndKey(object endKey)
		{
			this.endKey = endKey;
		}

		public virtual int GetSkip()
		{
			return skip;
		}

		public virtual void SetSkip(int skip)
		{
			this.skip = skip;
		}

		public virtual int GetLimit()
		{
			return limit;
		}

		public virtual void SetLimit(int limit)
		{
			this.limit = limit;
		}

		public virtual bool IsDescending()
		{
			return descending;
		}

		public virtual void SetDescending(bool descending)
		{
			this.descending = descending;
		}

		public virtual bool IsIncludeDocs()
		{
			return includeDocs;
		}

		public virtual void SetIncludeDocs(bool includeDocs)
		{
			this.includeDocs = includeDocs;
		}


        public virtual AllDocsMode GetAllDocsMode()
        {
            return allDocsMode;
        }

        public virtual void SetAllDocsMode(AllDocsMode allDocsMode)
        {
            this.allDocsMode = allDocsMode;
        }

		public virtual bool IsUpdateSeq()
		{
			return updateSeq;
		}

		public virtual void SetUpdateSeq(bool updateSeq)
		{
			this.updateSeq = updateSeq;
		}

		public virtual bool IsInclusiveEnd()
		{
			return inclusiveEnd;
		}

		public virtual void SetInclusiveEnd(bool inclusiveEnd)
		{
			this.inclusiveEnd = inclusiveEnd;
		}

		public virtual int GetGroupLevel()
		{
			return groupLevel;
		}

		public virtual void SetGroupLevel(int groupLevel)
		{
			this.groupLevel = groupLevel;
		}

		public virtual bool IsReduce()
		{
			return reduce;
		}

		public virtual void SetReduce(bool reduce)
		{
			this.reduce = reduce;
		}

		public virtual bool IsGroup()
		{
			return group;
		}

		public virtual void SetGroup(bool group)
		{
			this.group = group;
		}

		public virtual EnumSet<TDContentOptions> GetContentOptions()
		{
			return contentOptions;
		}

		public virtual void SetContentOptions(EnumSet<TDContentOptions> contentOptions
			)
		{
			this.contentOptions = contentOptions;
		}

		public virtual IEnumerable<object> GetKeys()
		{
			return keys;
		}

		public virtual void SetKeys(IEnumerable<object> keys)
		{
			this.keys = keys;
		}

		public virtual IndexUpdateMode GetStale()
		{
			return stale;
		}

		public virtual bool IsIncludeDeletedDocs()
		{
			return includeDeletedDocs;
		}

		public virtual void SetIncludeDeletedDocs(bool includeDeletedDocs)
		{
			this.includeDeletedDocs = includeDeletedDocs;
		}

		public virtual void SetStale(IndexUpdateMode stale)
		{
			this.stale = stale;
		}

		public virtual bool IsReduceSpecified()
		{
			return reduceSpecified;
		}

		public virtual void SetReduceSpecified(bool reduceSpecified)
		{
			this.reduceSpecified = reduceSpecified;
		}
	}
}
