// 
// HttpLogicException.cs
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

namespace Couchbase.Lite.Sync
{
    public enum HttpLogicError
    {
        TooManyRedirects,
        BadRedirectLocation
    }

    public sealed class HttpLogicException : Exception
    {
        #region Properties

        public HttpLogicError Code { get; }

        #endregion

        #region Constructors

        public HttpLogicException(HttpLogicError error) : base(GetMessage(error))
        {
            Code = error;
        }

        #endregion

        #region Private Methods

        private static string GetMessage(HttpLogicError error)
        {
            switch (error) {
                case HttpLogicError.BadRedirectLocation:
                    return "HTTP request was redirected to a non-existent location";
                case HttpLogicError.TooManyRedirects:
                    return "Too many HTTP redirects for the request to handle";
            }

            return "Unknown error";
        }

        #endregion
    }
}
