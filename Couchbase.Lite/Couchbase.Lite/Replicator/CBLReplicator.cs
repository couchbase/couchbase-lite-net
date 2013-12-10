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
using Couchbase;
using Couchbase.Auth;
using Couchbase.Support;
using Couchbase.Util;
using Org.Apache.Http.Client;
using Org.Apache.Http.Entity.Mime;
using Sharpen;

namespace Couchbase.Replicator
{
	public abstract class CBLReplicator : Observable
	{
		private static int lastSessionID = 0;

		protected internal ScheduledExecutorService workExecutor;

		protected internal CBLDatabase db;

		protected internal Uri remote;

		protected internal bool continuous;

		protected internal string lastSequence;

		protected internal bool lastSequenceChanged;

		protected internal IDictionary<string, object> remoteCheckpoint;

		protected internal bool savingCheckpoint;

		protected internal bool overdueForSave;

		protected internal bool running;

		protected internal bool active;

		protected internal Exception error;

		protected internal string sessionID;

		protected internal CBLBatcher<CBLRevision> batcher;

		protected internal int asyncTaskCount;

		private int changesProcessed;

		private int changesTotal;

		protected internal readonly HttpClientFactory clientFactory;

		protected internal string filterName;

		protected internal IDictionary<string, object> filterParams;

		protected internal ExecutorService remoteRequestExecutor;

		protected internal CBLAuthorizer authorizer;

		protected internal const int ProcessorDelay = 500;

		protected internal const int InboxCapacity = 100;

		public CBLReplicator(CBLDatabase db, Uri remote, bool continuous, ScheduledExecutorService
			 workExecutor) : this(db, remote, continuous, null, workExecutor)
		{
		}

		public CBLReplicator(CBLDatabase db, Uri remote, bool continuous, HttpClientFactory
			 clientFactory, ScheduledExecutorService workExecutor)
		{
			this.db = db;
			this.continuous = continuous;
			this.workExecutor = workExecutor;
			this.remote = remote;
			this.remoteRequestExecutor = Executors.NewCachedThreadPool();
			if (remote.GetQuery() != null && !remote.GetQuery().IsEmpty())
			{
				URI uri = URI.Create(remote.ToExternalForm());
				string personaAssertion = URIUtils.GetQueryParameter(uri, CBLPersonaAuthorizer.QueryParameter
					);
				if (personaAssertion != null && !personaAssertion.IsEmpty())
				{
					string email = CBLPersonaAuthorizer.RegisterAssertion(personaAssertion);
					CBLPersonaAuthorizer authorizer = new CBLPersonaAuthorizer(email);
					SetAuthorizer(authorizer);
				}
				string facebookAccessToken = URIUtils.GetQueryParameter(uri, CBLFacebookAuthorizer
					.QueryParameter);
				if (facebookAccessToken != null && !facebookAccessToken.IsEmpty())
				{
					string email = URIUtils.GetQueryParameter(uri, CBLFacebookAuthorizer.QueryParameterEmail
						);
					CBLFacebookAuthorizer authorizer = new CBLFacebookAuthorizer(email);
					Uri remoteWithQueryRemoved = null;
					try
					{
						remoteWithQueryRemoved = new Uri(remote.Scheme, remote.GetHost(), remote.Port, remote
							.AbsolutePath);
					}
					catch (UriFormatException e)
					{
						throw new ArgumentException(e);
					}
					CBLFacebookAuthorizer.RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved
						.ToExternalForm());
					SetAuthorizer(authorizer);
				}
				// we need to remove the query from the URL, since it will cause problems when
				// communicating with sync gw / couchdb
				try
				{
					this.remote = new Uri(remote.Scheme, remote.GetHost(), remote.Port, remote.AbsolutePath
						);
				}
				catch (UriFormatException e)
				{
					throw new ArgumentException(e);
				}
			}
			batcher = new CBLBatcher<CBLRevision>(workExecutor, InboxCapacity, ProcessorDelay
				, new _CBLBatchProcessor_113(this));
			this.clientFactory = clientFactory != null ? clientFactory : new CBLHttpClientFactory
				();
		}

