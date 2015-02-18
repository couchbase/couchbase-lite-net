//
// CouchbaseLiteHttpClientFactory.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;



namespace Couchbase.Lite.Support
{
    internal class CouchbaseLiteHttpClientFactory : IHttpClientFactory
    {
        const string Tag = "CouchbaseLiteHttpClientFactory";

        private readonly CookieStore cookieStore;

        public CouchbaseLiteHttpClientFactory(CookieStore cookieStore)
        {
            this.cookieStore = cookieStore;
            Headers = new ConcurrentDictionary<string,string>();

            // Disable SSL 3 fallback to mitigate POODLE vulnerability.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;

            //
            // Source: http://msdn.microsoft.com/en-us/library/office/dd633677(v=exchg.80).aspx
            // ServerCertificateValidationCallback returns true if either of the following criteria are met:
            // The certificate is valid and signed with a valid root certificate.
            // The certificate is self-signed by the server that returned the certificate.
            //
            ServicePointManager.ServerCertificateValidationCallback = 
            (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
            {
                // If the certificate is a valid, signed certificate, return true.
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                // If there are errors in the certificate chain, look at each error to determine the cause.
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                {
                    if (chain != null && chain.ChainStatus != null)
                    {
                        foreach (X509ChainStatus status in chain.ChainStatus)
                        {
                            if ((certificate.Subject == certificate.Issuer) &&
                                (status.Status == X509ChainStatusFlags.UntrustedRoot))
                            {
                                // Self-signed certificates with an untrusted root are valid. 
                                continue;
                            }
                            else
                            {
                                if (status.Status != X509ChainStatusFlags.NoError)
                                {
                                    // If there are any other errors in the certificate chain, the certificate is invalid,
                                    // so the method returns false.
                                    return false;
                                }
                            }
                        }
                    }

                    // When processing reaches this line, the only errors in the certificate chain are 
                    // untrusted root errors for self-signed certificates. These certificates are valid
                    // for default Exchange server installations, so return true.
                    return true;
                }
                else
                {
                    // In all other cases, return false.
                    return false;
                }
            };

            BuildHandlerPipeline();
        }

        /// <summary>
        /// Build a pipeline of HttpMessageHandlers.
        /// </summary>
        internal HttpMessageHandler BuildHandlerPipeline ()
        {
            var handler = new HttpClientHandler {
                CookieContainer = cookieStore,
                UseDefaultCredentials = true,
                UseCookies = true,
            };

            var authHandler = new DefaultAuthHandler (handler, cookieStore);

            var retryHandler = new TransientErrorRetryHandler(authHandler);

            return retryHandler;
        }

        public HttpClient GetHttpClient()
        {
            var authHandler = BuildHandlerPipeline();

            // As the handler will not be shared, client.Dispose() needs to be 
            // called once the operation is done to release the unmanaged resources 
            // and disposes of the managed resources.
            var client =  new CouchbaseLiteHttpClient(authHandler, true) 
            {
                Timeout = ManagerOptions.Default.RequestTimeout,
            };

            foreach(var header in Headers)
            {
                var success = client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                if (!success)
                    Util.Log.W(Tag, "Unabled to add header to request: {0}: {1}".Fmt(header.Key, header.Value));
            }

            return client;
        }

        public MessageProcessingHandler Handler { get; private set; }

        public IDictionary<string, string> Headers { get; set; }

        public void AddCookies(CookieCollection cookies)
        {
            cookieStore.Add(cookies);
        }

        public void DeleteCookie(Uri uri, string name)
        {
            cookieStore.Delete(uri, name);
        }

        public CookieContainer GetCookieContainer()
        {
            return cookieStore;
        }
    }
}
