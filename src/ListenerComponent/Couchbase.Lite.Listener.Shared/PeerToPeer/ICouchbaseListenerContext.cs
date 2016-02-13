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
using System.Collections.Specialized;
using System.IO;

using Couchbase.Lite.Replicator;
using System.Collections.Generic;

namespace Couchbase.Lite.Listener
{
    /// <summary>
    /// The signature of a method that can try to parse a string to a value
    /// of the specified type
    /// </summary>
    public delegate bool TryParseDelegate<T>(string s, out T value);

    /// <summary>
    /// An interface containing all of the information needed to process
    /// a P2P request
    /// </summary>
    public interface ICouchbaseListenerContext
    {

        /// <summary>
        /// The manager in charge of opening DBs, etc
        /// </summary>
        Manager DbManager { get; }

        /// <summary>
        /// The HTTP request body as a sstream
        /// </summary>
        Stream BodyStream { get; }

        /// <summary>
        /// The headers contained in the HTTP request
        /// </summary>
        NameValueCollection RequestHeaders { get; }

        /// <summary>
        /// The length of the content in the HTTP request
        /// </summary>
        long ContentLength { get; }

        /// <summary>
        /// The query options provided in the HTTP URL query
        /// </summary>
        QueryOptions QueryOptions { get; }

        /// <summary>
        /// The document content options provided in the HTTP URL query
        /// </summary>
        DocumentContentOptions ContentOptions { get; }

        /// <summary>
        /// The changes feed mode specified in the HTTP URL query
        /// </summary>
        /// <value>The changes feed mode.</value>
        ChangesFeedMode ChangesFeedMode { get; }

        /// <summary>
        /// The database name specified in the URL
        /// </summary>
        string DatabaseName { get; }

        /// <summary>
        /// The document name specified in the URL
        /// </summary>
        string DocumentName { get; }

        /// <summary>
        /// The attachment name specified in the URL
        /// </summary>
        /// <value>The name of the attachment.</value>
        string AttachmentName { get; }

        /// <summary>
        /// The design doc name specified in the URL
        /// </summary>
        /// <value>The name of the design document.</value>
        string DesignDocName { get; }

        /// <summary>
        /// The view name specified in the URL
        /// </summary>
        /// <value>The name of the view.</value>
        string ViewName { get; }

        /// <summary>
        /// The method used in the HTTP request
        /// </summary>
        /// <value>The method.</value>
        string Method { get; }

        /// <summary>
        /// The URL of the HTTP request
        /// </summary>
        /// <value>The request URL.</value>
        Uri RequestUrl { get; }

        /// <summary>
        /// Gets the specified item in the URL query as a JSON document, if possible
        /// </summary>
        /// <returns>The JSON document</returns>
        /// <param name="key">The name of the query item to inspect</param>
        object GetJsonQueryParam(string key);

        /// <summary>
        /// Gets the specified item in the URL query as a specified type, if possible
        /// </summary>
        /// <returns>The specified item as <c>T</c></returns>
        /// <param name="key">The name of the query item to inspect</param>
        /// <param name="parseDelegate">The delegate to parse the string into <c>T</c></param>
        /// <param name="defaultVal">The default value to use if the string cannot be parsed</param>
        /// <typeparam name="T">The type to parse the query string to</typeparam>
        T GetQueryParam<T>(string key, TryParseDelegate<T> parseDelegate, T defaultVal = default(T));

        /// <summary>
        /// Gets the specified item in the URL query
        /// </summary>
        /// <returns>The specified item</returns>
        /// <param name="key">The name of the query item to get</param>
        string GetQueryParam(string key);

        /// <summary>
        /// Returns all of the query parameters
        /// </summary>
        /// <returns>The query parameters of the URL.</returns>
        IDictionary<string, object> GetQueryParams();

        /// <summary>
        /// Inserts the given string into the Etag header of the response, and checks to see if
        /// the request has the etag in the If-None-Match header
        /// </summary>
        /// <returns><c>true</c>, if with etag was found, <c>false</c> otherwise.</returns>
        /// <param name="etag">The etag to search for / insert</param>
        bool CacheWithEtag(string etag);

        /// <summary>
        /// Checks to see if there is an explicit entry in the Accept header of the request that
        /// accepts the specified type
        /// </summary>
        /// <returns><c>true</c>, if the Accept header contains the entry, <c>false</c> otherwise.</returns>
        /// <param name="type">The content type to search for</param>
        bool ExplicitlyAcceptsType(string type);

        /// <summary>
        /// Gets and trims the If-Match header on the request
        /// </summary>
        /// <returns>The trimmed If-Match header</returns>
        string IfMatch();

        /// <summary>
        /// Reads the HTTP request body and casts it
        /// </summary>
        /// <returns>The body as <c>T</c></returns>
        /// <typeparam name="T">The type to cast the body to</typeparam>
        T BodyAs<T>() where T : class, new();

        /// <summary>
        /// Create a CouchbaseLiteResponse object based on the current context and given
        /// status code
        /// </summary>
        /// <returns>The instantiated response object</returns>
        /// <param name="code">The status code for the response</param>
        CouchbaseLiteResponse CreateResponse(StatusCode code = StatusCode.Ok);

    }

    public interface ICouchbaseListenerContext2 : ICouchbaseListenerContext
    {
        bool IsLoopbackRequest { get; }

        Uri Sender { get; }
    }
}