		private sealed class _CBLBatchProcessor_113 : CBLBatchProcessor<CBLRevision>
		{
			public _CBLBatchProcessor_113(CBLReplicator _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Process(IList<CBLRevision> inbox)
			{
				Log.V(CBLDatabase.Tag, "*** " + this.ToString() + ": BEGIN processInbox (" + inbox
					.Count + " sequences)");
				this._enclosing.ProcessInbox(new CBLRevisionList(inbox));
				Log.V(CBLDatabase.Tag, "*** " + this.ToString() + ": END processInbox (lastSequence="
					 + this._enclosing.lastSequence);
				this._enclosing.active = false;
			}

			private readonly CBLReplicator _enclosing;
		}

		public virtual string GetFilterName()
		{
			return filterName;
		}

		public virtual void SetFilterName(string filterName)
		{
			this.filterName = filterName;
		}

		public virtual IDictionary<string, object> GetFilterParams()
		{
			return filterParams;
		}

		public virtual void SetFilterParams(IDictionary<string, object> filterParams)
		{
			this.filterParams = filterParams;
		}

		public virtual bool IsContinuous()
		{
			return continuous;
		}

		public virtual void SetContinuous(bool continuous)
		{
			if (!IsRunning())
			{
				this.continuous = continuous;
			}
		}

		public virtual void SetAuthorizer(CBLAuthorizer authorizer)
		{
			this.authorizer = authorizer;
		}

		public virtual CBLAuthorizer GetAuthorizer()
		{
			return authorizer;
		}

		public virtual bool IsRunning()
		{
			return running;
		}

		public virtual Uri GetRemote()
		{
			return remote;
		}

		public virtual void DatabaseClosing()
		{
			SaveLastSequence();
			Stop();
			db = null;
		}

		public override string ToString()
		{
			string maskedRemoteWithoutCredentials = (remote != null ? remote.ToExternalForm()
				 : string.Empty);
			maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
				, "://---:---@");
			string name = GetType().Name + "[" + maskedRemoteWithoutCredentials + "]";
			return name;
		}

		public virtual bool IsPush()
		{
			return false;
		}

		public virtual string GetLastSequence()
		{
			return lastSequence;
		}

		public virtual void SetLastSequence(string lastSequenceIn)
		{
			if (lastSequenceIn != null && !lastSequenceIn.Equals(lastSequence))
			{
				Log.V(CBLDatabase.Tag, ToString() + ": Setting lastSequence to " + lastSequenceIn
					 + " from( " + lastSequence + ")");
				lastSequence = lastSequenceIn;
				if (!lastSequenceChanged)
				{
					lastSequenceChanged = true;
					workExecutor.Schedule(new _Runnable_197(this), 2 * 1000, TimeUnit.Milliseconds);
				}
			}
		}

		private sealed class _Runnable_197 : Runnable
		{
			public _Runnable_197(CBLReplicator _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Run()
			{
				this._enclosing.SaveLastSequence();
			}

			private readonly CBLReplicator _enclosing;
		}

		public virtual int GetChangesProcessed()
		{
			return changesProcessed;
		}

		public virtual void SetChangesProcessed(int processed)
		{
			this.changesProcessed = processed;
			SetChanged();
			NotifyObservers();
		}

		public virtual int GetChangesTotal()
		{
			return changesTotal;
		}

		public virtual void SetChangesTotal(int total)
		{
			this.changesTotal = total;
			SetChanged();
			NotifyObservers();
		}

		public virtual string GetSessionID()
		{
			return sessionID;
		}

		public virtual void Start()
		{
			if (running)
			{
				return;
			}
			this.sessionID = string.Format("repl%03d", ++lastSessionID);
			Log.V(CBLDatabase.Tag, ToString() + " STARTING ...");
			running = true;
			lastSequence = null;
			CheckSession();
		}

		protected internal virtual void CheckSession()
		{
			if (GetAuthorizer() != null && GetAuthorizer().UsesCookieBasedLogin())
			{
				CheckSessionAtPath("/_session");
			}
			else
			{
				FetchRemoteCheckpointDoc();
			}
		}

