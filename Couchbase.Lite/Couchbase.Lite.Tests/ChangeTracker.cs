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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;








using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;

using Sharpen;

namespace Couchbase.Lite.Replicator
{
	/// <summary>
	/// Reads the continuous-mode _changes feed of a database, and sends the
	/// individual change entries to its client's changeTrackerReceivedChange()
	/// </summary>
	public class ChangeTracker : Runnable
	{
		private Uri databaseURL;

		private IChangeTrackerClient client;

		private ChangeTracker.ChangeTrackerMode mode;

		private object lastSequenceID;

		private Sharpen.Thread thread;

		private bool running = false;

		private HttpRequestMessage request;

		private string filterName;

		private IDictionary<string, object> filterParams;

		private IList<string> docIDs;

		private Exception error;

		protected internal IDictionary<string, object> requestHeaders;

		public enum ChangeTrackerMode
		{
			OneShot,
			LongPoll,
			Continuous
		}

		public ChangeTracker(Uri databaseURL, ChangeTracker.ChangeTrackerMode mode, object
			 lastSequenceID, IChangeTrackerClient client)
		{
			this.databaseURL = databaseURL;
			this.mode = mode;
			this.lastSequenceID = lastSequenceID;
			this.client = client;
			this.requestHeaders = new Dictionary<string, object>();
		}

		public virtual void SetFilterName(string filterName)
		{
			this.filterName = filterName;
		}

		public virtual void SetFilterParams(IDictionary<string, object> filterParams)
		{
			this.filterParams = filterParams;
		}

		public virtual void SetClient(IChangeTrackerClient client)
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
				case ChangeTracker.ChangeTrackerMode.OneShot:
				{
					path += "normal";
					break;
				}

				case ChangeTracker.ChangeTrackerMode.LongPoll:
				{
					path += "longpoll&limit=50";
					break;
				}

				case ChangeTracker.ChangeTrackerMode.Continuous:
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
			if (docIDs != null && docIDs.Count > 0)
			{
				filterName = "_doc_ids";
				filterParams = new Dictionary<string, object>();
				filterParams["doc_ids"] = docIDs;
			}
			if (filterName != null)
			{
				path += "&filter=" + URLEncoder.Encode(filterName);
				if (filterParams != null)
				{
					foreach (string filterParamKey in filterParams.Keys)
					{
						object value = filterParams[filterParamKey];
						if (!(value is string))
						{
							try
							{
								value = Manager.GetObjectMapper().WriteValueAsString(value);
							}
							catch (IOException e)
							{
								throw new ArgumentException(e);
							}
						}
						path += "&" + URLEncoder.Encode(filterParamKey) + "=" + URLEncoder.Encode(value.ToString
							());
					}
				}
			}
			return path;
		}

