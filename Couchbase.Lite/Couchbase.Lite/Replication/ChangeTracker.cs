//
// ChangeTracker.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Sharpen;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Threading;
using System.Web.UI;
using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;

namespace Couchbase.Lite.Replicator
{
	/// <summary>
	/// Reads the continuous-mode _changes feed of a database, and sends the
	/// individual change entries to its client's changeTrackerReceivedChange()
	/// </summary>
    internal class ChangeTracker : Runnable
	{
        const String Tag = "ChangeTracker";

		private Uri databaseURL;

		private IChangeTrackerClient client;

		private ChangeTracker.ChangeTrackerMode mode;

        private Object lastSequenceID;

        private Boolean includeConflicts;

        private Sharpen.Thread thread;

        private Boolean running = false;

        private HttpRequestMessage Request;

        private String filterName;

        private IDictionary<String, Object> filterParams;

        private IList<String> docIDs;

		private Exception Error;

		protected internal IDictionary<string, object> RequestHeaders;

        private ChangeTrackerBackoff backoff;

        private readonly CancellationTokenSource tokenSource;

        System.Threading.Tasks.Task CurrentRequest;

		public enum ChangeTrackerMode
		{
			OneShot,
			LongPoll,
			Continuous
		}

        public ChangeTracker(Uri databaseURL, ChangeTracker.ChangeTrackerMode mode, object lastSequenceID, 
            Boolean includeConflicts, IChangeTrackerClient client)
		{
			this.databaseURL = databaseURL;
			this.mode = mode;
            this.includeConflicts = includeConflicts;
			this.lastSequenceID = lastSequenceID;
			this.client = client;
			this.RequestHeaders = new Dictionary<string, object>();
            this.tokenSource = new CancellationTokenSource();
		}

		public void SetFilterName(string filterName)
		{
			this.filterName = filterName;
		}

        public void SetFilterParams(IDictionary<String, Object> filterParams)
		{
			this.filterParams = filterParams;
		}

		public void SetClient(IChangeTrackerClient client)
		{
			this.client = client;
		}

		public string GetDatabaseName()
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

		public string GetChangesFeedPath()
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
            if (includeConflicts) 
            {
                path += "&style=all_docs";
            }
			if (lastSequenceID != null)
			{
                path += "&since=" + HttpUtility.UrlEncode(lastSequenceID.ToString());
			}
			if (docIDs != null && docIDs.Count > 0)
			{
				filterName = "_doc_ids";
                filterParams = new Dictionary<String, Object>();
                filterParams["doc_ids"] = docIDs;
			}
			if (filterName != null)
			{
				path += "&filter=" + HttpUtility.UrlEncode(filterName);
				if (filterParams != null)
				{
					foreach (string filterParamKey in filterParams.Keys)
					{
                        var value = filterParams.Get(filterParamKey);
						if (!(value is string))
						{
							try
							{
								value = Manager.GetObjectMapper().WriteValueAsString(value);
							}
							catch (IOException e)
							{
                                throw new ArgumentException(Tag, e);
							}
						}
						path += "&" + HttpUtility.UrlEncode(filterParamKey) + "=" + HttpUtility.UrlEncode(value.ToString());
					}
				}
			}
			return path;
		}

