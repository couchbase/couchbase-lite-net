//
//  ICouchbaseListenerContext.cs
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
using System.IO;
using System.Collections.Specialized;
using System.Net.Http;
using Couchbase.Lite.Replicator;

namespace Couchbase.Lite.Listener
{
    internal delegate bool TryParseDelegate<T>(string s, out T value);

    internal interface ICouchbaseListenerContext
    {
        Manager DbManager { get; }

        HttpListenerContext HttpContext { get; }

        Stream HttpBodyStream { get; }

        NameValueCollection RequestHeaders { get; }

        long ContentLength { get; }

        QueryOptions QueryOptions { get; }

        DocumentContentOptions ContentOptions { get; }

        ChangesFeedMode ChangesFeedMode { get; }

        string DatabaseName { get; }

        string DocumentName { get; }

        string AttachmentName { get; }

        string DesignDocName { get; }

        string ViewName { get; }

        HttpMethod Method { get; }

        Uri RequestUrl { get; }

        object GetJsonQueryParam(string key);

        T GetQueryParam<T>(string key, TryParseDelegate<T> parseDelegate, T defaultVal = default(T));

        string GetQueryParam(string key);

        bool CacheWithEtag(string etag);

        bool ExplicitlyAcceptsType(string type);

        string IfMatch();

        T HttpBodyAs<T>() where T : class, new();

    }
}