		protected internal virtual void CheckSessionAtPath(string sessionPath)
		{
			AsyncTaskStarted();
			SendAsyncRequest("GET", sessionPath, null, new _CBLRemoteRequestCompletionBlock_255
				(this, sessionPath));
		}

		private sealed class _CBLRemoteRequestCompletionBlock_255 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_255(CBLReplicator _enclosing, string sessionPath
				)
			{
				this._enclosing = _enclosing;
				this.sessionPath = sessionPath;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e is HttpResponseException && ((HttpResponseException)e).GetStatusCode() == 404
					 && Sharpen.Runtime.EqualsIgnoreCase(sessionPath, "/_session"))
				{
					this._enclosing.CheckSessionAtPath("_session");
					return;
				}
				else
				{
					IDictionary<string, object> response = (IDictionary<string, object>)result;
					IDictionary<string, object> userCtx = (IDictionary<string, object>)response.Get("userCtx"
						);
					string username = (string)userCtx.Get("name");
					if (username != null && username.Length > 0)
					{
						Log.D(CBLDatabase.Tag, string.Format("%s Active session, logged in as %s", this, 
							username));
						this._enclosing.FetchRemoteCheckpointDoc();
					}
					else
					{
						Log.D(CBLDatabase.Tag, string.Format("%s No active session, going to login", this
							));
						this._enclosing.Login();
					}
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly CBLReplicator _enclosing;

			private readonly string sessionPath;
		}

		public abstract void BeginReplicating();

		public virtual void Stop()
		{
			if (!running)
			{
				return;
			}
			Log.V(CBLDatabase.Tag, ToString() + " STOPPING...");
			batcher.Flush();
			continuous = false;
			if (asyncTaskCount == 0)
			{
				Stopped();
			}
		}

		public virtual void Stopped()
		{
			Log.V(CBLDatabase.Tag, ToString() + " STOPPED");
			running = false;
			this.changesProcessed = this.changesTotal = 0;
			SaveLastSequence();
			SetChanged();
			NotifyObservers();
			batcher = null;
			db = null;
		}

		protected internal virtual void Login()
		{
			IDictionary<string, string> loginParameters = GetAuthorizer().LoginParametersForSite
				(remote);
			if (loginParameters == null)
			{
				Log.D(CBLDatabase.Tag, string.Format("%s: %s has no login parameters, so skipping login"
					, this, GetAuthorizer()));
				FetchRemoteCheckpointDoc();
				return;
			}
			string loginPath = GetAuthorizer().LoginPathForSite(remote);
			Log.D(CBLDatabase.Tag, string.Format("%s: Doing login with %s at %s", this, GetAuthorizer
				().GetType(), loginPath));
			AsyncTaskStarted();
			SendAsyncRequest("POST", loginPath, loginParameters, new _CBLRemoteRequestCompletionBlock_325
				(this, loginPath));
		}

		private sealed class _CBLRemoteRequestCompletionBlock_325 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_325(CBLReplicator _enclosing, string loginPath
				)
			{
				this._enclosing = _enclosing;
				this.loginPath = loginPath;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e != null)
				{
					Log.D(CBLDatabase.Tag, string.Format("%s: Login failed for path: %s", this, loginPath
						));
					this._enclosing.error = e;
				}
				else
				{
					Log.D(CBLDatabase.Tag, string.Format("%s: Successfully logged in!", this));
					this._enclosing.FetchRemoteCheckpointDoc();
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly CBLReplicator _enclosing;

			private readonly string loginPath;
		}

		public virtual void AsyncTaskStarted()
		{
			lock (this)
			{
				++asyncTaskCount;
			}
		}

		public virtual void AsyncTaskFinished(int numTasks)
		{
			lock (this)
			{
				this.asyncTaskCount -= numTasks;
				if (asyncTaskCount == 0)
				{
					if (!continuous)
					{
						Stopped();
					}
				}
			}
		}

		public virtual void AddToInbox(CBLRevision rev)
		{
			if (batcher.Count() == 0)
			{
				active = true;
			}
			batcher.QueueObject(rev);
		}

		public virtual void ProcessInbox(CBLRevisionList inbox)
		{
		}