		public virtual Uri GetChangesFeedURL()
		{
			string dbURLString = databaseURL.ToString();
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
				Log.E(Database.Tag, "Changes feed ULR is malformed", e);
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
				Log.W(Database.Tag, "ChangeTracker run() loop aborting because client == null");
				return;
			}
			if (mode == ChangeTracker.ChangeTrackerMode.Continuous)
			{
				// there is a failing unit test for this, and from looking at the code the Replication
				// object will never use Continuous mode anyway.  Explicitly prevent its use until
				// it is demonstrated to actually work.
				throw new RuntimeException("ChangeTracker does not correctly support continuous mode"
					);
			}
			httpClient = client.GetHttpClient();
			ChangeTrackerBackoff backoff = new ChangeTrackerBackoff();
			while (running)
			{
				Uri url = GetChangesFeedURL();
                request = new HttpRequestMessage(url.ToString());
				AddRequestHeaders(request);
				// if the URL contains user info AND if this a DefaultHttpClient
				// then preemptively set the auth credentials
				if (url.GetUserInfo() != null)
				{
					Log.V(Database.Tag, "url.getUserInfo(): " + url.GetUserInfo());
					if (url.GetUserInfo().Contains(":") && !url.GetUserInfo().Trim().Equals(":"))
					{
						string[] userInfoSplit = url.GetUserInfo().Split(":");
                        throw new NotImplementedException();
//						Credentials creds = new UsernamePasswordCredentials(URIUtils.Decode(userInfoSplit
//							[0]), URIUtils.Decode(userInfoSplit[1]));
//						if (httpClient is DefaultHttpClient)
//						{
//							DefaultHttpClient dhc = (DefaultHttpClient)httpClient;
//							MessageProcessingHandler preemptiveAuth = new _MessageProcessingHandler_212(creds
//								);
//                            dhc.AddRequestInterceptor((HttpWebRequest request, HttpContext context)=>
//                                {
//                                    AuthState authState = (AuthState)context.GetAttribute(ClientContext.TargetAuthState
//                                    );
//                                    CredentialsProvider credsProvider = (CredentialsProvider)context.GetAttribute(ClientContext
//                                        .CredsProvider);
//                                    HttpHost targetHost = (HttpHost)context.GetAttribute(ExecutionContext.HttpTargetHost
//                                    );
//                                    if (authState.GetAuthScheme() == null)
//                                    {
//                                        AuthScope authScope = new AuthScope(targetHost.GetHostName(), targetHost.GetPort(
//                                        ));
//                                        authState.SetAuthScheme(new BasicScheme());
//                                        authState.SetCredentials(creds);
//                                    }
//                                }, 0);
//						}
					}
					else
					{
						Log.W(Database.Tag, "ChangeTracker Unable to parse user info, not setting credentials"
							);
					}
				}
				try
				{
					string maskedRemoteWithoutCredentials = GetChangesFeedURL().ToString();
					maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
						, "://---:---@");
					Log.V(Database.Tag, "Making request to " + maskedRemoteWithoutCredentials);
					HttpResponse response = httpClient.Execute(request);
					StatusLine status = response.GetStatusLine();
					if (status.GetStatusCode() >= 300)
					{
						Log.E(Database.Tag, "Change tracker got error " + Sharpen.Extensions.ToString(status
							.GetStatusCode()));
						string msg = string.Format(status.ToString());
						this.error = new CouchbaseLiteException(msg, new Status(status.GetStatusCode()));
						Stop();
					}
					HttpEntity entity = response.GetEntity();
					InputStream input = null;
					if (entity != null)
					{
						input = entity.GetContent();
						if (mode == ChangeTracker.ChangeTrackerMode.LongPoll)
						{
							IDictionary<string, object> fullBody = Manager.GetObjectMapper().ReadValue<IDictionary
								>(input);
							bool responseOK = ReceivedPollResponse(fullBody);
							if (mode == ChangeTracker.ChangeTrackerMode.LongPoll && responseOK)
							{
								Log.V(Database.Tag, "Starting new longpoll");
								continue;
							}
							else
							{
								Log.W(Database.Tag, "Change tracker calling stop");
								Stop();
							}
						}
						else
						{
							JsonFactory jsonFactory = Manager.GetObjectMapper().GetJsonFactory();
							JsonParser jp = jsonFactory.CreateJsonParser(input);
							while (jp.CurrentToken() != JsonToken.StartArray)
							{
							}
							// ignore these tokens
							while (jp.CurrentToken() == JsonToken.StartObject)
							{
								IDictionary<string, object> change = (IDictionary)Manager.GetObjectMapper().ReadValue
									<IDictionary>(jp);
								if (!ReceivedChange(change))
								{
									Log.W(Database.Tag, string.Format("Received unparseable change line from server: %s"
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
						Log.E(Database.Tag, "Exception in change tracker", e);
					}
					backoff.SleepAppropriateAmountOfTime();
				}
			}
			Log.V(Database.Tag, "Change tracker run loop exiting");
		}

		public virtual bool ReceivedChange(IDictionary<string, object> change)
		{
			object seq = change["seq"];
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
			IList<IDictionary<string, object>> changes = (IList)response["results"];
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
			Log.W(Database.Tag, string.Format("Server error: %s", message));
			this.error = new Exception(message);
		}

		public virtual bool Start()
		{
			this.error = null;
			string maskedRemoteWithoutCredentials = databaseURL.ToString();
			maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@"
				, "://---:---@");
			thread = new Sharpen.Thread(this, "ChangeTracker-" + maskedRemoteWithoutCredentials
				);
			thread.Start();
			return true;
		}

		public virtual void Stop()
		{
			Log.D(Database.Tag, "changed tracker asked to stop");
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
			Log.D(Database.Tag, "change tracker in stopped");
			if (client != null)
			{
				Log.D(Database.Tag, "posting stopped");
				client.ChangeTrackerStopped(this);
			}
			client = null;
			Log.D(Database.Tag, "change tracker client should be null now");
		}

		internal virtual void SetRequestHeaders(IDictionary<string, object> requestHeaders
			)
		{
			this.requestHeaders = requestHeaders;
		}

		private void AddRequestHeaders(HttpRequestMessage request)
		{
			foreach (string requestHeaderKey in requestHeaders.Keys)
			{
				request.AddHeader(requestHeaderKey, requestHeaders[requestHeaderKey].ToString
					());
			}
		}

		public virtual Exception GetLastError()
		{
			return error;
		}

		public virtual bool IsRunning()
		{
			return running;
		}

		public virtual void SetDocIDs(IList<string> docIDs)
		{
			this.docIDs = docIDs;
		}
	}
}
