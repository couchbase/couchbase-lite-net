//
// ChangeTracker.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
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
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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

        private TaskFactory WorkExecutor;

        private Task runTask;

        private Boolean running = false;

        private readonly object runningMutex = new object();

        private HttpRequestMessage Request;

        private String filterName;

        private IDictionary<String, Object> filterParams;

        private IList<String> docIDs;

		private Exception Error;

        private bool shouldBreak;

        internal ChangeTrackerBackoff backoff;

		protected internal IDictionary<string, object> RequestHeaders;

        private readonly CancellationTokenSource tokenSource;

        internal enum ChangeTrackerMode
		{
			OneShot,
			LongPoll,
			Continuous
		}

        public ChangeTracker(Uri databaseURL, ChangeTracker.ChangeTrackerMode mode, object lastSequenceID, 
            Boolean includeConflicts, IChangeTrackerClient client, TaskFactory workExecutor = null)
		{
			this.databaseURL = databaseURL;
			this.mode = mode;
            this.includeConflicts = includeConflicts;
			this.lastSequenceID = lastSequenceID;
			this.client = client;
			this.RequestHeaders = new Dictionary<string, object>();
            this.tokenSource = new CancellationTokenSource();
            WorkExecutor = workExecutor ?? Task.Factory;
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
        public async void Run()
		{
            if (IsRunning()) 
            {
                return;
            }

            running = true;

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

            var httpClient = client.GetHttpClient();
            backoff = new ChangeTrackerBackoff();

            this.shouldBreak = false;
            while (IsRunning())
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
                    var maskedRemoteWithoutCredentials = GetChangesFeedURL().ToString();
                    maskedRemoteWithoutCredentials = maskedRemoteWithoutCredentials.ReplaceAll("://.*:.*@", "://---:---@");
                    Log.V(Tag, this + ": Making request to " + maskedRemoteWithoutCredentials);

                    if (tokenSource.Token.IsCancellationRequested)
                        break;
                        
                    var response = await httpClient.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, tokenSource.Token);
                    await ChangeFeedResponseHandler(response);

                    response.Dispose();
                    Request.Dispose();
				}
				catch (Exception e)
				{
                    if (!IsRunning() && e is IOException)
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

                if (runTask.Exception != null) {
                    Log.E(Tag, "Unhandled exception", runTask.Exception);
                    throw runTask.Exception;
                }

                if (this.shouldBreak) 
                {
                    break;
                }
			}

            httpClient.Dispose();
		}

        async Task ChangeFeedResponseHandler(HttpResponseMessage response)
        {
            var status = response.StatusCode;
            if ((Int32)status >= 300 && !Utils.IsTransientError(status))
            {
                var msg = String.Format("Change tracker got error: {0}", status);
                Log.E(Tag, msg);
                Error = new CouchbaseLiteException (msg, new Status (status.GetStatusCode ()));
                Stop();
            }
                
            switch (mode)
            {
                case ChangeTrackerMode.Continuous:
                    {
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var contentReader = new StreamReader(contentStream))
                        {

                            while (!tokenSource.IsCancellationRequested && !contentReader.EndOfStream)
                            {
                                var change = await contentReader.ReadLineAsync();
                                Log.V(Tag, "Read longpolling change: {0}".Fmt(change));
                                var fullBody = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(change);
                                var responseOK = ReceivedChange(fullBody);
                            }
                        }
                    }
                    break;
                case ChangeTrackerMode.LongPoll:
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();
                        var fullBody = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(content.AsEnumerable());
                        var responseOK = ReceivedPollResponse(fullBody);
                        if (responseOK)
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
                    break;
                default:
                    {
                        var content = response.Content.ReadAsByteArrayAsync().Result;
                        var results = Manager.GetObjectMapper().ReadValue<IDictionary<String, Object>>(content.AsEnumerable());
                        var resultsValue = results["results"] as JArray;
                        foreach (var item in resultsValue)
                        {
                            IDictionary<String, Object> change = null;
                            try
                            {
                                change = item.ToObject<IDictionary<String, Object>>();
                            }
                            catch (Exception)
                            {
                                Log.E(Tag, this + string.Format(": Received unparseable change line from server: {0}", change));
                            }
                            if (!ReceivedChange(change))
                            {
                                Log.W(Tag, this + string.Format(": Received unparseable change line from server: {0}", change));
                            }
                        }
                    
                        // As ReceivedChange() dispatches the change event to its client via WorkExecutor,
                        // to avoid the Stop() to be called before the client is done handling the change event,    
                        // we need to setup the Stop() call with the WorkExecutor as well 
                        // (Assuming that the WorkExecutor is a single thread executor).

                        WorkExecutor.StartNew(() => Stop());

                        this.shouldBreak = true;

                        return;
                    }
            }

            backoff.ResetBackoff();
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
                WorkExecutor.StartNew(()=> 
                    client.ChangeTrackerReceivedChange(change), tokenSource.Token);
			}
			lastSequenceID = seq;
			return true;
		}

        public bool ReceivedPollResponse(IDictionary<string, object> response)
        {
            var changes = ((JArray)response.Get("results")).ToList();
            if (changes == null)
            {
                return false;
            }
            foreach (var change in changes)
            {
                var changeDict = change.ToObject<IDictionary<string, object>>();
                if (! ReceivedChange(changeDict))
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
            runTask = Task.Factory.StartNew(Run, tokenSource.Token);
            return true;
		}

		public void Stop()
		{
            // Lock to prevent multiple calls to Stop() method from different
            // threads (eg. one from ChangeTracker itself and one from any other
            // consumers).
            lock(runningMutex)
            {
                if (!IsRunning())
                {
                    return;
                }

                Log.D(Tag, this + ": changed tracker asked to stop");

                running = false;

                if (Request != null)
                {
                    tokenSource.Cancel();
                }

                Stopped();
            }
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

		public Exception GetLastError()
		{
			return Error;
		}

        public void SetDocIDs(IList<string> docIDs)
        {
            this.docIDs = docIDs;
        }

		public bool IsRunning()
		{
            return running;
		}

        internal void SetRequestHeaders(IDictionary<String, Object> requestHeaders)
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
	}

}