		public Uri GetChangesFeedURL()
		{
            var dbURLString = databaseURL.ToString();
			if (!dbURLString.EndsWith ("/", StringComparison.Ordinal))
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
                Log.E(Tag, this + ": Changes feed ULR is malformed", e);
			}
			return result;
		}

        // TODO: Needs to refactored into smaller calls. Each continuation could be its own method, for example.
		public void Run()
		{
			running = true;
			HttpClient httpClient;

			if (client == null)
			{
				// This is a race condition that can be reproduced by calling cbpuller.start() and cbpuller.stop()
				// directly afterwards.  What happens is that by the time the Changetracker thread fires up,
				// the cbpuller has already set this.client to null.  See issue #109
                Log.W(Tag, this + ": ChangeTracker run() loop aborting because client == null");
				return;
			}
			if (mode == ChangeTracker.ChangeTrackerMode.Continuous)
			{
				// there is a failing unit test for this, and from looking at the code the Replication
				// object will never use Continuous mode anyway.  Explicitly prevent its use until
				// it is demonstrated to actually work.
				throw new RuntimeException("ChangeTracker does not correctly support continuous mode");
			}

			httpClient = client.GetHttpClient();
            backoff = new ChangeTrackerBackoff();

            var shouldBreak = false;
			while (running)
			{
                if (tokenSource.Token.IsCancellationRequested)
                    break;

                var url = GetChangesFeedURL();
                Request = new HttpRequestMessage(HttpMethod.Get, url);

				AddRequestHeaders(Request);

				// if the URL contains user info AND if this a DefaultHttpClient
                // then preemptively set/update the auth credentials
                if (!String.IsNullOrEmpty(url.UserInfo))
				{
					Log.V(Tag, "url.getUserInfo(): " + url.GetUserInfo());

                    var credentials = Request.ToCredentialsFromUri();
                    if (credentials != null)
					{
                        var handler = client.HttpHandler;
                        if (handler.Credentials == null || !handler.Credentials.Equals(credentials))
                            client.HttpHandler.Credentials = credentials;
					}
					else
					{
                        Log.W(Tag, this + ": ChangeTracker Unable to parse user info, not setting credentials");
					}
				}

				try
				{
                    var requestStatus = CurrentRequest == null 
                        ? TaskStatus.Canceled 
                        : CurrentRequest.Status;

                    Log.V(Tag, this + ": Current Request Status: " + requestStatus);

                    if (requestStatus == TaskStatus.Running || requestStatus == TaskStatus.WaitingForActivation) 
                    {
                        //System.Threading.Thread.Sleep(5000);
                        continue;
                    }
                    var maskedRemoteWithoutCredentials = GetChangesFeedURL().ToString();
                    maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@", "://---:---@");
                    Log.V(Tag, this + ": Making request to " + maskedRemoteWithoutCredentials);
                    if (tokenSource.Token.IsCancellationRequested)
                        break;
                    CurrentRequest = httpClient.SendAsync(Request)
                        .ContinueWith<HttpResponseMessage>(request=>
                        {
                            if (request.Status != System.Threading.Tasks.TaskStatus.RanToCompletion && request.IsFaulted)
                            {
                                Log.E(Tag, this + ": Change tracker got error " + Extensions.ToString(request.Status));
                                throw request.Exception;
                            }
                            return request.Result;
                        }, this.tokenSource.Token)
                        .ContinueWith<Task<Byte[]>>((request) =>
                        {
                            var status = request.Result.StatusCode;
                            if ((Int32)status >= 300)
                            {
                                var msg = String.Format("Change tracker got error: {0}", status);
                                Log.E(Tag, msg);
                                Error = new CouchbaseLiteException (msg, new Status (status.GetStatusCode ()));
                                Stop();
                            }
                            return request.Result.Content.ReadAsByteArrayAsync();
                        }, this.tokenSource.Token)
                        .ContinueWith((Task<Task<Byte[]>> response) => 
                        {
                            if (response.Status != System.Threading.Tasks.TaskStatus.RanToCompletion
                                && !response.IsFaulted
                                && response.Result != null)
                                return;

                            var result = response.Result.Result;

                            if (mode == ChangeTrackerMode.LongPoll)
                            {
                                var fullBody = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(result.AsEnumerable());
                                var responseOK = ReceivedPollResponse(fullBody);
                                if (mode == ChangeTracker.ChangeTrackerMode.LongPoll && responseOK)
                                {
                                    Log.V(Tag, this + ": Starting new longpoll");
                                    backoff.ResetBackoff();
                                    return;
                                }
                                else
                                {
                                    Log.W(Tag, this + ": Change tracker calling stop");
                                    Stop();
                                }
                            }
                            else
                            {
                                var results = Manager.GetObjectMapper().ReadValue<IDictionary<String, Object>>(result.AsEnumerable());
                                var resultsValue = results["results"] as Newtonsoft.Json.Linq.JArray;
                                foreach (var item in resultsValue)
                                {
                                    IDictionary<String, Object> change = null;
                                    try {
                                        change = item.ToObject<IDictionary<String, Object>>();
                                    } catch (Exception) {
                                        Log.E(Tag, this + string.Format(": Received unparseable change line from server: {0}", change));
                                    }
                                    if (!ReceivedChange(change))
                                    {
                                        Log.W(Tag, this + string.Format(": Received unparseable change line from server: {0}", change));
                                    }
                                }
                                Stop();
                                shouldBreak = true;
                                return;
                            }
                            backoff.ResetBackoff();
                        }, tokenSource.Token);
				}
				catch (Exception e)
				{
					if (!running && e is IOException)
                    {
                        // swallow
                    }
					else
					{
						// in this case, just silently absorb the exception because it
						// frequently happens when we're shutting down and have to
						// close the socket underneath our read.
                        Log.E(Tag, this + ": Exception in change tracker", e);
					}
					backoff.SleepAppropriateAmountOfTime();
				}
                if (shouldBreak) break;
			}
            if (!tokenSource.Token.IsCancellationRequested)
            {   // Handle cancellation requests while we are waiting.
                // e.g. when Stop() is called from another thread.
                try {
                    CurrentRequest.Wait (tokenSource.Token);
                } catch (Exception) {
                    Log.V(Tag, this + ": Run loop was cancelled.");
                }
            }
            Log.V(Tag, this + ": Change tracker run loop exiting");
		}

		public bool ReceivedChange(IDictionary<string, object> change)
		{
            var seq = change.Get("seq");
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

		public bool ReceivedPollResponse(IDictionary<string, object> response)
		{
            var changes = (IList<IDictionary<string, object>>)response.Get("results");
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

		public void SetUpstreamError(string message)
		{
            Log.W(Tag, this + string.Format(": Server error: {0}", message));
			this.Error = new Exception(message);
		}

		public bool Start()
		{
			this.Error = null;
			string maskedRemoteWithoutCredentials = databaseURL.ToString();
			maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@", "://---:---@");
            // TODO: Replace Thread object with TPL.
            thread = new Sharpen.Thread(this, "ChangeTracker-" + maskedRemoteWithoutCredentials);
			thread.Start();
			return true;
		}

		public void Stop()
		{
            Log.D(Tag, this + ": changed tracker asked to stop");
			running = false;
			thread.Interrupt();
			if (Request != null)
			{
                tokenSource.Cancel();
			}
			Stopped();
		}

		public void Stopped()
		{
            Log.D(Tag, this + ": change tracker in stopped");
			if (client != null)
			{
                Log.D(Tag, this + ": posting stopped");
				client.ChangeTrackerStopped(this);
			}
			client = null;
            Log.D(Tag, this + ": change tracker client should be null now");
		}

        internal virtual void SetRequestHeaders(IDictionary<String, Object> requestHeaders)
		{
			RequestHeaders = requestHeaders;
		}

        private void AddRequestHeaders(HttpRequestMessage request)
		{
			foreach (string requestHeaderKey in RequestHeaders.Keys)
			{
                request.Headers.Add(requestHeaderKey, RequestHeaders.Get(requestHeaderKey).ToString());
			}
		}

		public Exception GetLastError()
		{
			return Error;
		}

		public bool IsRunning()
		{
			return running;
		}

		public void SetDocIDs(IList<string> docIDs)
		{
			this.docIDs = docIDs;
		}
	}

}
