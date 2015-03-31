//
//  HttpListenerContextExtensions.cs
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
using System.Net;

namespace Couchbase.Lite.PeerToPeer
{
    internal static class HttpListenerContextExtensions
    {
        public static DocumentContentOptions GetContentOptions(this HttpListenerContext context)
        {
            var queryStr = context.Request.QueryString;
            DocumentContentOptions options = DocumentContentOptions.None;
            if (queryStr.Get<bool>("attachments", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeAttachments;
            }

            if (queryStr.Get<bool>("local_seq", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeLocalSeq;
            }

            if (queryStr.Get<bool>("conflicts", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeConflicts;
            }

            if (queryStr.Get<bool>("revs", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeRevs;
            }

            if (queryStr.Get<bool>("revs_info", bool.TryParse, false)) {
                options |= DocumentContentOptions.IncludeRevsInfo;
            }

            return options;
        }

        public static bool ExplicitlyAcceptsType(this HttpListenerContext context, string type)
        {
            string accept = context.Request.Headers["Accept"];
            return accept.Contains(type);
        }

        public static bool CacheWithEtag(this HttpListenerContext context, string etag, CouchbaseLiteResponse response)
        {
            etag = String.Format("\"{0}\"", etag);
            response["Etag"] = etag;
            return etag.Equals(context.Request.Headers.Get("If-None-Match"));
        }
    }
}

