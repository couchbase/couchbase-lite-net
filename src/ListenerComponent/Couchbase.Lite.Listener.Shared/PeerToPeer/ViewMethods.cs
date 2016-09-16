//
//  ViewMethods.cs
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

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// Methods for querying and returning formatted output from database
    /// </summary>
    internal static class ViewMethods
    {

        #region Public Methods

        /// <summary>
        /// Executes the specified view function from the specified design document.
        /// </summary>
        /// <returns>The response state for further HTTP processing</returns>
        /// <param name="context">The context of the Couchbase Lite HTTP request</param>
        /// <remarks>
        /// http://docs.couchdb.org/en/latest/api/ddoc/views.html#get--db-_design-ddoc-_view-view
        /// </remarks>
        public static ICouchbaseResponseState GetDesignView(ICouchbaseListenerContext context)
        {
            return QueryDesignDocument(context, null).AsDefaultState();
        }

        #endregion

        #region Private Methods

        // Performs the actual query logic on a design document
        private static CouchbaseLiteResponse QueryDesignDocument(ICouchbaseListenerContext context, IList<object> keys)
        {
            return DatabaseMethods.PerformLogicWithDatabase(context, true, db =>
            {
                var view = db.GetView(String.Format("{0}/{1}", context.DesignDocName, context.ViewName));
                var status = view.CompileFromDesignDoc();
                if(status.IsError) {
                    return context.CreateResponse(status.Code);
                }

                var options = context.QueryOptions;
                if(options == null) {
                    return context.CreateResponse(StatusCode.BadRequest);
                }

                if(keys != null) {
                    options.Keys = keys;
                }

                if(options.Stale == IndexUpdateMode.Before || view.LastSequenceIndexed <= 0) {
                    view.UpdateIndex_Internal();
                } else if(options.Stale == IndexUpdateMode.After && view.LastSequenceIndexed < db.GetLastSequenceNumber()) {
                    db.RunAsync(_ => view.UpdateIndex_Internal());
                }

                // Check for conditional GET and set response Etag header:
                if(keys == null) {
                    long eTag = options.IncludeDocs ? db.GetLastSequenceNumber() : view.LastSequenceIndexed;
                    if(context.CacheWithEtag(eTag.ToString())) {
                        return context.CreateResponse(StatusCode.NotModified);
                    }
                }

                return DatabaseMethods.QueryView(context, db, view, options);
            });
        }

        #endregion
    }
}

