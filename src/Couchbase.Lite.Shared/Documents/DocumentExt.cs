//
// DocumentExt.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using Couchbase.Lite.Revisions;
using System.Collections.Generic;

namespace Couchbase.Lite.Internal
{
    internal static class DocumentExt
    {
        internal static string CblID(this IDictionary<string, object> dict)
        {
            return dict?.GetCast<string>("_id");
        }

        internal static RevisionID CblRev(this IDictionary<string, object> dict)
        {
            return dict?.GetCast<string>("_rev").AsRevID();
        }

        internal static bool CblDeleted(this IDictionary<string, object> dict)
        {
            return dict == null ? false : dict.GetCast<bool>("_deleted");
        }

        internal static IDictionary<string, object> CblAttachments(this IDictionary<string, object> dict)
        {
            return dict?.Get("_attachments").AsDictionary<string, object>();
        }

        internal static void SetDocRevID(this IDictionary<string, object> dict, string docId, 
            RevisionID revId)
        {
            if(dict == null) {
                return;
            }

            dict["_id"] = docId;
            dict["_rev"] = revId.ToString();
        }

        internal static void SetDocRevID(this IDictionary<string, object> dict, string docId,
            string revId)
        {
            if(dict == null) {
                return;
            }

            dict["_id"] = docId;
            dict["_rev"] = revId;
        }

        internal static void SetRevID(this IDictionary<string, object> dict, RevisionID revId)
        {
            if(dict == null) {
                return;
            }

            dict["_rev"] = revId.ToString();
        }

        internal static void SetRevID(this IDictionary<string, object> dict, string revId)
        {
            if(dict == null) {
                return;
            }

            dict["_rev"] = revId;
        }
    }
}
