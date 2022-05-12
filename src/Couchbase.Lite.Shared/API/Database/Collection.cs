// 
//  Collection.cs
// 
//  Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Support;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class Collection : ICollection, IDisposable, IChangeObservable<DatabaseChangedEventArgs>,
        IDocumentChangeObservable
    {
        #region Properties

        [NotNull] //TODO: expose Database.ThreadSafety and assign here
        internal ThreadSafety ThreadSafety { get; }

        public string Name => throw new NotImplementedException();

        public IScope Scope => throw new NotImplementedException();

        public ulong Count => throw new NotImplementedException();

        #endregion

        #region IChangeObservable

        public ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDocumentChangeObservable

        public ListenerToken AddDocumentChangeListener([NotNull] string id, [CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DocumentChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddDocumentChangeListener([NotNull] string id, [NotNull] EventHandler<DocumentChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IChangeObservableRemovable

        public void RemoveChangeListener(ListenerToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ICollection

        public void CreateIndex([NotNull] string name, [NotNull] IndexConfiguration indexConfig)
        {
            throw new NotImplementedException();
        }

        public void Delete([NotNull] Document document)
        {
            throw new NotImplementedException();
        }

        public bool Delete([NotNull] Document document, ConcurrencyControl concurrencyControl)
        {
            throw new NotImplementedException();
        }

        public void DeleteIndex([NotNull] string name)
        {
            throw new NotImplementedException();
        }

        public Document GetDocument([NotNull] string id)
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset? GetDocumentExpiration(string docId)
        {
            throw new NotImplementedException();
        }

        public IList<string> GetIndexes()
        {
            throw new NotImplementedException();
        }

        public void Purge([NotNull] Document document)
        {
            throw new NotImplementedException();
        }

        public void Purge([NotNull] string docId)
        {
            throw new NotImplementedException();
        }

        public void Save([NotNull] MutableDocument document)
        {
            throw new NotImplementedException();
        }

        public bool Save([NotNull] MutableDocument document, ConcurrencyControl concurrencyControl)
        {
            throw new NotImplementedException();
        }

        public bool Save(MutableDocument document, Func<MutableDocument, Document, bool> conflictHandler)
        {
            throw new NotImplementedException();
        }

        public bool SetDocumentExpiration(string docId, DateTimeOffset? expiration)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDispose

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
