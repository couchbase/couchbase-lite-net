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
using System.Collections;
using System.Collections.Generic;
using Couchbase;
using Couchbase.Auth;
using Couchbase.Replicator;
using Sharpen;

namespace Couchbase
{
	public class CBLManager
	{
		private CBLServer server;

		//INSTANCE;
		public virtual CBLServer GetServer()
		{
			return server;
		}

		public virtual void SetServer(CBLServer server)
		{
			this.server = server;
		}

		private IDictionary<string, object> ParseSourceOrTarget(IDictionary<string, object
			> properties, string key)
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			object value = properties.Get(key);
			if (value is string)
			{
				result.Put("url", (string)value);
			}
			else
			{
				if (value is IDictionary)
				{
					result = (IDictionary<string, object>)value;
				}
			}
			return result;
		}

		/// <exception cref="Couchbase.CBLiteException"></exception>
		public virtual CBLReplicator GetReplicator(IDictionary<string, object> properties
			)
		{
			CBLAuthorizer authorizer = null;
			CBLReplicator repl = null;
			Uri remote = null;
			IDictionary<string, object> remoteMap;
			IDictionary<string, object> sourceMap = ParseSourceOrTarget(properties, "source");
			IDictionary<string, object> targetMap = ParseSourceOrTarget(properties, "target");
			string source = (string)sourceMap.Get("url");
			string target = (string)targetMap.Get("url");
			bool createTargetBoolean = (bool)properties.Get("create_target");
			bool createTarget = (createTargetBoolean != null && createTargetBoolean);
			bool continuousBoolean = (bool)properties.Get("continuous");
			bool continuous = (continuousBoolean != null && continuousBoolean);
			bool cancelBoolean = (bool)properties.Get("cancel");
			bool cancel = (cancelBoolean != null && cancelBoolean);
			// Map the 'source' and 'target' JSON params to a local database and remote URL:
			if (source == null || target == null)
			{
				throw new CBLiteException("source and target are both null", new CBLStatus(CBLStatus
					.BadRequest));
			}
			bool push = false;
			CBLDatabase db = GetServer().GetExistingDatabaseNamed(source);
			string remoteStr = null;
			if (db != null)
			{
				remoteStr = target;
				push = true;
				remoteMap = targetMap;
			}
			else
			{
				remoteStr = source;
				if (createTarget && !cancel)
				{
					db = GetServer().GetDatabaseNamed(target);
					if (!db.Open())
					{
						throw new CBLiteException("cannot open database: " + db, new CBLStatus(CBLStatus.
							InternalServerError));
					}
				}
				else
				{
					db = GetServer().GetExistingDatabaseNamed(target);
				}
				if (db == null)
				{
					throw new CBLiteException("database is null", new CBLStatus(CBLStatus.NotFound));
				}
				remoteMap = sourceMap;
			}
			IDictionary<string, object> authMap = (IDictionary<string, object>)remoteMap.Get(
				"auth");
			if (authMap != null)
			{
				IDictionary<string, object> persona = (IDictionary<string, object>)authMap.Get("persona"
					);
				if (persona != null)
				{
					string email = (string)persona.Get("email");
					authorizer = new CBLPersonaAuthorizer(email);
				}
				IDictionary<string, object> facebook = (IDictionary<string, object>)authMap.Get("facebook"
					);
				if (facebook != null)
				{
					string email = (string)facebook.Get("email");
					authorizer = new CBLFacebookAuthorizer(email);
				}
			}
			try
			{
				remote = new Uri(remoteStr);
			}
			catch (UriFormatException)
			{
				throw new CBLiteException("malformed remote url: " + remoteStr, new CBLStatus(CBLStatus
					.BadRequest));
			}
			if (remote == null || !remote.Scheme.StartsWith("http"))
			{
				throw new CBLiteException("remote URL is null or non-http: " + remoteStr, new CBLStatus
					(CBLStatus.BadRequest));
			}
			if (!cancel)
			{
				repl = db.GetReplicator(remote, GetServer().GetDefaultHttpClientFactory(), push, 
					continuous, GetServer().GetWorkExecutor());
				if (repl == null)
				{
					throw new CBLiteException("unable to create replicator with remote: " + remote, new 
						CBLStatus(CBLStatus.InternalServerError));
				}
				if (authorizer != null)
				{
					repl.SetAuthorizer(authorizer);
				}
				string filterName = (string)properties.Get("filter");
				if (filterName != null)
				{
					repl.SetFilterName(filterName);
					IDictionary<string, object> filterParams = (IDictionary<string, object>)properties
						.Get("query_params");
					if (filterParams != null)
					{
						repl.SetFilterParams(filterParams);
					}
				}
				if (push)
				{
					((CBLPusher)repl).SetCreateTarget(createTarget);
				}
			}
			else
			{
				// Cancel replication:
				repl = db.GetActiveReplicator(remote, push);
				if (repl == null)
				{
					throw new CBLiteException("unable to lookup replicator with remote: " + remote, new 
						CBLStatus(CBLStatus.NotFound));
				}
			}
			return repl;
		}
	}
}
