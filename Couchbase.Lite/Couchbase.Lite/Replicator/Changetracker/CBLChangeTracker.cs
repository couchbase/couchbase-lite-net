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
using System.IO;
using Couchbase;
using Couchbase.Replicator.Changetracker;
using Couchbase.Util;
using Org.Apache.Http;
using Org.Apache.Http.Auth;
using Org.Apache.Http.Client;
using Org.Apache.Http.Client.Methods;
using Org.Apache.Http.Client.Protocol;
using Org.Apache.Http.Impl.Client;
using Org.Apache.Http.Protocol;
using Org.Codehaus.Jackson;
using Sharpen;

namespace Couchbase.Replicator.Changetracker
{
	/// <summary>
	/// Reads the continuous-mode _changes feed of a database, and sends the
	/// individual change entries to its client's changeTrackerReceivedChange()
	/// </summary>
	public class CBLChangeTracker : Runnable
	{
		private Uri databaseURL;

		private CBLChangeTrackerClient client;

		private CBLChangeTracker.TDChangeTrackerMode mode;

		private object lastSequenceID;

		private Sharpen.Thread thread;

		private bool running = false;

		private HttpUriRequest request;

		private string filterName;

		private IDictionary<string, object> filterParams;

		private Exception error;

		public enum TDChangeTrackerMode
		{
			OneShot,
			LongPoll,
			Continuous
		}

		public CBLChangeTracker(Uri databaseURL, CBLChangeTracker.TDChangeTrackerMode mode
			, object lastSequenceID, CBLChangeTrackerClient client)
		{
			this.databaseURL = databaseURL;
			this.mode = mode;
			this.lastSequenceID = lastSequenceID;
			this.client = client;
		}

		public virtual void SetFilterName(string filterName)
		{
			this.filterName = filterName;
		}

		public virtual void SetFilterParams(IDictionary<string, object> filterParams)
		{
			this.filterParams = filterParams;
		}

		public virtual void SetClient(CBLChangeTrackerClient client)
		{
			this.client = client;
		}

		public virtual string GetDatabaseName()
		{
			string result = null;
			if (databaseURL != null)
			{
				result = databaseURL.AbsolutePath;
				if (result != null)
				{
					int pathLastSlashPos = result.LastIndexOf('/');
					if (pathLastSlashPos > 0)
					{
						result = Sharpen.Runtime.Substring(result, pathLastSlashPos);
					}
				}
			}
			return result;
		}

		public virtual string GetChangesFeedPath()
		{
			string path = "_changes?feed=";
			switch (mode)
			{
				case CBLChangeTracker.TDChangeTrackerMode.OneShot:
				{
					path += "normal";
					break;
				}

				case CBLChangeTracker.TDChangeTrackerMode.LongPoll:
				{
					path += "longpoll&limit=50";
					break;
				}

				case CBLChangeTracker.TDChangeTrackerMode.Continuous:
				{
					path += "continuous";
					break;
				}
			}
			path += "&heartbeat=300000";
			if (lastSequenceID != null)
			{
				path += "&since=" + URLEncoder.Encode(lastSequenceID.ToString());
			}
			if (filterName != null)
			{
				path += "&filter=" + URLEncoder.Encode(filterName);
				if (filterParams != null)
				{
					foreach (string filterParamKey in filterParams.Keys)
					{
						path += "&" + URLEncoder.Encode(filterParamKey) + "=" + URLEncoder.Encode(filterParams
							.Get(filterParamKey).ToString());
					}
				}
			}
			return path;
		}

		public virtual Uri GetChangesFeedURL()
		{
			string dbURLString = databaseURL.ToExternalForm();
			if (!dbURLString.EndsWith("/"))
			{
				dbURLString += "/";
			}
			dbURLString += GetChangesFeedPath();
			Uri result = null;
			try
			{
				result = new Uri(dbURLString);
			}
			catch (UriFormatException e)
			{
				Log.E(CBLDatabase.Tag, "Changes feed ULR is malformed", e);
			}
			return result;
		}

