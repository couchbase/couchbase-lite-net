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
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using System.Web;

namespace Couchbase.Lite.Replicator
{

	/// <summary>A revision received from a remote server during a pull.</summary>
	/// <remarks>A revision received from a remote server during a pull. Tracks the opaque remote sequence ID.
	/// 	</remarks>
	internal class PulledRevision : RevisionInternal
	{
		public PulledRevision(Body body, Database database) : base(body, database)
		{
		}

		public PulledRevision(string docId, string revId, bool deleted, Database database
			) : base(docId, revId, deleted, database)
		{
		}

		public PulledRevision(IDictionary<string, object> properties, Database database) : 
			base(properties, database)
		{
		}

		protected internal string remoteSequenceID;

		public string GetRemoteSequenceID()
		{
			return remoteSequenceID;
		}

		public void SetRemoteSequenceID(string remoteSequenceID)
		{
			this.remoteSequenceID = remoteSequenceID;
		}
	}
}
