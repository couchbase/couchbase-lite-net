//
//  IDatabase.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using System.Collections;
using System.Collections.Generic;
using Couchbase.Lite.Query;

namespace Couchbase.Lite
{
    /// <summary>
    /// An interface describing a Couchbase Lite database
    /// </summary>
    public interface IDatabase : IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets or sets the conflict resolver to use when conflicts arise
        /// </summary>
        IConflictResolver ConflictResolver { get; set; }

        /// <summary>
        /// Bracket operator for retrieving <see cref="IDocument"/>s
        /// </summary>
        /// <param name="id">The ID of the <see cref="IDocument"/> to retrieve</param>
        /// <returns>The instantiated <see cref="IDocument"/></returns>
        IDocument this[string id] { get; }

        /// <summary>
        /// Gets the name of the database
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the options that were used to create the database
        /// </summary>
        DatabaseConfiguration Config { get; }

        /// <summary>
        /// Gets the path on disk where the database exists
        /// </summary>
        string Path { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// An event fired whenever the database changes
        /// </summary>
        event EventHandler<DatabaseChangedEventArgs> Changed;

        event EventHandler<DocumentChangedEventArgs> DocumentChanged;

        /// <summary>
        /// Closes the database
        /// </summary>
        void Close();

        /// <summary>
        /// Creates an <see cref="IndexType.ValueIndex"/> index on the given path
        /// </summary>
        /// <param name="expressions">The expressions to create the index on</param>
        void CreateIndex(IList<IExpression> expressions);

        /// <summary>
        /// Creates an index of the given type on the given path with the given options
        /// </summary>
        /// <param name="expressions">The expressions to create the index on (must be either string
        /// or IExpression)</param>
        /// <param name="indexType">The type of index to create</param>
        /// <param name="options">The options to apply to the index</param>
        void CreateIndex(IList expressions, IndexType indexType, IndexOptions options);

        /// <summary>
        /// Deletes the database
        /// </summary>
        void Delete();
        
        void Delete(IDocument document);
        
        void Save(IDocument document);
        
        bool Purge(IDocument document);

        /// <summary>
        /// Deletes an index of the given <see cref="IndexType"/> on the given propertyPath
        /// </summary>
        /// <param name="propertyPath">The path of the index to delete</param>
        /// <param name="type">The type of the index to delete</param>
        void DeleteIndex(string propertyPath, IndexType type);

        /// <summary>
        /// Gets or creates an <see cref="IDocument"/> with the specified ID
        /// </summary>
        /// <param name="id">The ID to use when creating or getting the document</param>
        /// <returns>The instantiated </returns>
        IDocument GetDocument(string id);

        /// <summary>
        /// Runs the given batch of operations as an atomic unit
        /// </summary>
        /// <param name="a">The <see cref="Func{T}"/> of <see cref="Boolean"/> containing the operations.  
        /// This function should return <c>true</c>
        /// on success and <c>false</c> on failure which will cause all the operations to be abandoned.</param>
        /// <returns>The return value of <c>a</c></returns>
        void InBatch(Action a);

        #endregion
    }
}
