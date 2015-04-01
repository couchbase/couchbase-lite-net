//
//  DocumentMethods.cs
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
using Couchbase.Lite.Internal;
using System.Linq;
using System.Collections.Generic;

namespace Couchbase.Lite.PeerToPeer
{
    internal static class DocumentMethods
    {
        private const string TAG = "DocumentMethods";

        public static ICouchbaseResponseState GetDocument(HttpListenerContext context)
        {
            return DatabaseMethods.PerformLogicWithDatabase(context, true, db => {
                var response = new CouchbaseLiteResponse(context);
                // http://wiki.apache.org/couchdb/HTTP_Document_API#GET
                string[] splitUrl = context.Request.Url.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string docId = splitUrl[1];
                bool isLocalDoc = false;
                if (docId.Equals("_local")) {
                    isLocalDoc = true;
                    docId = splitUrl[2];
                }

                DocumentContentOptions options = context.GetContentOptions();
                string openRevsParam = context.Request.QueryString["open_revs"];
                bool mustSendJson = context.ExplicitlyAcceptsType("application/json");
                if (openRevsParam == null || isLocalDoc) {
                    //Regular GET:
                    string revId = context.Request.QueryString["rev"]; //often null
                    RevisionInternal rev;
                    bool includeAttachments = false, sendMultipart = false;
                    if (isLocalDoc) {
                        rev = db.GetLocalDocument(docId, revId);
                    } else {
                        includeAttachments = options.HasFlag(DocumentContentOptions.IncludeAttachments);
                        if(includeAttachments) {
                            sendMultipart = !mustSendJson;
                            options &= ~DocumentContentOptions.IncludeAttachments;
                        }

                        Status status = new Status();
                        rev = db.GetDocumentWithIDAndRev(docId, revId, options, status);
                        if(rev != null) {
                            rev = ApplyOptions(options, rev, context, db, status);
                        }

                        if(rev == null) {
                            if(status.GetCode() == StatusCode.Deleted) {
                                response.StatusReason = "deleted";
                            } else {
                                response.StatusReason = "missing";
                            }

                            response.InternalStatus = status.GetCode();
                            return response;
                        }
                    }

                    if(rev == null) {
                        response.InternalStatus = StatusCode.NotFound;
                        return response;
                    }

                    if(context.CacheWithEtag(rev.GetRevId(), response)) {
                        response.InternalStatus = StatusCode.NotModified;
                        return response;
                    }

                    if(!isLocalDoc && includeAttachments) {
                        int minRevPos = 1;
                        IList<string> attsSince = context.Request.QueryString.JsonQuery("atts_since").AsList<string>();
                        string ancestorId = db.FindCommonAncestor(rev, attsSince);
                        if(ancestorId != null) {
                            minRevPos = RevisionInternal.GenerationFromRevID(ancestorId);
                        }

                        Status status = new Status();
                        bool attEncodingInfo = context.Request.QueryString.Get<bool>("att_encoding_info", bool.TryParse, false);
                        if(!Database.ExpandAttachments(rev, minRevPos, sendMultipart, attEncodingInfo, status)) {
                            response.InternalStatus = status.GetCode();
                            return response;
                        }
                    }

                    if(sendMultipart) {
                        response.SetMultipartBody(db.MultipartWriterForRev(rev, "multipart/related"));
                    } else {
                        response.Body = rev.GetBody();
                    }
                } else {
                    // open_revs query:
                    IList<IDictionary<string, object>> result;
                    if(openRevsParam.Equals("all")) {
                        // ?open_revs=all returns all current/leaf revisions:
                        bool includeDeleted = context.Request.QueryString.Get<bool>("include_deleted", bool.TryParse, false);
                        RevisionList allRevs = db.GetAllRevisionsOfDocumentID(docId, true);

                        result = new List<IDictionary<string, object>>();
                        foreach(var rev in allRevs) {
                            if(!includeDeleted && rev.IsDeleted()) {
                                continue;
                            }

                            Status status = new Status();
                            RevisionInternal loadedRev = db.RevisionByLoadingBody(rev, status);
                            if(loadedRev != null) {
                                ApplyOptions(options, loadedRev, context, db, status);
                            }

                            if(loadedRev != null) {
                                result.Add(new Dictionary<string, object> { { "ok", loadedRev.GetProperties() } });
                            } else if(status.GetCode() <= StatusCode.InternalServerError) {
                                result.Add(new Dictionary<string, object> { { "missing", rev.GetRevId() } });
                            } else {
                                response.InternalStatus = status.GetCode();
                                return response;
                            }
                        }
                    } else {
                        // ?open_revs=[...] returns an array of specific revisions of the document:
                        var openRevs = context.Request.QueryString.JsonQuery("open_revs").AsList<object>();
                        if(openRevs == null) {
                            response.InternalStatus = StatusCode.BadParam;
                            return response;
                        }

                        result = new List<IDictionary<string, object>>();
                        foreach(var revIDObj in openRevs) {
                            var revID = revIDObj as string;
                            if(revID == null) {
                                response.InternalStatus = StatusCode.BadId;
                                return response;
                            }

                            Status status = new Status();
                            var rev = db.GetDocumentWithIDAndRev(docId, revID, DocumentContentOptions.None, status);
                            if(rev != null) {
                                rev = ApplyOptions(options, rev, context, db, status);
                            }

                            if(rev != null) {
                                result.Add(new Dictionary<string, object>{ { "ok", rev.GetProperties() } });
                            } else {
                                result.Add(new Dictionary<string, object>{ { "missing", revID } });
                            }
                        }
                    }

                    if(mustSendJson) {
                        response.Body = new Body(result.Cast<object>().ToList());
                    } else {
                        response.SetMultipartBody(result.Cast<object>().ToList(), "multipart/mixed");
                    }
                }

                return response;
            }).AsDefaultState();
        }

