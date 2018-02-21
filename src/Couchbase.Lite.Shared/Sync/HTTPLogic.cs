// 
// HTTPLogic.cs
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Couchbase.Lite.DI;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Util;

using LiteCore;
using LiteCore.Interop;

namespace Couchbase.Lite.Sync
{
    internal sealed class HTTPLogic
    {
        #region Constants

        private const int MaxRedirects = 10;
        private const string Tag = nameof(HTTPLogic);

		#endregion

		#region Variables

		public static readonly string UserAgent = GetUserAgent();

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private string _authorizationHeader;
        private uint _redirectCount;
        private UriBuilder _urlRequest;

        #endregion

        #region Properties

        public NetworkCredential Credential { get; set; }

        public Exception Error { get; private set; }

        public bool HandleRedirects { get; set; }

        public int HttpStatus { get; private set; }

        public string this[string key]
        {
            get => _headers[key];
            set => _headers[key] = value?.TrimEnd();
        }

        public ushort Port
        {
            get {
                if (_urlRequest.Port == -1) {
                    return UseTls ? (ushort)443 : (ushort)80;
                }

                return (ushort)_urlRequest.Port;
            }
        }

        public bool ShouldContinue { get; private set; }

        public bool ShouldRetry { get; private set; }

        public Uri UrlRequest => _urlRequest.Uri;

        public bool UseTls
        {
            get {
                var scheme = _urlRequest.Scheme.ToLowerInvariant();
                return "https".Equals(scheme) || "wss".Equals(scheme);
            }
        }

        #endregion

        #region Constructors

        public HTTPLogic(Uri url)
        {
            _urlRequest = new UriBuilder(url);
            HandleRedirects = true;
        }

        #endregion

        #region Public Methods

        public byte[] HTTPRequestData()
        {
            var stringBuilder = new StringBuilder($"GET {_urlRequest.Path} HTTP/1.1\r\n");
            if (!_headers.ContainsKey("User-Agent")) {
                _headers["User-Agent"] = UserAgent;
            }

            if (!_headers.ContainsKey("Host")) {
                _headers["Host"] = $"{_urlRequest.Host}:{_urlRequest.Port}";
            }

            if (ShouldRetry && _authorizationHeader != null) {
                _headers["Authorization"] = _authorizationHeader;
            }

            foreach (var header in _headers) {
                stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
            }
            stringBuilder.Append("\r\n");

            ShouldContinue = ShouldRetry = false;
            HttpStatus = 0;

            return Encoding.ASCII.GetBytes(stringBuilder.ToString());
        }

        public void ReceivedResponse(HttpMessageParser parser)
        {
            ShouldContinue = ShouldRetry = false;
            var httpStatus = parser.StatusCode;
            switch (httpStatus) {
                case HttpStatusCode.Moved:
                case HttpStatusCode.Found:
                case HttpStatusCode.RedirectKeepVerb:
                    if (!HandleRedirects) {
                        break;
                    }

                    if (++_redirectCount > MaxRedirects) {
                        Error = new HttpLogicException(HttpLogicError.TooManyRedirects);
                    } else if (!Redirect(parser)) {
                        Error = new HttpLogicException(HttpLogicError.BadRedirectLocation);
                    } else {
                        ShouldRetry = true;
                    }

                    break;
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.ProxyAuthenticationRequired:
                    var authResponse = parser.Headers.Get("WWW-Authenticate");
                    if (authResponse == null) {
                        Log.To.Sync.W(Tag, "HTTP missing WWW-Authenticate response!");
                        Error = new LiteCoreException(new C4Error(C4ErrorCode.RemoteError));
                        break;
                    }

                    if (_authorizationHeader == null && Credential != null) {
                        _authorizationHeader = CreateAuthHeader(authResponse);
                        var password = new SecureLogString(Credential.Password, LogMessageSensitivity.Insecure);
                        Log.To.Sync.I(Tag, $"Auth challenge; credential = {Credential.UserName} / {password}");
                        ShouldRetry = true;
                        break;
                    }

                    var auth = new SecureLogString(_authorizationHeader, LogMessageSensitivity.Insecure);
                    var wwwAuth = new SecureLogString(authResponse, LogMessageSensitivity.Insecure);
                    Log.To.Sync.I(Tag, $"HTTP auth failed; sent Authorization {auth} ; got WWW-Authenticate {wwwAuth}");
                    Error = new HttpLogicException(HttpLogicError.Unauthorized);

                    break;
                default:
                    if ((int) httpStatus < 300) {
                        ShouldContinue = true;
                    }
                    break;
            }

            HttpStatus = (int)httpStatus;
        }

        #endregion

        #region Private Methods

        private string CreateAuthHeader(string authResponse)
        {
            var challenge = ParseAuthHeader(authResponse);
            if (challenge == null) {
                return null;
            }

            if (challenge["Scheme"] == "Basic") {
                var cipher = Encoding.UTF8.GetBytes($"{Credential.UserName}:{Credential.Password}");
                var encodedVal = Convert.ToBase64String(cipher);
                return $"Basic {encodedVal}";
            }

            return null;
        }

		private static string GetUserAgent()
		{

			var versionAtt = (AssemblyInformationalVersionAttribute)typeof(Database).GetTypeInfo().Assembly
				.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
			var version = versionAtt?.InformationalVersion ?? "Unknown";
            var regex = new Regex("([0-9]+\\.[0-9]+\\.[0-9]+)-b([0-9]+)");
			var build = "0";
			var commit = "unknown";
			if (regex.IsMatch(version))
			{
				var match = regex.Match(version);
				build = match.Groups[2].Value.TrimStart('0');
				version = match.Groups[1].Value;
			}

			try
			{
				var st = typeof(Database).GetTypeInfo().Assembly.GetManifestResourceStream("version");
				using (var reader = new StreamReader(st, Encoding.ASCII, false, 32, false))
				{
					commit = reader.ReadToEnd().TrimEnd();
				}
			}
			catch (Exception e)
			{
				Log.To.Couchbase.W(Tag, "Error getting commit information", e);
			}

			var runtimePlatform = Service.GetInstance<IRuntimePlatform>();
			var osDescription = runtimePlatform?.OSDescription ?? RuntimeInformation.OSDescription;
			var hardware = runtimePlatform != null ? $"; {runtimePlatform.HardwareName}" : "";
			return $"CouchbaseLite/{version} (.NET; {osDescription}{hardware}) Build/{build} LiteCore/{Native.c4_getVersion()} Commit/{commit}";
		}

        private Dictionary<string, string> ParseAuthHeader(string authResponse)
        {
            if (authResponse == null) {
                return null;
            }

            var challenge = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var re = new Regex("(\\w+)\\s+(\\w+)=((\\w+)|\"([^\"]+))");
            var groups = re.Match(authResponse).Groups;
            var key = authResponse.Substring(groups[2].Index, groups[2].Length);
            var k = groups[4];
            if (k.Length == 0) {
                k = groups[5];
            }

            challenge[key] = authResponse.Substring(k.Index, k.Length);
            challenge["Scheme"] = authResponse.Substring(groups[1].Index, groups[1].Length);
            challenge["WWW-Authenticate"] = authResponse;
            return challenge;
        }

        private bool Redirect(HttpMessageParser parser)
        {
            string location;
            if (!parser.Headers.TryGetValue("location", out location)) {
                return false;
            }

            Uri url;
            if (!Uri.TryCreate(UrlRequest, location, out url)) {
                return false;
            }

            if (!url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            _urlRequest = new UriBuilder(url);
            return true;
        }

        #endregion
    }
}
