//
//  DocumentChangedEventArgs.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// The arguments for the <see cref="Database.AddDocumentChangeListener(string, EventHandler{DocumentChangedEventArgs})"/> 
    /// event
    /// </summary>
    public sealed class DocumentChangedEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// The ID of the document that changed
        /// </summary>
        [NotNull]
        public string DocumentID { get; }

        /// <summary>
        /// The source of the document that changed
        /// </summary>
        [NotNull]
        public Database Database { get; }

        #endregion

        #region Constructors

        internal DocumentChangedEventArgs([NotNull]string documentID, [NotNull]Database database)
        {
            DocumentID = documentID;
            Database = database;
        }

        #endregion
    }
}
