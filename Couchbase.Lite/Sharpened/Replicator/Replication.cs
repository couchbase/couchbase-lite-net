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
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Org.Apache.Http.Client;
using Org.Apache.Http.Entity.Mime;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
	public abstract class Replication
	{
		private static int lastSessionID = 0;

		protected internal bool continuous;

		protected internal string filterName;

		protected internal ScheduledExecutorService workExecutor;

		protected internal Database db;

		protected internal Uri remote;

		protected internal string lastSequence;

		protected internal bool lastSequenceChanged;

		protected internal IDictionary<string, object> remoteCheckpoint;

		protected internal bool savingCheckpoint;

		protected internal bool overdueForSave;

		protected internal bool running;

		protected internal bool active;

		protected internal Exception error;

		protected internal string sessionID;

		protected internal Batcher<RevisionInternal> batcher;

		protected internal int asyncTaskCount;

		private int completedChangesCount;

		private int changesCount;

		protected internal bool online;

		protected internal HttpClientFactory clientFactory;

		private IList<Replication.ChangeListener> changeListeners;

		protected internal IList<string> documentIDs;

		protected internal IDictionary<string, object> filterParams;

		protected internal ExecutorService remoteRequestExecutor;

		protected internal Authorizer authorizer;

		private Replication.ReplicationStatus status = Replication.ReplicationStatus.ReplicationStopped;

		protected internal IDictionary<string, object> requestHeaders;

		protected internal const int ProcessorDelay = 500;

		protected internal const int InboxCapacity = 100;

		public const string ByChannelFilterName = "sync_gateway/bychannel";

		public const string ChannelsQueryParam = "channels";

		public const string ReplicatorDatabaseName = "_replicator";

		/// <summary>Options for what metadata to include in document bodies</summary>
		public enum ReplicationStatus
		{
			ReplicationStopped,
			ReplicationOffline,
			ReplicationIdle,
			ReplicationActive
		}

		/// <summary>Private Constructor</summary>
		[InterfaceAudience.Private]
		public Replication(Database db, Uri remote, bool continuous, ScheduledExecutorService
			 workExecutor) : this(db, remote, continuous, null, workExecutor)
		{
		}

		/// <summary>Private Constructor</summary>
		[InterfaceAudience.Private]
		public Replication(Database db, Uri remote, bool continuous, HttpClientFactory clientFactory
			, ScheduledExecutorService workExecutor)
		{
			this.db = db;
			this.continuous = continuous;
			this.workExecutor = workExecutor;
			this.remote = remote;
			this.remoteRequestExecutor = Executors.NewCachedThreadPool();
			this.changeListeners = new AList<Replication.ChangeListener>();
			this.online = true;
			this.requestHeaders = new Dictionary<string, object>();
			if (remote.GetQuery() != null && !remote.GetQuery().IsEmpty())
			{
				URI uri = URI.Create(remote.ToExternalForm());
				string personaAssertion = URIUtils.GetQueryParameter(uri, PersonaAuthorizer.QueryParameter
					);
				if (personaAssertion != null && !personaAssertion.IsEmpty())
				{
					string email = PersonaAuthorizer.RegisterAssertion(personaAssertion);
					PersonaAuthorizer authorizer = new PersonaAuthorizer(email);
					SetAuthorizer(authorizer);
				}
				string facebookAccessToken = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameter
					);
				if (facebookAccessToken != null && !facebookAccessToken.IsEmpty())
				{
					string email = URIUtils.GetQueryParameter(uri, FacebookAuthorizer.QueryParameterEmail
						);
					FacebookAuthorizer authorizer = new FacebookAuthorizer(email);
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
					FacebookAuthorizer.RegisterAccessToken(facebookAccessToken, email, remoteWithQueryRemoved
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
			batcher = new Batcher<RevisionInternal>(workExecutor, InboxCapacity, ProcessorDelay
				, new _BatchProcessor_149(this));
			SetClientFactory(clientFactory);
		}

		private sealed class _BatchProcessor_149 : BatchProcessor<RevisionInternal>
		{
			public _BatchProcessor_149(Replication _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Process(IList<RevisionInternal> inbox)
			{
				Log.V(Database.Tag, "*** " + this.ToString() + ": BEGIN processInbox (" + inbox.Count
					 + " sequences)");
				this._enclosing.ProcessInbox(new RevisionList(inbox));
				Log.V(Database.Tag, "*** " + this.ToString() + ": END processInbox (lastSequence="
					 + this._enclosing.lastSequence);
				this._enclosing.active = false;
			}

			private readonly Replication _enclosing;
		}

		// this.clientFactory = clientFactory != null ? clientFactory : CouchbaseLiteHttpClientFactory.INSTANCE;
		/// <summary>
		/// Set the HTTP client factory if one was passed in, or use the default
		/// set in the manager if available.
		/// </summary>
		/// <remarks>
		/// Set the HTTP client factory if one was passed in, or use the default
		/// set in the manager if available.
		/// </remarks>
		/// <param name="clientFactory"></param>
		protected internal virtual void SetClientFactory(HttpClientFactory clientFactory)
		{
			Manager manager = null;
			if (this.db != null)
			{
				manager = this.db.GetManager();
			}
			HttpClientFactory managerClientFactory = null;
			if (manager != null)
			{
				managerClientFactory = manager.GetDefaultHttpClientFactory();
			}
			if (clientFactory != null)
			{
				this.clientFactory = clientFactory;
			}
			else
			{
				if (managerClientFactory != null)
				{
					this.clientFactory = managerClientFactory;
				}
				else
				{
					this.clientFactory = CouchbaseLiteHttpClientFactory.Instance;
				}
			}
		}

		/// <summary>Get the local database which is the source or target of this replication
		/// 	</summary>
		[InterfaceAudience.Public]
		public virtual Database GetLocalDatabase()
		{
			return db;
		}

		/// <summary>Get the remote URL which is the source or target of this replication</summary>
		[InterfaceAudience.Public]
		public virtual Uri GetRemoteUrl()
		{
			return remote;
		}

		/// <summary>Is this a pull replication?  (Eg, it pulls data from Sync Gateway -&gt; Device running CBL?)
		/// 	</summary>
		[InterfaceAudience.Public]
		public abstract bool IsPull();

		/// <summary>Should the target database be created if it doesn't already exist? (Defaults to NO).
		/// 	</summary>
		/// <remarks>Should the target database be created if it doesn't already exist? (Defaults to NO).
		/// 	</remarks>
		[InterfaceAudience.Public]
		public abstract bool ShouldCreateTarget();

		/// <summary>Set whether the target database be created if it doesn't already exist?</summary>
		[InterfaceAudience.Public]
		public abstract void SetCreateTarget(bool createTarget);

		/// <summary>
		/// Should the replication operate continuously, copying changes as soon as the
		/// source database is modified? (Defaults to NO).
		/// </summary>
		/// <remarks>
		/// Should the replication operate continuously, copying changes as soon as the
		/// source database is modified? (Defaults to NO).
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual bool IsContinuous()
		{
			return continuous;
		}

		/// <summary>Set whether the replication should operate continuously.</summary>
		/// <remarks>Set whether the replication should operate continuously.</remarks>
		[InterfaceAudience.Public]
		public virtual void SetContinuous(bool continuous)
		{
			if (!IsRunning())
			{
				this.continuous = continuous;
			}
		}

		/// <summary>Name of an optional filter function to run on the source server.</summary>
		/// <remarks>
		/// Name of an optional filter function to run on the source server. Only documents for
		/// which the function returns true are replicated.
		/// For a pull replication, the name looks like "designdocname/filtername".
		/// For a push replication, use the name under which you registered the filter with the Database.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual string GetFilter()
		{
			return filterName;
		}

		/// <summary>Set the filter to be used by this replication</summary>
		[InterfaceAudience.Public]
		public virtual void SetFilter(string filterName)
		{
			this.filterName = filterName;
		}

		/// <summary>Parameters to pass to the filter function.</summary>
		/// <remarks>Parameters to pass to the filter function.  Should map strings to strings.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual IDictionary<string, object> GetFilterParams()
		{
			return filterParams;
		}

		/// <summary>Set parameters to pass to the filter function.</summary>
		/// <remarks>Set parameters to pass to the filter function.</remarks>
		[InterfaceAudience.Public]
		public virtual void SetFilterParams(IDictionary<string, object> filterParams)
		{
			this.filterParams = filterParams;
		}

		/// <summary>List of Sync Gateway channel names to filter by; a nil value means no filtering, i.e.
		/// 	</summary>
		/// <remarks>
		/// List of Sync Gateway channel names to filter by; a nil value means no filtering, i.e. all
		/// available channels will be synced.  Only valid for pull replications whose source database
		/// is on a Couchbase Sync Gateway server.  (This is a convenience that just reads or
		/// changes the values of .filter and .query_params.)
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual IList<string> GetChannels()
		{
			if (filterParams == null || filterParams.IsEmpty())
			{
				return new AList<string>();
			}
			string @params = (string)filterParams.Get(ChannelsQueryParam);
			if (!IsPull() || GetFilter() == null || !GetFilter().Equals(ByChannelFilterName) 
				|| @params == null || @params.IsEmpty())
			{
				return new AList<string>();
			}
			string[] paramsArray = @params.Split(",");
			return new AList<string>(Arrays.AsList(paramsArray));
		}

		/// <summary>Set the list of Sync Gateway channel names</summary>
		[InterfaceAudience.Public]
		public virtual void SetChannels(IList<string> channels)
		{
			if (channels != null && !channels.IsEmpty())
			{
				if (!IsPull())
				{
					Log.W(Database.Tag, "filterChannels can only be set in pull replications");
					return;
				}
				SetFilter(ByChannelFilterName);
				IDictionary<string, object> filterParams = new Dictionary<string, object>();
				filterParams.Put(ChannelsQueryParam, TextUtils.Join(",", channels));
				SetFilterParams(filterParams);
			}
			else
			{
				if (GetFilter().Equals(ByChannelFilterName))
				{
					SetFilter(null);
					SetFilterParams(null);
				}
			}
		}

		/// <summary>Extra HTTP headers to send in all requests to the remote server.</summary>
		/// <remarks>
		/// Extra HTTP headers to send in all requests to the remote server.
		/// Should map strings (header names) to strings.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual IDictionary<string, object> GetHeaders()
		{
			return requestHeaders;
		}

		/// <summary>Set Extra HTTP headers to be sent in all requests to the remote server.</summary>
		/// <remarks>Set Extra HTTP headers to be sent in all requests to the remote server.</remarks>
		[InterfaceAudience.Public]
		public virtual void SetHeaders(IDictionary<string, object> requestHeadersParam)
		{
			if (requestHeadersParam != null && !requestHeaders.Equals(requestHeadersParam))
			{
				requestHeaders = requestHeadersParam;
			}
		}

		/// <summary>Gets the documents to specify as part of the replication.</summary>
		/// <remarks>Gets the documents to specify as part of the replication.</remarks>
		[InterfaceAudience.Public]
		public virtual IList<string> GetDocsIds()
		{
			return documentIDs;
		}

		/// <summary>Sets the documents to specify as part of the replication.</summary>
		/// <remarks>Sets the documents to specify as part of the replication.</remarks>
		[InterfaceAudience.Public]
		public virtual void SetDocIds(IList<string> docIds)
		{
			documentIDs = docIds;
		}

		/// <summary>The replication's current state, one of {stopped, offline, idle, active}.
		/// 	</summary>
		/// <remarks>The replication's current state, one of {stopped, offline, idle, active}.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual Replication.ReplicationStatus GetStatus()
		{
			return status;
		}

		/// <summary>The number of completed changes processed, if the task is active, else 0 (observable).
		/// 	</summary>
		/// <remarks>The number of completed changes processed, if the task is active, else 0 (observable).
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual int GetCompletedChangesCount()
		{
			return completedChangesCount;
		}

		/// <summary>The total number of changes to be processed, if the task is active, else 0 (observable).
		/// 	</summary>
		/// <remarks>The total number of changes to be processed, if the task is active, else 0 (observable).
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual int GetChangesCount()
		{
			return changesCount;
		}

		/// <summary>True while the replication is running, False if it's stopped.</summary>
		/// <remarks>
		/// True while the replication is running, False if it's stopped.
		/// Note that a continuous replication never actually stops; it only goes idle waiting for new
		/// data to appear.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual bool IsRunning()
		{
			return running;
		}

		/// <summary>
		/// The error status of the replication, or null if there have not been any errors since
		/// it started.
		/// </summary>
		/// <remarks>
		/// The error status of the replication, or null if there have not been any errors since
		/// it started.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Exception GetLastError()
		{
			return error;
		}

		/// <summary>Starts the replication, asynchronously.</summary>
		/// <remarks>Starts the replication, asynchronously.</remarks>
		[InterfaceAudience.Public]
		public virtual void Start()
		{
			if (running)
			{
				return;
			}
			this.sessionID = string.Format("repl%03d", ++lastSessionID);
			Log.V(Database.Tag, ToString() + " STARTING ...");
			running = true;
			lastSequence = null;
			CheckSession();
		}

		/// <summary>Stops replication, asynchronously.</summary>
		/// <remarks>Stops replication, asynchronously.</remarks>
		[InterfaceAudience.Public]
		public virtual void Stop()
		{
			if (!running)
			{
				return;
			}
			Log.V(Database.Tag, ToString() + " STOPPING...");
			batcher.Flush();
			continuous = false;
			if (asyncTaskCount == 0)
			{
				Stopped();
			}
		}

		/// <summary>Restarts a completed or failed replication.</summary>
		/// <remarks>Restarts a completed or failed replication.</remarks>
		[InterfaceAudience.Public]
		public virtual void Restart()
		{
			// TODO: add the "started" flag and check it here
			Stop();
			Start();
		}

		/// <summary>Adds a change delegate that will be called whenever the Replication changes.
		/// 	</summary>
		/// <remarks>Adds a change delegate that will be called whenever the Replication changes.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual void AddChangeListener(Replication.ChangeListener changeListener)
		{
			changeListeners.AddItem(changeListener);
		}

		[InterfaceAudience.Public]
		public override string ToString()
		{
			string maskedRemoteWithoutCredentials = (remote != null ? remote.ToExternalForm()
				 : string.Empty);
			maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
				, "://---:---@");
			string name = GetType().Name + "[" + maskedRemoteWithoutCredentials + "]";
			return name;
		}

		/// <summary>
		/// The type of event raised by a Replication when any of the following
		/// properties change: mode, running, error, completed, total.
		/// </summary>
		/// <remarks>
		/// The type of event raised by a Replication when any of the following
		/// properties change: mode, running, error, completed, total.
		/// </remarks>
		public class ChangeEvent
		{
			private Replication source;

			public ChangeEvent(Replication source)
			{
				this.source = source;
			}

			public virtual Replication GetSource()
			{
				return source;
			}
		}

		/// <summary>A delegate that can be used to listen for Replication changes.</summary>
		/// <remarks>A delegate that can be used to listen for Replication changes.</remarks>
		public interface ChangeListener
		{
			void Changed(Replication.ChangeEvent @event);
		}

		/// <summary>Removes the specified delegate as a listener for the Replication change event.
		/// 	</summary>
		/// <remarks>Removes the specified delegate as a listener for the Replication change event.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual void RemoveChangeListener(Replication.ChangeListener changeListener
			)
		{
			changeListeners.Remove(changeListener);
		}

		[InterfaceAudience.Private]
		public virtual void SetAuthorizer(Authorizer authorizer)
		{
			this.authorizer = authorizer;
		}

		[InterfaceAudience.Private]
		public virtual Authorizer GetAuthorizer()
		{
			return authorizer;
		}

		[InterfaceAudience.Private]
		public virtual void DatabaseClosing()
		{
			SaveLastSequence();
			Stop();
			db = null;
		}

		[InterfaceAudience.Private]
		public virtual string GetLastSequence()
		{
			return lastSequence;
		}

		[InterfaceAudience.Private]
		public virtual void SetLastSequence(string lastSequenceIn)
		{
			if (lastSequenceIn != null && !lastSequenceIn.Equals(lastSequence))
			{
				Log.V(Database.Tag, ToString() + ": Setting lastSequence to " + lastSequenceIn + 
					" from( " + lastSequence + ")");
				lastSequence = lastSequenceIn;
				if (!lastSequenceChanged)
				{
					lastSequenceChanged = true;
					workExecutor.Schedule(new _Runnable_521(this), 2 * 1000, TimeUnit.Milliseconds);
				}
			}
		}

		private sealed class _Runnable_521 : Runnable
		{
			public _Runnable_521(Replication _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Run()
			{
				this._enclosing.SaveLastSequence();
			}

			private readonly Replication _enclosing;
		}

		[InterfaceAudience.Private]
		internal virtual void SetCompletedChangesCount(int processed)
		{
			this.completedChangesCount = processed;
			NotifyChangeListeners();
		}

		[InterfaceAudience.Private]
		internal virtual void SetChangesCount(int total)
		{
			this.changesCount = total;
			NotifyChangeListeners();
		}

		[InterfaceAudience.Private]
		public virtual string GetSessionID()
		{
			return sessionID;
		}

		[InterfaceAudience.Private]
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

		[InterfaceAudience.Private]
		protected internal virtual void CheckSessionAtPath(string sessionPath)
		{
			AsyncTaskStarted();
			SendAsyncRequest("GET", sessionPath, null, new _RemoteRequestCompletionBlock_562(
				this, sessionPath));
		}

		private sealed class _RemoteRequestCompletionBlock_562 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_562(Replication _enclosing, string sessionPath
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
						Log.D(Database.Tag, string.Format("%s Active session, logged in as %s", this, username
							));
						this._enclosing.FetchRemoteCheckpointDoc();
					}
					else
					{
						Log.D(Database.Tag, string.Format("%s No active session, going to login", this));
						this._enclosing.Login();
					}
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly Replication _enclosing;

			private readonly string sessionPath;
		}

		[InterfaceAudience.Private]
		public abstract void BeginReplicating();

		[InterfaceAudience.Private]
		protected internal virtual void Stopped()
		{
			Log.V(Database.Tag, ToString() + " STOPPED");
			running = false;
			this.completedChangesCount = this.changesCount = 0;
			SaveLastSequence();
			NotifyChangeListeners();
			batcher = null;
			db = null;
		}

		[InterfaceAudience.Private]
		private void NotifyChangeListeners()
		{
			UpdateProgress();
			foreach (Replication.ChangeListener listener in changeListeners)
			{
				Replication.ChangeEvent changeEvent = new Replication.ChangeEvent(this);
				listener.Changed(changeEvent);
			}
		}

		[InterfaceAudience.Private]
		protected internal virtual void Login()
		{
			IDictionary<string, string> loginParameters = GetAuthorizer().LoginParametersForSite
				(remote);
			if (loginParameters == null)
			{
				Log.D(Database.Tag, string.Format("%s: %s has no login parameters, so skipping login"
					, this, GetAuthorizer()));
				FetchRemoteCheckpointDoc();
				return;
			}
			string loginPath = GetAuthorizer().LoginPathForSite(remote);
			Log.D(Database.Tag, string.Format("%s: Doing login with %s at %s", this, GetAuthorizer
				().GetType(), loginPath));
			AsyncTaskStarted();
			SendAsyncRequest("POST", loginPath, loginParameters, new _RemoteRequestCompletionBlock_631
				(this, loginPath));
		}

		private sealed class _RemoteRequestCompletionBlock_631 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_631(Replication _enclosing, string loginPath
				)
			{
				this._enclosing = _enclosing;
				this.loginPath = loginPath;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e != null)
				{
					Log.D(Database.Tag, string.Format("%s: Login failed for path: %s", this, loginPath
						));
					this._enclosing.error = e;
				}
				else
				{
					Log.D(Database.Tag, string.Format("%s: Successfully logged in!", this));
					this._enclosing.FetchRemoteCheckpointDoc();
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly Replication _enclosing;

			private readonly string loginPath;
		}

		[InterfaceAudience.Private]
		public virtual void AsyncTaskStarted()
		{
			lock (this)
			{
				++asyncTaskCount;
			}
		}

		[InterfaceAudience.Private]
		public virtual void AsyncTaskFinished(int numTasks)
		{
			lock (this)
			{
				this.asyncTaskCount -= numTasks;
				System.Diagnostics.Debug.Assert((asyncTaskCount >= 0));
				if (asyncTaskCount == 0)
				{
					if (!continuous)
					{
						Stopped();
					}
				}
			}
		}

		[InterfaceAudience.Private]
		public virtual void AddToInbox(RevisionInternal rev)
		{
			if (batcher.Count() == 0)
			{
				active = true;
			}
			batcher.QueueObject(rev);
		}

		[InterfaceAudience.Private]
		protected internal virtual void ProcessInbox(RevisionList inbox)
		{
		}

		[InterfaceAudience.Private]
		public virtual void SendAsyncRequest(string method, string relativePath, object body
			, RemoteRequestCompletionBlock onCompletion)
		{
			try
			{
				string urlStr = BuildRelativeURLString(relativePath);
				Uri url = new Uri(urlStr);
				SendAsyncRequest(method, url, body, onCompletion);
			}
			catch (UriFormatException e)
			{
				Log.E(Database.Tag, "Malformed URL for async request", e);
			}
		}

		[InterfaceAudience.Private]
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

		[InterfaceAudience.Private]
		public virtual void SendAsyncRequest(string method, Uri url, object body, RemoteRequestCompletionBlock
			 onCompletion)
		{
			RemoteRequest request = new RemoteRequest(workExecutor, clientFactory, method, url
				, body, GetHeaders(), onCompletion);
			remoteRequestExecutor.Execute(request);
		}

		[InterfaceAudience.Private]
		public virtual void SendAsyncMultipartDownloaderRequest(string method, string relativePath
			, object body, Database db, RemoteRequestCompletionBlock onCompletion)
		{
			try
			{
				string urlStr = BuildRelativeURLString(relativePath);
				Uri url = new Uri(urlStr);
				RemoteMultipartDownloaderRequest request = new RemoteMultipartDownloaderRequest(workExecutor
					, clientFactory, method, url, body, db, GetHeaders(), onCompletion);
				remoteRequestExecutor.Execute(request);
			}
			catch (UriFormatException e)
			{
				Log.E(Database.Tag, "Malformed URL for async request", e);
			}
		}

		[InterfaceAudience.Private]
		public virtual void SendAsyncMultipartRequest(string method, string relativePath, 
			MultipartEntity multiPartEntity, RemoteRequestCompletionBlock onCompletion)
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
			RemoteMultipartRequest request = new RemoteMultipartRequest(workExecutor, clientFactory
				, method, url, multiPartEntity, GetHeaders(), onCompletion);
			remoteRequestExecutor.Execute(request);
		}

		/// <summary>CHECKPOINT STORAGE:</summary>
		[InterfaceAudience.Private]
		internal virtual void MaybeCreateRemoteDB()
		{
		}

		// Pusher overrides this to implement the .createTarget option
		/// <summary>This is the _local document ID stored on the remote server to keep track of state.
		/// 	</summary>
		/// <remarks>
		/// This is the _local document ID stored on the remote server to keep track of state.
		/// Its ID is based on the local database ID (the private one, to make the result unguessable)
		/// and the remote database's URL.
		/// </remarks>
		[InterfaceAudience.Private]
		public virtual string RemoteCheckpointDocID()
		{
			if (db == null)
			{
				return null;
			}
			string input = db.PrivateUUID() + "\n" + remote.ToExternalForm() + "\n" + (!IsPull
				() ? "1" : "0");
			return Misc.TDHexSHA1Digest(Sharpen.Runtime.GetBytesForString(input));
		}

		[InterfaceAudience.Private]
		private bool Is404(Exception e)
		{
			if (e is HttpResponseException)
			{
				return ((HttpResponseException)e).GetStatusCode() == 404;
			}
			return false;
		}

		[InterfaceAudience.Private]
		public virtual void FetchRemoteCheckpointDoc()
		{
			lastSequenceChanged = false;
			string localLastSequence = db.LastSequenceWithRemoteURL(remote, !IsPull());
			if (localLastSequence == null)
			{
				MaybeCreateRemoteDB();
				BeginReplicating();
				return;
			}
			AsyncTaskStarted();
			SendAsyncRequest("GET", "/_local/" + RemoteCheckpointDocID(), null, new _RemoteRequestCompletionBlock_795
				(this, localLastSequence));
		}

		private sealed class _RemoteRequestCompletionBlock_795 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_795(Replication _enclosing, string localLastSequence
				)
			{
				this._enclosing = _enclosing;
				this.localLastSequence = localLastSequence;
			}

			public void OnCompletion(object result, Exception e)
			{
				if (e != null && !this._enclosing.Is404(e))
				{
					Log.D(Database.Tag, this + " error getting remote checkpoint: " + e);
					this._enclosing.error = e;
				}
				else
				{
					if (e != null && this._enclosing.Is404(e))
					{
						Log.D(Database.Tag, this + " 404 error getting remote checkpoint " + this._enclosing
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
						Log.V(Database.Tag, this + ": Replicating from lastSequence=" + this._enclosing.lastSequence
							);
					}
					else
					{
						Log.V(Database.Tag, this + ": lastSequence mismatch: I had " + localLastSequence 
							+ ", remote had " + remoteLastSequence);
					}
					this._enclosing.BeginReplicating();
				}
				this._enclosing.AsyncTaskFinished(1);
			}

			private readonly Replication _enclosing;

			private readonly string localLastSequence;
		}

		[InterfaceAudience.Private]
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
			Log.V(Database.Tag, this + " checkpointing sequence=" + lastSequence);
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
			SendAsyncRequest("PUT", "/_local/" + remoteCheckpointDocID, body, new _RemoteRequestCompletionBlock_854
				(this, body));
			// TODO: If error is 401 or 403, and this is a pull, remember that remote is read-only and don't attempt to read its checkpoint next time.
			db.SetLastSequence(lastSequence, remote, !IsPull());
		}

		private sealed class _RemoteRequestCompletionBlock_854 : RemoteRequestCompletionBlock
		{
			public _RemoteRequestCompletionBlock_854(Replication _enclosing, IDictionary<string
				, object> body)
			{
				this._enclosing = _enclosing;
				this.body = body;
			}

			public void OnCompletion(object result, Exception e)
			{
				this._enclosing.savingCheckpoint = false;
				if (e != null)
				{
					Log.V(Database.Tag, this + ": Unable to save remote checkpoint", e);
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

			private readonly Replication _enclosing;

			private readonly IDictionary<string, object> body;
		}

		[InterfaceAudience.Private]
		internal virtual bool GoOffline()
		{
			if (!online)
			{
				return false;
			}
			online = false;
			// TODO: [self stopRemoteRequests]; - remoteRequestExecutor.shutdown(); or remoteRequestExecutor.shutdownNow();
			UpdateProgress();
			return true;
		}

		[InterfaceAudience.Private]
		internal virtual void UpdateProgress()
		{
			if (!IsRunning())
			{
				status = Replication.ReplicationStatus.ReplicationStopped;
			}
			else
			{
				if (!online)
				{
					status = Replication.ReplicationStatus.ReplicationOffline;
				}
				else
				{
					if (active)
					{
						status = Replication.ReplicationStatus.ReplicationActive;
					}
					else
					{
						status = Replication.ReplicationStatus.ReplicationIdle;
					}
				}
			}
		}
	}
}
