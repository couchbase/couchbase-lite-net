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

using Sharpen;

namespace Couchbase.Lite
{
	public class ManagerOptions
	{
		/// <summary>No modifications to databases are allowed.</summary>
		/// <remarks>No modifications to databases are allowed.</remarks>
		private bool readOnly;

		/// <summary>Persistent replications will not run (until/unless startPersistentReplications is called.)
		/// 	</summary>
		private bool noReplicator;

		public ManagerOptions(bool readOnly, bool noReplicator)
		{
			this.readOnly = readOnly;
			this.noReplicator = noReplicator;
		}

		public virtual bool IsReadOnly()
		{
			return readOnly;
		}

		public virtual void SetReadOnly(bool readOnly)
		{
			this.readOnly = readOnly;
		}

		public virtual bool IsNoReplicator()
		{
			return noReplicator;
		}

		public virtual void SetNoReplicator(bool noReplicator)
		{
			this.noReplicator = noReplicator;
		}
	}
}
