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
    internal static class ViewMethods
    {
        public static ICouchbaseResponseState GetDesignView(ICouchbaseListenerContext context)
        {
            return QueryDesignDocument(context, null).AsDefaultState();
        }

        private static CouchbaseLiteResponse QueryDesignDocument(ICouchbaseListenerContext context, IList<object> keys)
        {
            return DatabaseMethods.PerformLogicWithDatabase(context, true, db =>
            {
                var view = db.GetView(String.Format("{0}/{1}", context.DesignDocName, context.ViewName));
                var status = view.CompileFromDesignDoc();
                if(status.IsError) {
                    return new CouchbaseLiteResponse(context) { InternalStatus = status.GetCode() };
                }

                var options = context.QueryOptions;
                if(options == null) {
                    return new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.BadRequest };
                }

                if(keys != null) {
                    options.SetKeys(keys);
                }

                if(options.GetStale() == IndexUpdateMode.Before || view.LastSequenceIndexed <= 0) {
                    view.UpdateIndex();
                } else if(options.GetStale() == IndexUpdateMode.After && view.LastSequenceIndexed < db.LastSequenceNumber) {
                    db.RunAsync(_ => view.UpdateIndex());
                }

                // Check for conditional GET and set response Etag header:
                if(keys == null) {
                    long eTag = options.IsIncludeDocs() ? db.LastSequenceNumber : view.LastSequenceIndexed;
                    if(context.CacheWithEtag(eTag.ToString())) {
                        return new CouchbaseLiteResponse(context) { InternalStatus = StatusCode.NotModified };
                    }
                }

                return DatabaseMethods.QueryView(context, view, options);
            });
        }
    }
}

