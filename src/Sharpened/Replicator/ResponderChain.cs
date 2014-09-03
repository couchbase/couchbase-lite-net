// 
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
//using System.Net;
using Apache.Http;
using Couchbase.Lite.Replicator;
using Sharpen;

namespace Couchbase.Lite.Replicator
{
    /// <summary>A ResponderChain lets you stack up responders which will handle sequences of requests.
    ///     </summary>
    /// <remarks>A ResponderChain lets you stack up responders which will handle sequences of requests.
    ///     </remarks>
    public class ResponderChain : CustomizableMockHttpClient.Responder
    {
        /// <summary>A list of responders which are "consumed" as soon as they answer a request
        ///     </summary>
        private Queue<CustomizableMockHttpClient.Responder> responders;

        /// <summary>
        /// The final responder in the chain, which is "sticky" and won't
        /// be removed after handling a request.
        /// </summary>
        /// <remarks>
        /// The final responder in the chain, which is "sticky" and won't
        /// be removed after handling a request.
        /// </remarks>
        private CustomizableMockHttpClient.Responder sentinal;

        /// <summary>Create a responder chain.</summary>
        /// <remarks>
        /// Create a responder chain.
        /// In this version, you pass it a list of responders which are "consumed"
        /// as soon as they answer a request.  If you go off the edge and have more
        /// requests than responders, then it will throw a runtime exception.
        /// If you have no idea how many requests this responder chain will need to
        /// service, then set a sentinal or use the other ctor that takes a sentinal.
        /// </remarks>
        /// <param name="responders">
        /// a list of responders, which will be "consumed" as soon
        /// as they respond to a request.
        /// </param>
        public ResponderChain(Queue<CustomizableMockHttpClient.Responder> responders)
        {
            this.responders = responders;
        }

        /// <summary>Create a responder chain with a "sentinal".</summary>
        /// <remarks>
        /// Create a responder chain with a "sentinal".
        /// If and when the responders passed into responders are consumed, then the sentinal
        /// will handle all remaining requests to the responder chain.
        /// This is the version you want to use if you don't know ahead of time how many
        /// requests this responderchain will need to handle.
        /// </remarks>
        /// <param name="responders">
        /// a list of responders, which will be "consumed" as soon
        /// as they respond to a request.
        /// </param>
        /// <param name="sentinal">
        /// the final responder in the chain, which is "sticky" and won't
        /// be removed after handling a request.
        /// </param>
        public ResponderChain(Queue<CustomizableMockHttpClient.Responder> responders, CustomizableMockHttpClient.Responder
             sentinal)
        {
            this.responders = responders;
            this.sentinal = sentinal;
        }

        /// <exception cref="System.IO.IOException"></exception>
        public virtual HttpResponse Execute(HttpRequestMessage httpUriRequest)
        {
            CustomizableMockHttpClient.Responder responder;
            CustomizableMockHttpClient.Responder nextResponder = responders.Peek();
            if (nextResponder != null)
            {
                responder = responders.Remove();
            }
            else
            {
                if (sentinal != null)
                {
                    responder = sentinal;
                }
                else
                {
                    throw new RuntimeException("No more responders in queue");
                }
            }
            return responder.Execute(httpUriRequest);
        }
    }
}
