//
//  CouchbaseListenerContext.cs
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;

using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.Listener
{

    /// <summary>
    /// A base class implementation of ICouchbaseListenerContext, for common logic like
    /// parsing URLs
    /// </summary>
    public abstract class CouchbaseListenerContext : ICouchbaseListenerContext
    {

        #region Variables

        private string[] _urlComponents;                    // The URL split by slashes
        private string _viewPrefix = String.Empty;          // The prefix to add to the view (_local or _design)
        private QueryOptions _queryOptions;                 // The options of the HTTP URL query (i.e. stuff after the '?')
        private DocumentContentOptions? _contentOptions;    // The content options specified in the URL query
        private ChangesFeedMode? _changesFeedMode;          // The changes feed mode specified in the URL query

        #endregion

        #region Properties

        // ICouchbaseListenerContext
        public Manager DbManager { get; private set; }

        // ICouchbaseListenerContext
        public string DatabaseName {
            get {
                //Must do this twice because Unity3D requires double escaping of encoded slashes in URL
                var name = Uri.UnescapeDataString(Uri.UnescapeDataString(UrlComponentAt(0)));
                if (!Manager.IsValidDatabaseName(name)) {
                    throw new CouchbaseLiteException(StatusCode.BadId);
                }

                return name;
            }
        }

        // ICouchbaseListenerContext
        public string DocumentName {
            get {
                if(String.IsNullOrEmpty(_viewPrefix)) {
                    return UrlComponentAt(1);
                }

                return _viewPrefix + UrlComponentAt(2);
            }
        }

        // ICouchbaseListenerContext
        public string AttachmentName {
            get {
                int startPos = !String.IsNullOrEmpty(_viewPrefix) ? 3 : 2;
                return String.Join("/", _urlComponents, startPos, _urlComponents.Length - startPos);
            }
        }

        // ICouchbaseListenerContext
        public string DesignDocName
        {
            get {
                if (UrlComponentAt(1).Equals("_design")) {
                    return UrlComponentAt(2);
                }

                return null;
            }
        }

        // ICouchbaseListenerContext
        public string ViewName
        {
            get {
                if (UrlComponentAt(1).Equals("_design")) {
                    return UrlComponentAt(4);
                }

                return null;
            }
        }

        // ICouchbaseListenerContext
        public QueryOptions QueryOptions {
            get {
                if (_queryOptions != null) {
                    return _queryOptions;
                }

                _queryOptions = new QueryOptions();
                _queryOptions.Skip = GetQueryParam<int>("skip", int.TryParse, _queryOptions.Skip);
                _queryOptions.Limit = GetQueryParam<int>("limit", int.TryParse, _queryOptions.Limit);
                _queryOptions.GroupLevel = GetQueryParam<int>("group_level", int.TryParse, _queryOptions.GroupLevel);
                _queryOptions.Descending = GetQueryParam<bool>("descending", bool.TryParse, _queryOptions.Descending);
                _queryOptions.IncludeDocs = GetQueryParam<bool>("include_docs", bool.TryParse, _queryOptions.IncludeDocs);
                if (GetQueryParam<bool>("include_deleted", bool.TryParse, false)) {
                    _queryOptions.AllDocsMode = AllDocsMode.IncludeDeleted;
                } else if (GetQueryParam<bool>("include_conflicts", bool.TryParse, false)) { //non-standard
                    _queryOptions.AllDocsMode = AllDocsMode.ShowConflicts; 
                } else if(GetQueryParam<bool>("only_conflicts", bool.TryParse, false)) { //non-standard
                    _queryOptions.AllDocsMode = AllDocsMode.OnlyConflicts;
                }

                _queryOptions.UpdateSeq = GetQueryParam<bool>("update_seq", bool.TryParse, false);
                _queryOptions.InclusiveEnd = GetQueryParam<bool>("inclusive_end", bool.TryParse, false);
                //TODO: InclusiveStart
                //TODO: PrefixMatchLevel
                _queryOptions.ReduceSpecified = GetQueryParam("reduce") != null;
                _queryOptions.Reduce = GetQueryParam<bool>("reduce", bool.TryParse, false);
                _queryOptions.Group = GetQueryParam<bool>("group", bool.TryParse, false);
                _queryOptions.ContentOptions = ContentOptions;

                // Stale options (ok or update_after):
                string stale = GetQueryParam("stale");
                if(stale != null) {
                    if (stale.Equals("ok")) {
                        _queryOptions.Stale = IndexUpdateMode.Never;
                    } else if (stale.Equals("update_after")) {
                        _queryOptions.Stale = IndexUpdateMode.After;
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
                    _queryOptions.Keys = keys;
                } else {
                    try {
                        _queryOptions.StartKey = GetJsonQueryParam("start_key");
                        _queryOptions.EndKey = GetJsonQueryParam("end_key");
                        _queryOptions.StartKeyDocId = GetJsonQueryParam("startkey_docid") as string;
                        _queryOptions.EndKeyDocId = GetJsonQueryParam("endkey_docid") as string;
                    } catch(CouchbaseLiteException) {
                        return null;
                    }
                }

                //TODO:  Full text and bbox

                return _queryOptions;
            }
        }

        // ICouchbaseListenerContext
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

        // ICouchbaseListenerContext
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

        // ICouchbaseListenerContext
        public abstract NameValueCollection RequestHeaders { get; }

        // ICouchbaseListenerContext
        public abstract Uri RequestUrl { get; }

        // ICouchbaseListenerContext
        public abstract Stream BodyStream { get; }

        // ICouchbaseListenerContext
        public abstract string Method { get; }

        // ICouchbaseListenerContext
        public abstract long ContentLength { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="manager">The manager responsible for opening DBs, etc</param>
        protected CouchbaseListenerContext(Manager manager)
        {
            DbManager = manager;
        }

        #endregion

        #region ICouchbaseListenerContext

        public object GetJsonQueryParam(string key)
        {
            string value = GetQueryParam(key);
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

        public bool ExplicitlyAcceptsType(string type)
        {
            string accept = RequestHeaders.Get("Accept");
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

        public T BodyAs<T>() where T : class, new()
        {
            var bodyStream = BodyStream;
            if(bodyStream != null) {
                try {
                    var body = Manager.GetObjectMapper().ReadValue<T>(BodyStream);
                    return body;
                } catch(CouchbaseLiteException) {
                    return null;
                }
            }

            return new T();
        }

        public abstract CouchbaseLiteResponse CreateResponse(StatusCode code = StatusCode.Ok);

        public abstract string GetQueryParam(string key);

        public abstract IDictionary<string, object> GetQueryParams();

        public abstract bool CacheWithEtag(string etag);

        #endregion

        #region Private Methods

        // Processes the URL string and splits it into components, then returns the component 
        // at the specified index
        private string UrlComponentAt(int index)
        {
            if (_urlComponents == null) {
                var tmp = RequestUrl.AbsolutePath.Split('?')[0];
                _urlComponents = tmp.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (_urlComponents.Length >= 3) {
                    var secondComponent = _urlComponents[1];
                    if (secondComponent.Equals("_local") || secondComponent.Equals("_design")) {
                        _viewPrefix = secondComponent + "/";
                    }
                }
            }

            return _urlComponents.ElementAtOrDefault(index);
        }

        #endregion
    }
}

