// 
// CouchbaseAndroidClientHandler.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using System;
using Java.Security;
using Java.Security.Cert;
using Javax.Net.Ssl;
using Xamarin.Android.Net;

namespace Couchbase.Lite
{
    internal sealed class CouchbaseAndroidClientHandler : AndroidClientHandler
    {
        public bool AllowSelfSigned { get; set; }

        protected override SSLSocketFactory ConfigureCustomSSLSocketFactory(HttpsURLConnection connection)
        {
            return AllowSelfSigned ? SelfSignedSocketFactory() : base.ConfigureCustomSSLSocketFactory(connection);
        }

        protected override IHostnameVerifier GetSSLHostnameVerifier(HttpsURLConnection connection)
        {
            return AllowSelfSigned ? new IgnoreHostnameVerifier() : base.GetSSLHostnameVerifier(connection);
        }

        private static SSLSocketFactory SelfSignedSocketFactory()
        {
            var trustManager = new SelfSignedTrustManager();
            var sslContext = SSLContext.GetInstance("TLS");
            sslContext.Init(null, new[] {trustManager}, null);
            return sslContext.SocketFactory;
        }

        private sealed class IgnoreHostnameVerifier : Java.Lang.Object, IHostnameVerifier
        {
            public bool Verify(string hostname, ISSLSession session)
            {
                return true;
            }
        }

        private sealed class SelfSignedTrustManager : Java.Lang.Object, IX509TrustManager
        {

            public void CheckClientTrusted(X509Certificate[] chain, string authType)
            {
                
            }

            public void CheckServerTrusted(X509Certificate[] chain, string authType)
            {
                
            }

            public X509Certificate[] GetAcceptedIssuers()
            {
                return new X509Certificate[0];
            }
        }
    }
}