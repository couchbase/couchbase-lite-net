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

using System;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	public class DocumentChange
	{
		[InterfaceAudience.Private]
		internal DocumentChange(RevisionInternal addedRevision, RevisionInternal winningRevision
			, bool isConflict, Uri sourceUrl)
		{
			this.addedRevision = addedRevision;
			this.winningRevision = winningRevision;
			this.isConflict = isConflict;
			this.sourceUrl = sourceUrl;
		}

		private RevisionInternal addedRevision;

		private RevisionInternal winningRevision;

		private bool isConflict;

		private Uri sourceUrl;

		public virtual string GetDocumentId()
		{
			return addedRevision.GetDocId();
		}

		public virtual string GetRevisionId()
		{
			return addedRevision.GetRevId();
		}

		public virtual bool IsCurrentRevision()
		{
			return winningRevision != null && addedRevision.GetRevId().Equals(winningRevision
				.GetRevId());
		}

		public virtual bool IsConflict()
		{
			return isConflict;
		}

		public virtual Uri GetSourceUrl()
		{
			return sourceUrl;
		}

		[InterfaceAudience.Private]
		public virtual RevisionInternal GetAddedRevision()
		{
			return addedRevision;
		}

		[InterfaceAudience.Private]
		internal virtual RevisionInternal GetWinningRevision()
		{
			return winningRevision;
		}

		public static Couchbase.Lite.DocumentChange TempFactory(RevisionInternal revisionInternal
			, Uri sourceUrl, bool inConflict)
		{
			Couchbase.Lite.DocumentChange change = new Couchbase.Lite.DocumentChange(revisionInternal
				, null, inConflict, sourceUrl);
			// TODO: fix winning revision here
			return change;
		}
	}
}
