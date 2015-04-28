//
//  CouchbaseLiteTcpContext.cs
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
using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace Couchbase.Lite.Listener.Tcp
{

    /// <summary>
    /// An implementation of CouchbaseListenerContext for TCP/IP
    /// </summary>
    internal sealed class CouchbaseListenerTcpContext : CouchbaseListenerContext
    {
        #region Variables

        private readonly HttpListenerContext _httpContext;

        #endregion

        #region Properties

        public override Stream BodyStream
        {
            get {
                return _httpContext.Request.InputStream;
            }
        }

        public override NameValueCollection RequestHeaders
        {
            get {
                return _httpContext.Request.Headers;
            }
        }

        public override long ContentLength
        {
            get {
                return _httpContext.Request.ContentLength64;
            }
        }

        public override string Method {
            get {
                return _httpContext.Request.HttpMethod;
            }
        }

        public override Uri RequestUrl {
            get {
                return _httpContext.Request.Url;
            }
        }

        #endregion

        #region Constructors

        public CouchbaseListenerTcpContext(HttpListenerContext context, Manager manager) : base(manager)
        {
            _httpContext = context;
        }

        #endregion

        #region ICouchbaseListenerContext

        public override string GetQueryParam(string key)
        {
            return _httpContext.Request.QueryString[key];
        }

        public override bool CacheWithEtag(string etag)
        {
            etag = String.Format("\"{0}\"", etag);
            _httpContext.Response.Headers["Etag"] = etag;
            return etag.Equals(RequestHeaders.Get("If-None-Match"));
        }

        public override CouchbaseLiteResponse CreateResponse(StatusCode code = StatusCode.Ok)
        {
            return new CouchbaseLiteResponse(Method, RequestHeaders, new TcpResponseWriter(_httpContext.Response)) {
                InternalStatus = code
            };
        }

        #endregion
    }
}

