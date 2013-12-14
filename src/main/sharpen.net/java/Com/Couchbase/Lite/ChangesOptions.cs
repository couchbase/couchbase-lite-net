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

using Com.Couchbase.Lite;
using Sharpen;

namespace Com.Couchbase.Lite
{
	/// <summary>Options for _changes feed</summary>
	public class ChangesOptions
	{
		private int limit = int.MaxValue;

		private EnumSet<Database.TDContentOptions> contentOptions = EnumSet.NoneOf<Database.TDContentOptions
			>();

		private bool includeDocs = false;

		private bool includeConflicts = false;

		private bool sortBySequence = true;

		public virtual int GetLimit()
		{
			return limit;
		}

		public virtual void SetLimit(int limit)
		{
			this.limit = limit;
		}

		public virtual bool IsIncludeConflicts()
		{
			return includeConflicts;
		}

		public virtual void SetIncludeConflicts(bool includeConflicts)
		{
			this.includeConflicts = includeConflicts;
		}

		public virtual bool IsIncludeDocs()
		{
			return includeDocs;
		}

		public virtual void SetIncludeDocs(bool includeDocs)
		{
			this.includeDocs = includeDocs;
		}

		public virtual bool IsSortBySequence()
		{
			return sortBySequence;
		}

		public virtual void SetSortBySequence(bool sortBySequence)
		{
			this.sortBySequence = sortBySequence;
		}

		public virtual EnumSet<Database.TDContentOptions> GetContentOptions()
		{
			return contentOptions;
		}

		public virtual void SetContentOptions(EnumSet<Database.TDContentOptions> contentOptions
			)
		{
			this.contentOptions = contentOptions;
		}
	}
}
