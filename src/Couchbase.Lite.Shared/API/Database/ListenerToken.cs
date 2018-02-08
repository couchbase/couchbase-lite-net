// 
//  ListenerToken.cs
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

using Couchbase.Lite.Util;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// A token that stores information about an event handler that
    /// is registered on a Couchbase Lite object (for example
    /// <see cref="Database.AddChangeListener(System.EventHandler{DatabaseChangedEventArgs})"/>)
    /// </summary>
    public struct ListenerToken
    {
        #region Variables

        [NotNull]
        internal readonly CouchbaseEventHandler EventHandler;

        [NotNull]
        internal readonly string Type;

        #endregion

        #region Constructors

        internal ListenerToken([NotNull]CouchbaseEventHandler handler,  [NotNull]string type)
        {
            EventHandler = handler;
            Type = type;
        }

        #endregion
    }
}