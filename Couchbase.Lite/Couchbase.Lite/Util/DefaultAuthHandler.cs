/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Sharpen;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Threading;
using System.Web.UI;

namespace Couchbase.Lite.Replicator
{

    internal sealed class DefaultAuthHandler : MessageProcessingHandler
    {
        public DefaultAuthHandler(HttpClientHandler context) : base()
        {
            this.context = context;
        }

        #region implemented abstract members of MessageProcessingHandler

        protected override HttpResponseMessage ProcessResponse (HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response;
        }

        /// <exception cref="Org.Apache.Http.HttpException"></exception>
        /// <exception cref="System.IO.IOException"></exception>
        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken token)
        {
//              AuthState authState = (AuthState)context.GetAttribute(ClientContext.TargetAuthState
//                  );
//              CredentialsProvider credsProvider = (CredentialsProvider)context.GetAttribute(ClientContext
//                  .CredsProvider);
//              HttpHost targetHost = (HttpHost)context.GetAttribute(ExecutionContext.HttpTargetHost
//                  );
//              if (authState.GetAuthScheme() == null)
//              {
//                  AuthScope authScope = new AuthScope(targetHost.GetHostName(), targetHost.GetPort(
//                      ));
//                  authState.SetAuthScheme(new BasicScheme());
//                  authState.SetCredentials(creds);
//              }

            if (!context.UseDefaultCredentials && (context.Credentials == null))
            {

                context.Credentials = request.ToCredentialsFromUri();
            }

            return request;
        }

        #endregion

        private IEnumerator GetEnumerator() { return AuthenticationManager.RegisteredModules; }

        private readonly HttpClientHandler context;
    }
}
