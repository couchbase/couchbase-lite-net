// 
//  FilteredEvent.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Dispatch;
    
#if NET9_0_OR_GREATER
using LockType = System.Threading.Lock;
#else
using LockType = System.Object;
#endif

namespace Couchbase.Lite.Util;

internal sealed class QueueTaskScheduler : TaskScheduler
{
    private readonly SerialQueue _queue = new();

    [ExcludeFromCodeCoverage]
    protected override IEnumerable<Task> GetScheduledTasks() => throw new NotSupportedException();

    protected override void QueueTask(Task task) => _queue.DispatchAsync(() => TryExecuteTask(task));

    [ExcludeFromCodeCoverage]
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
}

internal abstract class CouchbaseEventHandler
{
    public readonly Guid Id = Guid.NewGuid();

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (obj is not CouchbaseEventHandler other) {
            return false;
        }

        return Id == other.Id;
    }
}

internal class CouchbaseEventHandler<TEventType>(EventHandler<TEventType> handler, TaskScheduler? scheduler)
    : CouchbaseEventHandler where TEventType : EventArgs?
{
    private readonly EventHandler<TEventType> _handler = handler;
    private readonly TaskFactory _taskFactory = new(scheduler ?? new QueueTaskScheduler());

    public bool Equals(EventHandler<TEventType> handler) => handler == _handler;

    public void Fire(object sender, TEventType args) => _taskFactory.StartNew(() => _handler.Invoke(sender, args));

    public override int GetHashCode() => _handler.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (obj is not CouchbaseEventHandler<TEventType> other) {
            return false;
        }

        return _handler == other._handler;
    }
}

internal sealed class CouchbaseEventHandler<TFilterType, TEventType>(EventHandler<TEventType> handler, 
    TFilterType filter, TaskScheduler? scheduler)
    : CouchbaseEventHandler<TEventType>(handler, scheduler)
    where TEventType : EventArgs?
{
    public readonly TFilterType Filter = filter;
}

internal class Event<TEventType> where TEventType : EventArgs?
{
    private readonly List<CouchbaseEventHandler<TEventType>> _events = [];
    private readonly LockType _locker = new();

    internal int Counter
    {
        get {
            lock (_locker) {
                return _events.Count;
            }
        }
    }

    public int Add(CouchbaseEventHandler<TEventType> handler)
    {
        lock (_locker) {
            _events.Add(handler);
            return _events.Count - 1;
        }
    }

    public int Remove(ListenerToken token)
    {
        lock (_locker) {
            _events.Remove((CouchbaseEventHandler<TEventType>)token.EventHandler);
            return _events.Count;
        }
    }

    public int Remove(EventHandler<TEventType> handler)
    {
        lock (_locker) {
            var index = _events.FindIndex(x => x.Equals(handler));
            if (index != -1) {
                _events.RemoveAt(index);
            }

            return _events.Count;
        }
    }

    internal void Fire(object sender, TEventType args)
    {
        lock (_locker) {
            foreach (var ev in _events) {
                ev.Fire(sender, args);
            }
        }
    }
}

internal sealed class FilteredEvent<TFilterType, TEventType> where TEventType : EventArgs where TFilterType : notnull
{
    private readonly ConcurrentDictionary<TFilterType, HashSet<CouchbaseEventHandler<TEventType>>> _eventMap = new();

    private readonly LockType _locker = new();

    public int Add(CouchbaseEventHandler<TFilterType, TEventType> handler)
    {
        var collection = _eventMap.GetOrAdd(handler.Filter, []);

        lock (_locker) {
            collection.Add(handler);
            return collection.Count - 1;
        }
    }

    public int Remove(ListenerToken token, out TFilterType? filter)
    {
        filter = default;
        if (token.EventHandler is not CouchbaseEventHandler<TFilterType, TEventType> handler) {
            return -1;
        }

        var collection = _eventMap.GetOrAdd(handler.Filter, []);

        lock (_locker) {
            collection.Remove(handler);
            filter = handler.Filter;
            return collection.Count;
        }
    }

    internal void Fire(TFilterType key, object sender, TEventType args)
    {
        var collection = _eventMap.GetOrAdd(key, []);

        lock (_locker) {
            foreach (var method in collection) {
                method.Fire(sender, args);
            }
        }
    }
}