		public virtual void SendAsyncRequest(string method, string relativePath, object body
			, CBLRemoteRequestCompletionBlock onCompletion)
		{
			try
			{
				string urlStr = BuildRelativeURLString(relativePath);
				Uri url = new Uri(urlStr);
				SendAsyncRequest(method, url, body, onCompletion);
			}
			catch (UriFormatException e)
			{
				Log.E(CBLDatabase.Tag, "Malformed URL for async request", e);
			}
		}

		internal virtual string BuildRelativeURLString(string relativePath)
		{
			// the following code is a band-aid for a system problem in the codebase
			// where it is appending "relative paths" that start with a slash, eg:
			//     http://dotcom/db/ + /relpart == http://dotcom/db/relpart
			// which is not compatible with the way the java url concatonation works.
			string remoteUrlString = remote.ToExternalForm();
			if (remoteUrlString.EndsWith("/") && relativePath.StartsWith("/"))
			{
				remoteUrlString = Sharpen.Runtime.Substring(remoteUrlString, 0, remoteUrlString.Length
					 - 1);
			}
			return remoteUrlString + relativePath;
		}

		public virtual void SendAsyncRequest(string method, Uri url, object body, CBLRemoteRequestCompletionBlock
			 onCompletion)
		{
			CBLRemoteRequest request = new CBLRemoteRequest(workExecutor, clientFactory, method
				, url, body, onCompletion);
			remoteRequestExecutor.Execute(request);
		}

		public virtual void SendAsyncMultipartDownloaderRequest(string method, string relativePath
			, object body, CBLDatabase db, CBLRemoteRequestCompletionBlock onCompletion)
		{
			try
			{
				string urlStr = BuildRelativeURLString(relativePath);
				Uri url = new Uri(urlStr);
				CBLRemoteMultipartDownloaderRequest request = new CBLRemoteMultipartDownloaderRequest
					(workExecutor, clientFactory, method, url, body, db, onCompletion);
				remoteRequestExecutor.Execute(request);
			}
			catch (UriFormatException e)
			{
				Log.E(CBLDatabase.Tag, "Malformed URL for async request", e);
			}
		}

		public virtual void SendAsyncMultipartRequest(string method, string relativePath, 
			MultipartEntity multiPartEntity, CBLRemoteRequestCompletionBlock onCompletion)
		{
			Uri url = null;
			try
			{
				string urlStr = BuildRelativeURLString(relativePath);
				url = new Uri(urlStr);
			}
			catch (UriFormatException e)
			{
				throw new ArgumentException(e);
			}
			CBLRemoteMultipartRequest request = new CBLRemoteMultipartRequest(workExecutor, clientFactory
				, method, url, multiPartEntity, onCompletion);
			remoteRequestExecutor.Execute(request);
		}

		/// <summary>CHECKPOINT STORAGE:</summary>
		public virtual void MaybeCreateRemoteDB()
		{
		}

		// CBLPusher overrides this to implement the .createTarget option
		/// <summary>This is the _local document ID stored on the remote server to keep track of state.
		/// 	</summary>
		/// <remarks>
		/// This is the _local document ID stored on the remote server to keep track of state.
		/// Its ID is based on the local database ID (the private one, to make the result unguessable)
		/// and the remote database's URL.
		/// </remarks>
		public virtual string RemoteCheckpointDocID()
		{
			if (db == null)
			{
				return null;
			}
			string input = db.PrivateUUID() + "\n" + remote.ToExternalForm() + "\n" + (IsPush
				() ? "1" : "0");
			return CBLMisc.TDHexSHA1Digest(Sharpen.Runtime.GetBytesForString(input));
		}

		private bool Is404(Exception e)
		{
			if (e is HttpResponseException)
			{
				return ((HttpResponseException)e).GetStatusCode() == 404;
			}
			return false;
		}

		public virtual void FetchRemoteCheckpointDoc()
		{
			lastSequenceChanged = false;
			string localLastSequence = db.LastSequenceWithRemoteURL(remote, IsPush());
			if (localLastSequence == null)
			{
				MaybeCreateRemoteDB();
				BeginReplicating();
				return;
			}
			AsyncTaskStarted();
			SendAsyncRequest("GET", "/_local/" + RemoteCheckpointDocID(), null, new _CBLRemoteRequestCompletionBlock_460
				(this, localLastSequence));
		}

