//
//  IQueryStore.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite.Store
{

    /// <summary>
    /// Storage for a QueryRow. Instantiated by a IViewStore when it creates a QueryRow.
    /// </summary>
    public interface IQueryRowStore
    {

        /// <summary>
        /// Given the raw data of a row's value, returns <c>true</c> if this is a non-JSON placeholder representing
        /// the entire document. If so, the QueryRow will not parse this data but will instead fetch the
        /// document's body from the database and use that as its value.
        /// </summary>
        bool RowValueIsEntireDoc(object valueData);

        /// <summary>
        /// Parses a "normal" (not entire-doc) row value into a JSON-compatible object.
        /// </summary>
        T ParseRowValue<T>(IEnumerable<byte> valueData);

        /// <summary>
        /// Fetches a document's body; called when the row value represents the entire document.
        /// </summary>
        /// <returns>The document properties, or nil on error</returns>
        /// <param name="docId">The document ID</param>
        /// <param name="sequenceNumber">The sequence representing this revision</param>
        IDictionary<string, object> DocumentProperties(string docId, long sequenceNumber);

        //TODO: full text
        /*/// <summary>
        /// Fetches the full text that was emitted for the given document.
        /// </summary>
        /// <returns>The full text as UTF-8 data, or null on error.</returns>
        /// <param name="docId">The document ID</param>
        /// <param name="sequenceNumber">The sequence representing this revision</param>
        /// <param name="fullTextID">The opaque ID given when the QueryRow was created; this is used to
        /// disambiguate between multiple calls to emit() made for a single document.</param>
        byte[] FullTextForDocument(string docId, Int64 sequenceNumber, UInt64 fullTextID);*/

    }
}


