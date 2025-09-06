// 
//  QueryBase.cs
// 
//  Copyright (c) 2021 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Dispatch;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query;

internal abstract class QueryBase : IQuery, IStoppable
{
    private const string Tag = nameof(QueryBase);

    protected readonly DisposalWatchdog DisposalWatchdog = new DisposalWatchdog(nameof(IQuery));
    protected C4QueryWrapper? _c4Query;
    private Parameters _queryParameters;
    private readonly Dictionary<ListenerToken, LiveQuerier?> _listenerTokens = new();
    private int _observingCount;

    public Database? Database { get; protected set; }

    public Collection? Collection { get; protected set; }

    public Parameters Parameters
    {
        get => _queryParameters;
        set
        {
            _queryParameters = value.Freeze();
            SetParameters(_queryParameters.ToString());
        }
    }

    internal ThreadSafety ThreadSafety { get; set; } = new ThreadSafety();

    internal SerialQueue DispatchQueue { get; } = new SerialQueue();

    internal Dictionary<string, int> ColumnNames
    {
        get {
            if(_c4Query == null) {
                throw new ObjectDisposedException(nameof(QueryBase));
            }

            return CreateColumnNames(_c4Query);
        }
    }

    protected QueryBase()
    {
        _queryParameters = new Parameters(this);
    }

    ~QueryBase()
    {
        Dispose(true);
    }

    public void Stop()
    {
        if (_observingCount == 0) {
            return;
        }
        
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        Collection?.Database?.RemoveActiveStoppable(this);
        foreach (var t in _listenerTokens) {
            var token = t.Key;
            var querier = t.Value;
            querier?.StopObserver(token);
            querier?.Dispose();
        }

        _listenerTokens.Clear();
    }

    internal void SetParameters(string parameters)
    {
        CreateQuery();
        if (_c4Query != null && !String.IsNullOrEmpty(parameters)) {
            NativeSafe.c4query_setParameters(_c4Query, parameters);
        }
    }

    public void Dispose()
    {
        Dispose(false);
    }

    private void Dispose(bool finalizing)
    {
        if (!finalizing) {
            Stop();
            using var threadSafetyScope = ThreadSafety.BeginLockedScope();
            _c4Query?.Dispose();
            _c4Query = null;
            DisposalWatchdog.Dispose();
        }
    }

    public abstract IResultSet Execute();

    public abstract string Explain();

    public ListenerToken AddChangeListener(TaskScheduler? scheduler, EventHandler<QueryChangedEventArgs> handler)
    {
        CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(handler), handler);
        DisposalWatchdog.CheckDisposed();

        if (Interlocked.Increment(ref _observingCount) == 1) {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            Collection?.Database?.AddActiveStoppable(this);
        }

        var cbHandler = new CouchbaseEventHandler<QueryChangedEventArgs>(handler, scheduler);
        var listenerToken = CreateLiveQuerier(cbHandler);

        return listenerToken;
    }

    public ListenerToken AddChangeListener(EventHandler<QueryChangedEventArgs> handler) => AddChangeListener(null, handler);

    public void RemoveChangeListener(ListenerToken token)
    {
        DisposalWatchdog.CheckDisposed();
        _listenerTokens[token]?.StopObserver(token);
        _listenerTokens.Remove(token);
        if (Interlocked.Decrement(ref _observingCount) == 0) {
            Stop();
        }
    }

    protected abstract void CreateQuery();

    protected abstract Dictionary<string, int> CreateColumnNames(C4QueryWrapper query);

    private ListenerToken CreateLiveQuerier(CouchbaseEventHandler<QueryChangedEventArgs> cbEventHandler)
    {
        CreateQuery();
        LiveQuerier? liveQuerier = null;
        if (_c4Query != null) {
            liveQuerier = new LiveQuerier(this);
            liveQuerier.CreateLiveQuerier(_c4Query);
        }
            
        liveQuerier?.StartObserver(cbEventHandler);
        var token = new ListenerToken(cbEventHandler, ListenerTokenType.Query, this);
        _listenerTokens.Add(token, liveQuerier);
        return token;
    }
}