        private static RevisionInternal ApplyOptions(DocumentContentOptions options, RevisionInternal rev, HttpListenerContext context,
            Database db, Status outStatus)
        {
            if ((options & (DocumentContentOptions.IncludeRevs | DocumentContentOptions.IncludeRevsInfo | DocumentContentOptions.IncludeConflicts |
                DocumentContentOptions.IncludeAttachments | DocumentContentOptions.IncludeLocalSeq)) != 0) {
                var dst = rev.GetProperties(); 
                if (options.HasFlag(DocumentContentOptions.IncludeLocalSeq)) {
                    dst["_local_seq"] = rev.GetSequence();
                }

                if (options.HasFlag(DocumentContentOptions.IncludeRevs)) {
                    dst["_revisions"] = db.GetRevisionHistoryDict(rev);
                }

                if (options.HasFlag(DocumentContentOptions.IncludeRevsInfo)) {
                    dst["_revs_info"] = db.GetRevisionHistory(rev).Select(x =>
                    {
                        string status = "available";
                        if(x.IsDeleted()) {
                            status = "deleted";
                        } else if(x.IsMissing()) {
                            status = "missing";
                        }

                        return new Dictionary<string, object> {
                            { "rev", x.GetRevId() },
                            { "status", status }
                        };
                    });
                }

                if (options.HasFlag(DocumentContentOptions.IncludeConflicts)) {
                    RevisionList revs = db.GetAllRevisionsOfDocumentID(rev.GetDocId(), true);
                    if (revs.Count > 1) {
                        dst["_conflicts"] = revs.Select(x =>
                        {
                            return x.Equals(rev) || x.IsDeleted() ? null : x.GetRevId();
                        });
                    }
                }

                RevisionInternal nuRev = new RevisionInternal(dst);
                if (options.HasFlag(DocumentContentOptions.IncludeAttachments)) {
                    bool attEncodingInfo = context.Request.QueryString.Get<bool>("att_encoding_info", bool.TryParse, false);
                    if(!Database.ExpandAttachments(nuRev, 0, false, !attEncodingInfo, outStatus)) {
                        return null;
                    }
                }

                rev = nuRev;
            }

            return rev;
        }
    }
}

