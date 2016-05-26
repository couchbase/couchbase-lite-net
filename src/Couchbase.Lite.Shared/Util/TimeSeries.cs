//
// TimeSeries.cs
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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using Couchbase.Lite.Internal;

#if !NET_3_5
using System.IO.MemoryMappedFiles;
#endif

namespace Couchbase.Lite.Util
{
    public class TimeSeries : IDisposable
    {
        private static readonly string Tag = typeof(TimeSeries).Name;
        private const int MaxDocSize = 100 * 1024; // bytes
        private const int MaxDocEventCount = 1000;

        private TaskFactory _scheduler = new TaskFactory(new SingleTaskThreadpoolScheduler());
        private Database _db;
        private FileStream _out;
        private Exception _error;
        private uint _eventsInFile;
        private string _docType;
        private string _path;
        private ConcurrentQueue<IDictionary<string, object>> _docsToAdd;

        public TimeSeries(Database db, string docType)
        {
            if(db == null) {
                Log.To.NoDomain.E(Tag, "db cannot be null in ctor, throwing...");
                throw new ArgumentNullException("db");
            }

            if(docType == null) {
                Log.To.NoDomain.E(Tag, "docType cannot be null in ctor, throwing...");
                throw new ArgumentNullException("docType");
            }

            var filename = String.Format("TS-{0}.tslog", docType);
            _path = Path.Combine(db.DbDirectory, filename);
            try {
                _out = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            } catch(Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.NoDomain, e, Tag, "Failed to open {0} for write",
                    _path);
            }

            _db = db;
            _docType = docType;
        }

        public void AddEvent(IDictionary<string, object> eventToAdd)
        {
            AddEvent(eventToAdd, DateTime.Now);
        }

        public void AddEvent(IDictionary<string, object> eventToAdd, DateTime time)
        {
            if(eventToAdd == null) {
                Log.To.NoDomain.E(Tag, "eventToAdd cannot be null in AddEvent, throwing...");
                throw new ArgumentNullException("eventToAdd");
            }

            var props = new Dictionary<string, object>(eventToAdd);
            if(_scheduler == null) {
                return;
            }

            _scheduler.StartNew(() =>
            {
                props["t"] = (ulong)time.TimeSinceEpoch().TotalMilliseconds;
                var json = default(byte[]);
                try {
                    json = Manager.GetObjectMapper().WriteValueAsBytes(props).ToArray();
                } catch(Exception e) {
                    Log.To.NoDomain.W(Tag, "Got exception trying to serialize json, aborting save...", e);
                    return;
                }

                var pos = _out.Position;
                if(pos + json.Length + 20 > MaxDocSize || _eventsInFile >= MaxDocEventCount) {
                    TransferToDB();
                    pos = 0;
                }

                try {
                    if(pos == 0) {
                        _out.WriteByte((byte)'[');
                    } else {
                        _out.WriteByte((byte)',');
                        _out.WriteByte((byte)'\n');
                    }

                    _out.Write(json, 0, json.Length);
                    _out.Flush();
                } catch(Exception e) {
                    Log.To.NoDomain.W(Tag, "Error while writing event to file, recording...");
                    _error = e;
                }

                ++_eventsInFile;
            });
        }

        public Task FlushAsync()
        {
            if(_scheduler == null) {
                return Task.FromResult(false);
            }

            return _scheduler.StartNew(() =>
            {
                if(_eventsInFile > 0 || _out.Position > 0) {
                    TransferToDB();
                }
            });
        }

        public void Flush()
        {
            FlushAsync().Wait();
        }

        public Replication CreatePushReplication(Uri remoteUrl, bool purgeWhenPushed)
        {
            var push = _db.CreatePushReplication(remoteUrl);
            _db.SetFilter("com.couchbase.DocIDPrefix", (rev, props) =>
                rev.Document.Id.StartsWith(props.GetCast<string>("prefix")));

            push.Filter = "com.couchbase.DocIDPrefix";
            push.FilterParams = new Dictionary<string, object> {
                { "prefix", String.Format("TS-{0}-", _docType) }
            };

            push.ReplicationOptions.AllNew = true;
            push.ReplicationOptions.PurgePushed = purgeWhenPushed;

            return push;
        }