		public virtual void Run()
		{
			running = true;
			HttpClient httpClient;
			if (client == null)
			{
				// This is a race condition that can be reproduced by calling cbpuller.start() and cbpuller.stop()
				// directly afterwards.  What happens is that by the time the Changetracker thread fires up,
				// the cbpuller has already set this.client to null.  See issue #109
				Log.W(CBLDatabase.Tag, "ChangeTracker run() loop aborting because client == null"
					);
				return;
			}
			httpClient = client.GetHttpClient();
			CBLChangeTrackerBackoff backoff = new CBLChangeTrackerBackoff();
			while (running)
			{
				Uri url = GetChangesFeedURL();
				request = new HttpGet(url.ToString());
				// if the URL contains user info AND if this a DefaultHttpClient
				// then preemptively set the auth credentials
				if (url.GetUserInfo() != null)
				{
					Log.V(CBLDatabase.Tag, "url.getUserInfo(): " + url.GetUserInfo());
					if (url.GetUserInfo().Contains(":") && !url.GetUserInfo().Trim().Equals(":"))
					{
						string[] userInfoSplit = url.GetUserInfo().Split(":");
						Credentials creds = new UsernamePasswordCredentials(Uri.Decode(userInfoSplit[0]), 
							Uri.Decode(userInfoSplit[1]));
						if (httpClient is DefaultHttpClient)
						{
							DefaultHttpClient dhc = (DefaultHttpClient)httpClient;
							IHttpRequestInterceptor preemptiveAuth = new _IHttpRequestInterceptor_178(creds);
							dhc.AddRequestInterceptor(preemptiveAuth, 0);
						}
					}
					else
					{
						Log.W(CBLDatabase.Tag, "ChangeTracker Unable to parse user info, not setting credentials"
							);
					}
				}
				try
				{
					string maskedRemoteWithoutCredentials = GetChangesFeedURL().ToString();
					maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
						, "://---:---@");
					Log.V(CBLDatabase.Tag, "Making request to " + maskedRemoteWithoutCredentials);
					HttpResponse response = httpClient.Execute(request);
					StatusLine status = response.GetStatusLine();
					if (status.GetStatusCode() >= 300)
					{
						Log.E(CBLDatabase.Tag, "Change tracker got error " + Sharpen.Extensions.ToString(
							status.GetStatusCode()));
						Stop();
					}
					HttpEntity entity = response.GetEntity();
					InputStream input = null;
					if (entity != null)
					{
						input = entity.GetContent();
						if (mode == CBLChangeTracker.TDChangeTrackerMode.LongPoll)
						{
							IDictionary<string, object> fullBody = CBLServer.GetObjectMapper().ReadValue<IDictionary
								>(input);
							bool responseOK = ReceivedPollResponse(fullBody);
							if (mode == CBLChangeTracker.TDChangeTrackerMode.LongPoll && responseOK)
							{
								Log.V(CBLDatabase.Tag, "Starting new longpoll");
								continue;
							}
							else
							{
								Log.W(CBLDatabase.Tag, "Change tracker calling stop");
								Stop();
							}
						}
						else
						{
							JsonFactory jsonFactory = CBLServer.GetObjectMapper().GetJsonFactory();
							JsonParser jp = jsonFactory.CreateJsonParser(input);
							while (jp.NextToken() != JsonToken.StartArray)
							{
							}
							// ignore these tokens
							while (jp.NextToken() == JsonToken.StartObject)
							{
								IDictionary<string, object> change = (IDictionary)CBLServer.GetObjectMapper().ReadValue
									<IDictionary>(jp);
								if (!ReceivedChange(change))
								{
									Log.W(CBLDatabase.Tag, string.Format("Received unparseable change line from server: %s"
										, change));
								}
							}
							Stop();
							break;
						}
						backoff.ResetBackoff();
					}
				}
				catch (Exception e)
				{
					if (!running && e is IOException)
					{
					}
					else
					{
						// in this case, just silently absorb the exception because it
						// frequently happens when we're shutting down and have to
						// close the socket underneath our read.
						Log.E(CBLDatabase.Tag, "Exception in change tracker", e);
					}
					backoff.SleepAppropriateAmountOfTime();
				}
			}
			Log.V(CBLDatabase.Tag, "Change tracker run loop exiting");
		}

		private sealed class _IHttpRequestInterceptor_178 : IHttpRequestInterceptor
		{
			public _IHttpRequestInterceptor_178(Credentials creds)
			{
				this.creds = creds;
			}

			/// <exception cref="Org.Apache.Http.HttpException"></exception>
			/// <exception cref="System.IO.IOException"></exception>
			public void Process(IHttpRequest request, HttpContext context)
			{
				AuthState authState = (AuthState)context.GetAttribute(ClientContext.TargetAuthState
					);
				CredentialsProvider credsProvider = (CredentialsProvider)context.GetAttribute(ClientContext
					.CredsProvider);
				HttpHost targetHost = (HttpHost)context.GetAttribute(ExecutionContext.HttpTargetHost
					);
				if (authState.GetAuthScheme() == null)
				{
					AuthScope authScope = new AuthScope(targetHost.GetHostName(), targetHost.GetPort(
						));
					authState.SetAuthScheme(new BasicScheme());
					authState.SetCredentials(creds);
				}
			}

			private readonly Credentials creds;
		}

		public virtual bool ReceivedChange(IDictionary<string, object> change)
		{
			object seq = change.Get("seq");
			if (seq == null)
			{
				return false;
			}
			//pass the change to the client on the thread that created this change tracker
			if (client != null)
			{
				client.ChangeTrackerReceivedChange(change);
			}
			lastSequenceID = seq;
			return true;
		}

		public virtual bool ReceivedPollResponse(IDictionary<string, object> response)
		{
			IList<IDictionary<string, object>> changes = (IList)response.Get("results");
			if (changes == null)
			{
				return false;
			}
			foreach (IDictionary<string, object> change in changes)
			{
				if (!ReceivedChange(change))
				{
					return false;
				}
			}
			return true;
		}

		public virtual void SetUpstreamError(string message)
		{
			Log.W(CBLDatabase.Tag, string.Format("Server error: %s", message));
			this.error = new Exception(message);
		}

		public virtual bool Start()
		{
			this.error = null;
			string maskedRemoteWithoutCredentials = databaseURL.ToExternalForm();
			maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
				, "://---:---@");
			thread = new Sharpen.Thread(this, "ChangeTracker-" + maskedRemoteWithoutCredentials
				);
			thread.Start();
			return true;
		}

		public virtual void Stop()
		{
			Log.D(CBLDatabase.Tag, "changed tracker asked to stop");
			running = false;
			thread.Interrupt();
			if (request != null)
			{
				request.Abort();
			}
			Stopped();
		}

		public virtual void Stopped()
		{
			Log.D(CBLDatabase.Tag, "change tracker in stopped");
			if (client != null)
			{
				Log.D(CBLDatabase.Tag, "posting stopped");
				client.ChangeTrackerStopped(this);
			}
			client = null;
			Log.D(CBLDatabase.Tag, "change tracker client should be null now");
		}

		public virtual bool IsRunning()
		{
			return running;
		}
	}
}
