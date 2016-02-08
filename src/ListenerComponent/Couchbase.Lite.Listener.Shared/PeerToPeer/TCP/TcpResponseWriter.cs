//
//  TcpResponseWriter.cs
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
using System.IO;
using System.Text;
using WebSocketSharp.Net;

namespace Couchbase.Lite.Listener.Tcp
{
    /// <summary>
    /// An implementation of ICouchbaseResponseWriter for TCP/IP
    /// </summary>
    internal sealed class TcpResponseWriter : ICouchbaseResponseWriter
    {

        #region Variables

        private readonly HttpListenerResponse _responseObject;

        #endregion

        #region Properties

        public Encoding ContentEncoding
        {
            get { return _responseObject.ContentEncoding;  } 
            set { _responseObject.ContentEncoding = value; }
        }

        public long ContentLength
        {
            get { return _responseObject.ContentLength64; }
            set { _responseObject.ContentLength64 = value; }
        }

        public Stream OutputStream
        {
            get { return _responseObject.OutputStream; }
        }

        public int StatusCode
        {
            get { return _responseObject.StatusCode; }
            set { _responseObject.StatusCode = value; }
        }

        public string StatusDescription
        {
            get { return _responseObject.StatusDescription; }
            set { _responseObject.StatusDescription = value; }
        }

        public bool IsChunked 
        {
            get { return _responseObject.SendChunked; }
            set { _responseObject.SendChunked = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="response">The response object to write to</param>
        public TcpResponseWriter(HttpListenerResponse response)
        {
            _responseObject = response;
        }

        #endregion

        #region ICouchbaseResponseWriter

        public void AddHeader(string name, string value)
        {
            if (name == "Content-Type") {
                _responseObject.ContentType = value;
            } else {
                _responseObject.AddHeader(name, value);
            }
        }

        public void ClearHeaders()
        {
            _responseObject.Headers.Clear();
        }

        public void Close()
        {
            _responseObject.Close();
        }

        #endregion
    }
}