        public IEnumerable<IDictionary<string, object>> GetEventsInRange(DateTime start, DateTime end)
        {
            // Get the first series from the doc containing start (if any):
            IList<IDictionary<string, object>> curSeries = null;
            var curStamp = 0UL;
            if(start > DateTime.MinValue) {
                curSeries = GetEvents(start, ref curStamp);
            }

            // Start forwards query if I haven't already:
            var q = _db.CreateAllDocumentsQuery();
            ulong startStamp;
            if(curSeries != null && curSeries.Count > 0) {
                startStamp = curStamp;
                foreach(var gotEvent in curSeries) {
                    startStamp += gotEvent.GetCast<ulong>("dt");
                }

                q.InclusiveStart = false;
            } else {
                startStamp = start > DateTime.MinValue ? (ulong)start.TimeSinceEpoch().TotalMilliseconds : 0UL;
            }

            var endStamp = end < DateTime.MaxValue ? (ulong)end.TimeSinceEpoch().TotalMilliseconds : UInt64.MaxValue;

            var e = default(QueryEnumerator);
            if(startStamp < endStamp) {
                q.StartKey = MakeDocID(startStamp);
                q.EndKey = MakeDocID(endStamp);
                e = q.Run();
            }

            // OK, here is the block for the enumerator:
            var curIndex = 0;
            while(true) {
                while(curIndex >= (curSeries == null ? 0 : curSeries.Count)) {
                    if(e == null) {
                        yield break;
                    }

                    if(!e.MoveNext()) {
                        e.Dispose();
                        yield break;
                    }

                    curSeries = e.Current.Document.GetProperty("events").AsList<IDictionary<string, object>>();
                    curIndex = 0;
                    curStamp = Convert.ToUInt64(e.Current.Document.GetProperty("t0"));
                }

                // Return the next event from curSeries
                var gotEvent = curSeries[curIndex++];
                curStamp += gotEvent.GetCast<ulong>("dt");
                if(curStamp > endStamp) {
                    if(e != null) {
                        e.Dispose();
                    }

                    yield break;
                }

                gotEvent["t"] = Misc.OffsetFromEpoch(TimeSpan.FromMilliseconds(curStamp));
                gotEvent.Remove("dt");
                yield return gotEvent;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposing) {
                Stop();
            }
        }

        private IList<IDictionary<string, object>> GetEvents(DateTime t, ref ulong startStamp)
        {
            var q = _db.CreateAllDocumentsQuery();
            var timestamp = t > DateTime.MinValue ? (ulong)t.TimeSinceEpoch().TotalMilliseconds : 0;
            q.StartKey = MakeDocID(timestamp);
            q.Descending = true;
            q.Limit = 1;
            q.Prefetch = true;
            var row = q.Run().FirstOrDefault();

            if(row == null) {
                return new List<IDictionary<string, object>>();
            }

            var events = row.DocumentProperties.Get("events").AsList<IDictionary<string, object>>();
            var skip = 0;
            var ts = Convert.ToUInt64(row.Document.GetProperty("t0"));
            foreach(var gotEvent in events) {
                var prevTs = ts;
                ts += gotEvent.GetCast<ulong>("dt");
                if(ts >= timestamp) {
                    startStamp = prevTs;
                    break;
                }

                skip++;
            }

            return events.Skip(skip).ToList();
        }