		private sealed class _CBLRemoteRequestCompletionBlock_460 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_460(CBLReplicator _enclosing, string localLastSequence
				)
			{
				this._enclosing = _enclosing;
				this.localLastSequence = localLastSequence;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e != null && !this._enclosing.Is404(e))
				{
					Log.D(CBLDatabase.Tag, this + " error getting remote checkpoint: " + e);
					this._enclosing.error = e;
				}
				else
				{
					if (e != null && this._enclosing.Is404(e))
					{
						Log.D(CBLDatabase.Tag, this + " 404 error getting remote checkpoint " + this._enclosing
							.RemoteCheckpointDocID() + ", calling maybeCreateRemoteDB");
						this._enclosing.MaybeCreateRemoteDB();
					}
					IDictionary<string, object> response = (IDictionary<string, object>)result;
					this._enclosing.remoteCheckpoint = response;
					string remoteLastSequence = null;
					if (response != null)
					{
						remoteLastSequence = (string)response.Get("lastSequence");
					}
					if (remoteLastSequence != null && remoteLastSequence.Equals(localLastSequence))
					{
						this._enclosing.lastSequence = localLastSequence;
						Log.V(CBLDatabase.Tag, this + ": Replicating from lastSequence=" + this._enclosing
							.lastSequence);
					}
					else
					{
						Log.V(CBLDatabase.Tag, this + ": lastSequence mismatch: I had " + localLastSequence
							 + ", remote had " + remoteLastSequence);
					}
					this._enclosing.BeginReplicating();
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly CBLReplicator _enclosing;

			private readonly string localLastSequence;
		}

		public virtual void SaveLastSequence()
		{
			if (!lastSequenceChanged)
			{
				return;
			}
			if (savingCheckpoint)
			{
				// If a save is already in progress, don't do anything. (The completion block will trigger
				// another save after the first one finishes.)
				overdueForSave = true;
				return;
			}
			lastSequenceChanged = false;
			overdueForSave = false;
			Log.V(CBLDatabase.Tag, this + " checkpointing sequence=" + lastSequence);
			IDictionary<string, object> body = new Dictionary<string, object>();
			if (remoteCheckpoint != null)
			{
				body.PutAll(remoteCheckpoint);
			}
			body.Put("lastSequence", lastSequence);
			string remoteCheckpointDocID = RemoteCheckpointDocID();
			if (remoteCheckpointDocID == null)
			{
				return;
			}
			savingCheckpoint = true;
			SendAsyncRequest("PUT", "/_local/" + remoteCheckpointDocID, body, new _CBLRemoteRequestCompletionBlock_518
				(this, body));
			// TODO: If error is 401 or 403, and this is a pull, remember that remote is read-only and don't attempt to read its checkpoint next time.
			db.SetLastSequence(lastSequence, remote, IsPush());
		}

		private sealed class _CBLRemoteRequestCompletionBlock_518 : CBLRemoteRequestCompletionBlock
		{
			public _CBLRemoteRequestCompletionBlock_518(CBLReplicator _enclosing, IDictionary
				<string, object> body)
			{
				this._enclosing = _enclosing;
				this.body = body;
			}

			public void OnCompletion(object result, Exception e)
			{
				this._enclosing.savingCheckpoint = false;
				if (e != null)
				{
					Log.V(CBLDatabase.Tag, this + ": Unable to save remote checkpoint", e);
				}
				else
				{
					IDictionary<string, object> response = (IDictionary<string, object>)result;
					body.Put("_rev", response.Get("rev"));
					this._enclosing.remoteCheckpoint = body;
				}
				if (this._enclosing.overdueForSave)
				{
					this._enclosing.SaveLastSequence();
				}
			}

			private readonly CBLReplicator _enclosing;

			private readonly IDictionary<string, object> body;
		}

		public virtual Exception GetError()
		{
			return error;
		}
	}
}
