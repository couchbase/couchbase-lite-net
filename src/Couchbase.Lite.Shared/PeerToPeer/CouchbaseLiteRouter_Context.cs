//
//  CouchbaseLiteRouter_Context.cs
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
using System.Collections.Specialized;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.PeerToPeer
{
    internal partial class CouchbaseLiteRouter
    {
        private sealed class CouchbaseListenerContext : ICouchbaseListenerContext
        {
            private string[] _urlComponents;
            private string _viewPrefix = String.Empty;
            private QueryOptions _queryOptions;
            private DocumentContentOptions? _contentOptions;
            private ChangesFeedMode? _changesFeedMode;

            public HttpListenerContext HttpContext { get; private set; }

            public Manager DbManager { get; private set; }

            public System.IO.Stream HttpBodyStream
            {
                get {
                    return HttpContext.Request.InputStream;
                }
            }

            public NameValueCollection RequestHeaders
            {
                get {
                    return HttpContext.Request.Headers;
                }
            }

            public long ContentLength
            {
                get {
                    return HttpContext.Request.ContentLength64;
                }
            }

            public string DatabaseName {
                get {
                    var name = Uri.UnescapeDataString(UrlComponentAt(0));
                    if (!Manager.IsValidDatabaseName(name)) {
                        throw new CouchbaseLiteException(StatusCode.BadId);
                    }

                    return name;
                }
            }

            public string DocumentName {
                get {
                    if(String.IsNullOrEmpty(_viewPrefix)) {
                        return UrlComponentAt(1);
                    }

                    return _viewPrefix + UrlComponentAt(2);
                }
            }

            public string AttachmentName {
                get {
                    int startPos = !String.IsNullOrEmpty(_viewPrefix) ? 3 : 2;
                    return String.Join("/", _urlComponents, startPos, _urlComponents.Length - startPos);
                }
            }

            public string DesignDocName
            {
                get {
                    if (UrlComponentAt(1).Equals("_design")) {
                        return UrlComponentAt(2);
                    }

                    return null;
                }
            }

            public string ViewName
            {
                get {
                    if (UrlComponentAt(1).Equals("_design")) {
                        return UrlComponentAt(4);
                    }

                    return null;
                }
            }

            public QueryOptions QueryOptions {
                get {
                    if (_queryOptions != null) {
                        return _queryOptions;
                    }

                    _queryOptions = new QueryOptions();
                    _queryOptions.SetSkip(GetQueryParam<int>("skip", int.TryParse, _queryOptions.GetSkip()));
                    _queryOptions.SetLimit(GetQueryParam<int>("limit", int.TryParse, _queryOptions.GetLimit()));
                    _queryOptions.SetGroupLevel(GetQueryParam<int>("group_level", int.TryParse, _queryOptions.GetGroupLevel()));
                    _queryOptions.SetDescending(GetQueryParam<bool>("descending", bool.TryParse, _queryOptions.IsDescending()));
                    _queryOptions.SetIncludeDocs(GetQueryParam<bool>("include_docs", bool.TryParse, _queryOptions.IsIncludeDocs()));
                    if (GetQueryParam<bool>("include_deleted", bool.TryParse, false)) {
                        _queryOptions.SetAllDocsMode(AllDocsMode.IncludeDeleted);
                    } else if (GetQueryParam<bool>("include_conflicts", bool.TryParse, false)) { //non-standard
                        _queryOptions.SetAllDocsMode(AllDocsMode.ShowConflicts); 
                    } else if(GetQueryParam<bool>("only_conflicts", bool.TryParse, false)) { //non-standard
                        _queryOptions.SetAllDocsMode(AllDocsMode.OnlyConflicts);
                    }

                    _queryOptions.SetUpdateSeq(GetQueryParam<bool>("update_seq", bool.TryParse, false));
                    _queryOptions.SetInclusiveEnd(GetQueryParam<bool>("inclusive_end", bool.TryParse, false));
                    //TODO: InclusiveStart
                    //TODO: PrefixMatchLevel
                    _queryOptions.SetReduceSpecified(GetQueryParam("reduce") != null);
                    _queryOptions.SetReduce(GetQueryParam<bool>("reduce", bool.TryParse, false));
                    _queryOptions.SetGroup(GetQueryParam<bool>("group", bool.TryParse, false));
                    _queryOptions.SetContentOptions(ContentOptions);

                    // Stale options (ok or update_after):
                    string stale = GetQueryParam("stale");
                    if(stale != null) {
                        if (stale.Equals("ok")) {
                            _queryOptions.SetStale(IndexUpdateMode.Never);
                        } else if (stale.Equals("update_after")) {
                            _queryOptions.SetStale(IndexUpdateMode.After);
                        } else {
                            return null;
                        }
                    }

                    IList<object> keys = null;
                    try {
                        keys = GetJsonQueryParam("keys").AsList<object>();
                        if (keys == null) {
                            var key = GetJsonQueryParam("key");
                            if (key != null) {
                                keys = new List<object> { key };
                            }
                        }
                    } catch(CouchbaseLiteException) {
                        return null;
                    }

                    if (keys != null) {
                        _queryOptions.SetKeys(keys);
                    } else {
                        try {
                            _queryOptions.SetStartKey(GetJsonQueryParam("start_key"));
                            _queryOptions.SetEndKey(GetJsonQueryParam("end_key"));
                            _queryOptions.SetStartKeyDocId(GetJsonQueryParam("startkey_docid") as string);
                            _queryOptions.SetEndKeyDocId(GetJsonQueryParam("endkey_docid") as string);
                        } catch(CouchbaseLiteException) {
                            return null;
                        }
                    }

                    //TODO:  Full text and bbox

                    return _queryOptions;
                }
            }

            public DocumentContentOptions ContentOptions {
                get {
                    if (_contentOptions.HasValue) {
                        return _contentOptions.Value;
                    }

                    _contentOptions = DocumentContentOptions.None;
                    if (GetQueryParam<bool>("attachments", bool.TryParse, false)) {
                        _contentOptions |= DocumentContentOptions.IncludeAttachments;
                    }

                    if (GetQueryParam<bool>("local_seq", bool.TryParse, false)) {
                        _contentOptions |= DocumentContentOptions.IncludeLocalSeq;
                    }

                    if (GetQueryParam<bool>("conflicts", bool.TryParse, false)) {
                        _contentOptions |= DocumentContentOptions.IncludeConflicts;
                    }

                    if (GetQueryParam<bool>("revs", bool.TryParse, false)) {
                        _contentOptions |= DocumentContentOptions.IncludeRevs;
                    }

                    if (GetQueryParam<bool>("revs_info", bool.TryParse, false)) {
                        _contentOptions |= DocumentContentOptions.IncludeRevsInfo;
                    }
                        
                    return _contentOptions.Value;
                }
            }

            public ChangesFeedMode ChangesFeedMode 
            {
                get {
                    if (_changesFeedMode.HasValue) {
                        return _changesFeedMode.Value;
                    }

                    _changesFeedMode = ChangesFeedMode.Normal;
                    string feed = GetQueryParam("feed");
                    if (feed == null) {
                        return _changesFeedMode.Value;
                    }

                    if (feed.Equals("longpoll")) {
                        _changesFeedMode = ChangesFeedMode.LongPoll;
                    } else if (feed.Equals("continuous")) {
                        _changesFeedMode = ChangesFeedMode.Continuous;
                    } else if (feed.Equals("eventsource")) {
                        _changesFeedMode = ChangesFeedMode.EventSource;
                    }

                    return _changesFeedMode.Value;
                }
            }

            public HttpMethod Method {
                get {
                    return new HttpMethod(HttpContext.Request.HttpMethod);
                }
            }

            public Uri RequestUrl {
                get {
                    return HttpContext.Request.Url;
                }
            }

            public CouchbaseListenerContext(HttpListenerContext context, Manager manager)
            {
                HttpContext = context;
                DbManager = manager;
            }

            public object GetJsonQueryParam(string key)
            {
                string value = HttpContext.Request.QueryString[key];
                if (value == null) {
                    return null;
                }

                return Manager.GetObjectMapper().ReadValue<object>(Uri.UnescapeDataString(value));
            }

            public T GetQueryParam<T>(string key, TryParseDelegate<T> parseDelegate, T defaultVal = default(T))
            {
                string value = GetQueryParam(key);
                if (value == null) {
                    return defaultVal;
                }

                T retVal;
                if (!parseDelegate(value, out retVal)) {
                    return defaultVal;
                }

                return retVal;
            }

            public string GetQueryParam(string key)
            {
                return HttpContext.Request.QueryString[key];
            }

            public bool CacheWithEtag(string etag)
            {
                etag = String.Format("\"{0}\"", etag);
                HttpContext.Response.Headers["Etag"] = etag;
                return etag.Equals(RequestHeaders.Get("If-None-Match"));
            }

            public bool ExplicitlyAcceptsType(string type)
            {
                string accept = HttpContext.Request.Headers["Accept"];
                return accept != null && accept.Contains(type);
            }

            public string IfMatch()
            {
                string ifMatch = RequestHeaders.Get("If-Match");
                if (ifMatch == null) {
                    return null;
                }

                // Value of If-Match is an ETag, so have to trim the quotes around it:
                if (ifMatch.Length > 2 && ifMatch.StartsWith("\"") && ifMatch.EndsWith("\"")) {
                    return ifMatch.Trim('"');
                }

                return null;
            }

            public T HttpBodyAs<T>() where T : class, new()
            {
                if(HttpContext.Request.HasEntityBody) {
                    try {
                        var body = Manager.GetObjectMapper().ReadValue<T>(HttpBodyStream);
                        return body;
                    } catch(CouchbaseLiteException) {
                        return null;
                    }
                }

                return new T();
            }

            private string UrlComponentAt(int index)
            {
                if (_urlComponents == null) {
                    _urlComponents = HttpContext.Request.Url.AbsolutePath.Split(new[]{ '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (_urlComponents.Length >= 3) {
                        var secondComponent = _urlComponents[1];
                        if (secondComponent.Equals("_local") || secondComponent.Equals("_design")) {
                            _viewPrefix = secondComponent + "/";
                        }
                    }
                }

                return _urlComponents.ElementAtOrDefault(index);
            }
        }
    }
}

