//
// OneShotOrLongPollLogic.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.IO;
using Couchbase.Lite.Util;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Lite.Internal
{
    internal sealed class OneShotOrLongPollLogic : IChangeTrackerResponseLogic
    {
        private static readonly string Tag = typeof(OneShotOrLongPollLogic).Name;
        private bool _caughtUp = false;
        private DateTime _startTime;
        private ManualResetEventSlim _pauseWait = new ManualResetEventSlim(true);

        public OneShotOrLongPollLogic()
        {
            _startTime = DateTime.Now;
        }

        public TimeSpan Heartbeat { get; set; }

        public Action OnCaughtUp { get; set; }

        public Action<IDictionary<string, object>> OnChangeFound { get; set; }

        public Action<Exception> OnFinished { get; set; }

        private bool ReceivedPollResponse(IJsonSerializer jsonReader, CancellationToken token, ref bool timedOut)
        {
            bool started = false;
            var start = DateTime.Now;
            try {
                while (jsonReader.Read() && !token.IsCancellationRequested) {
                    _pauseWait.Wait();
                    if (jsonReader.CurrentToken == JsonToken.StartArray) {
                        timedOut = true;
                        started = true;
                    } else if (jsonReader.CurrentToken == JsonToken.EndArray) {
                        started = false;
                    } else if (started) {
                        IDictionary<string, object> change;
                        try {
                            change = jsonReader.DeserializeNextObject<IDictionary<string, object>>();
                        } catch(Exception e) {
                            var ex = e as CouchbaseLiteException;
                            if (ex == null || ex.Code != StatusCode.BadJson) {
                                Log.To.ChangeTracker.W(Tag, "Failure during change tracker JSON parsing", e);
                                throw;
                            }

                            return false;
                        }

                        if(OnChangeFound != null) {
                            OnChangeFound(change);
                        }

                        timedOut = false;
                    }
                }
            } catch (CouchbaseLiteException e) {
                var elapsed = DateTime.Now - start;
                timedOut = timedOut && elapsed.TotalSeconds >= 30;
                if (e.CBLStatus.Code == StatusCode.BadJson && timedOut) {
                    return false;
                }

                throw;
            }

            return true;
        }

        #region IChangeTrackerResponseLogic

        public ChangeTrackerResponseCode ProcessResponseStream(Stream stream, CancellationToken token)
        {
            Log.To.ChangeTracker.D(Tag, "Got stream from change tracker response");
            bool beforeFirstItem = true;
            bool responseOK = false;
            using (var jsonReader = Manager.GetObjectMapper().StartIncrementalParse(stream)) {
                responseOK = ReceivedPollResponse(jsonReader, token, ref beforeFirstItem);
            }

            Log.To.ChangeTracker.V(Tag, "{0} Finished reading stream", this);

            if (responseOK) {
                var response = ChangeTrackerResponseCode.Normal;
                if (!_caughtUp) {
                    _caughtUp = true;
                    if (OnCaughtUp != null) {
                        OnCaughtUp();
                    }
                }

                if (OnFinished != null) {
                    OnFinished(null);
                }

                return response;
            } else {
                if (beforeFirstItem) {
                    var elapsed = DateTime.Now - _startTime;
                    Log.To.ChangeTracker.W(Tag, "{0} longpoll connection closed (by proxy?) after {0} sec", 
                        this, elapsed.TotalSeconds);

                    // Looks like the connection got closed by a proxy (like AWS' load balancer) while the
                    // server was waiting for a change to send, due to lack of activity.
                    // Lower the heartbeat time to work around this, and reconnect:
                    long newTicks = (long)(elapsed.Ticks * 0.75);
                    Heartbeat = new TimeSpan(newTicks);
                    return ChangeTrackerResponseCode.ChangeHeartbeat;
                } else {
                    Log.To.ChangeTracker.W(Tag, "{0} Received improper _changes feed response", this);
                    if (OnFinished != null) {
                        OnFinished(new CouchbaseLiteException(StatusCode.BadJson));
                    }

                    return ChangeTrackerResponseCode.Failed;
                }
            }
        }

        public void Pause()
        {
            _pauseWait.Reset();
        }

        public void Resume()
        {
            _pauseWait.Set();
        }

        #endregion

        public void Dispose()
        {
            _pauseWait.Dispose();
        }
        
    }
}