        private void TransferToDB()
        {
            try {
                _out.WriteByte((byte)']');
                _out.Flush();
                _out.Dispose();
            } catch(Exception e) {
                Log.To.NoDomain.W(Tag, "Error writing final byte to file, recording...", e);
                _error = e;
                return;
            }

            // Parse a JSON array from the file:
            var stream = default(Stream);
#if !NET_3_5
            var mapped = default(MemoryMappedFile);
            try {
                mapped = MemoryMappedFile.CreateFromFile(_path, FileMode.Open, "TimeSeries", 0, MemoryMappedFileAccess.Read);
                stream = mapped.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            } catch(Exception e) {
                Log.To.NoDomain.W(Tag, "Error creating or reading memory mapped file, recording...", e);
                _error = e;
                return;
            }
#else
            stream = File.OpenRead(_path);
#endif

            var events = default(IList<object>);
            try {
                events = Manager.GetObjectMapper().ReadValue<IList<object>>(stream);
            } catch(Exception e) {
                Log.To.NoDomain.W(Tag, "Error reading json from file, recording...", e);
                _error = e;
                return;
            } finally {
                stream.Dispose();
#if !NET_3_5
                mapped.Dispose();
#endif
            }

            // Add the events to documents in batches:
            var count = events.Count;
            for(var pos = 0; pos < count; pos += MaxDocEventCount) {
                var group = events.Skip(pos).Take(MaxDocEventCount).ToList();
                AddEventsToDB(group);
            }

            // Now erase the file for subsequent events:
            _out.Dispose();
            _out = File.Open(_path, FileMode.Truncate, FileAccess.Write);
            _eventsInFile = 0;

        }

        private void SaveQueuedDocs()
        {
            var docsToAdd = Interlocked.Exchange<ConcurrentQueue<IDictionary<string, object>>>(ref _docsToAdd, null);
            if(docsToAdd != null && docsToAdd.Count > 0) {
                _db.RunInTransaction(() =>
                {
                    IDictionary<string, object> next = null;
                    while(docsToAdd.TryDequeue(out next)) {
                        var docID = next.CblID();
                        try {
                            _db.GetDocument(docID).PutProperties(next);
                        } catch(Exception e) {
                            Log.To.NoDomain.W(Tag, String.Format("Couldn't save events to '{0}', recording...",
                                docID), e);
                            _error = e;
                        }
                    }

                    return true;
                });
            }
        }

        private void AddEventsToDB(IList<object> events)
        {
            if(events.Count == 0) {
                return;
            }

            var convertedEvents = new List<Dictionary<string, object>>();
            JsonUtility.PopulateNetObject(events, convertedEvents);
            var nextEvent = convertedEvents[0];
            if(nextEvent == null) {
                Log.To.NoDomain.W(Tag, "Invalid object found in log events, aborting...");
                return;
            }

            var t0 = nextEvent.GetCast<ulong>("t");
            var docID = MakeDocID(t0);

            // Convert all timestamps to relative:
            var t = t0;
            foreach(var storedEvent in convertedEvents) {
                var tnew = storedEvent.GetCast<ulong>("t");
                if(tnew > t) {
                    storedEvent["dt"] = tnew - t;
                }

                storedEvent.Remove("t");
                t = tnew;
            }

            var doc = new Dictionary<string, object> {
                { "_id", docID },
                { "type", _docType },
                { "t0", t0 },
                { "events", convertedEvents }
            };

            bool firstDoc = false;
            if(Interlocked.CompareExchange<ConcurrentQueue<IDictionary<string, object>>>(ref _docsToAdd,
                   new ConcurrentQueue<IDictionary<string, object>>(), null) == null) {
                firstDoc = true;
            }

            _docsToAdd.Enqueue(doc);
            if(firstDoc) {
                _db.RunAsync(d => SaveQueuedDocs());
            }

        }

        private string MakeDocID(ulong timestamp)
        {
            return String.Format("TS-{0}-{1:D8}", _docType, timestamp);
        }

        protected void Stop()
        {
            if(_scheduler != null) {
                _scheduler.StartNew(() =>
                {
                    if(_out != null) {
                        _out.Dispose();
                        _out = null;
                    }
                });

                _scheduler = null;
            }

            _error = null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(false);
        }
    }
}
