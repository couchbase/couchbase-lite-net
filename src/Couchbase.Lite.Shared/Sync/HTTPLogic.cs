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
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;

using LiteCore.Interop;

#if !NET8_0_OR_GREATER
using Couchbase.Lite.Util;
#endif

namespace Couchbase.Lite.Sync;

internal sealed class HTTPLogic(Uri url)
{
    private const int MaxRedirects = 10;
    private const string Tag = nameof(HTTPLogic);

    public static readonly string UserAgent = GetUserAgent();

    private readonly Dictionary<string, string?> _headers = new Dictionary<string, string?>();
    private string? _authorizationHeader;
    private uint _redirectCount;
    private UriBuilder _urlRequest = new(url);

    public NetworkCredential? Credential { get; set; }

    public NetworkCredential? ProxyCredential { get; set; }

    public Exception? Error { get; private set; }

    public bool HandleRedirects { get; set; } = true;

    public bool HasProxy { get; set; }

    public int HttpStatus { get; private set; }

    public string? this[string key]
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

    public byte[] HTTPRequestData()
    {
        var stringBuilder = new StringBuilder($"GET {_urlRequest.Path} HTTP/1.1\r\n");
        _headers.TryAdd("User-Agent", UserAgent);

        if (!_headers.ContainsKey("Host")) {
            _headers["Host"] = $"{_urlRequest.Host}:{_urlRequest.Port}";
        }

        _authorizationHeader = CreateAuthHeader();
        if (_authorizationHeader != null) {
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

    public byte[] ProxyRequest()
    {
        string toSend;
        if (ProxyCredential != null) {
            var base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ProxyCredential.UserName}:{ProxyCredential.Password}"));
            toSend = String.Format("CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}\r\nContent-Length: 0\r\nProxy-Connection: Keep-Alive\r\nProxy-Authorization: Basic {2}\r\nPragma: no-cache\r\n\r\n\r\n",
                _urlRequest.Host, _urlRequest.Port, base64);
        } else {
            toSend = String.Format("CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}\r\nProxy-Connection: keep-alive\r\n\r\n",
                _urlRequest.Host, _urlRequest.Port);
        }

        return Encoding.ASCII.GetBytes(toSend);
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
                    Error = new CouchbaseNetworkException(C4NetworkErrorCode.TooManyRedirects);
                } else if (!Redirect(parser)) {
                    Error = new CouchbaseNetworkException(C4NetworkErrorCode.InvalidRedirect);
                } else {
                    ShouldRetry = true;
                }

                break;
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.ProxyAuthenticationRequired:
                WriteLog.To.Sync.I(Tag, "HTTP auth failed");
                Error = new CouchbaseNetworkException(httpStatus);
                break;
            default:
                if ((int) httpStatus < 300) {
                    ShouldContinue = true;
                }
                break;
        }

        HttpStatus = (int)httpStatus;
    }

    private static string GetUserAgent()
    {

        var versionAtt = (AssemblyInformationalVersionAttribute?)typeof(Database).GetTypeInfo().Assembly
            .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
        var version = versionAtt?.InformationalVersion ?? "Unknown";
        var regex = new Regex("((?:[0-9+]\\.)+[0-9]+)-b([0-9]+)");
        var build = "0";
        var commit = ThisAssembly.Git.Commit;
            #if COUCHBASE_ENTERPRISE
        commit += $"+{SubmoduleInfo.Commit}";
            #endif
        if (regex.IsMatch(version))
        {
            var match = regex.Match(version);
            build = match.Groups[2].Value.TrimStart('0');
            version = match.Groups[1].Value;
        }

        var runtimePlatform = Service.GetInstance<IRuntimePlatform>();
        var osDescription = runtimePlatform?.OSDescription ?? RuntimeInformation.OSDescription;
        var hardware = runtimePlatform?.HardwareName != null ? $"; {runtimePlatform.HardwareName}" : "";
        return $"CouchbaseLite/{version} (.NET; {osDescription}{hardware}) Build/{build} LiteCore/{Native.c4_getVersion()} Commit/{commit}";
    }

    private string? CreateAuthHeader()
    {
        if (Credential == null) {
            return null;
        }

        var cipher = Encoding.UTF8.GetBytes($"{Credential.UserName}:{Credential.Password}");
        var encodedVal = Convert.ToBase64String(cipher);
        return $"Basic {encodedVal}";
    }

    private bool Redirect(HttpMessageParser parser)
    {
        if (!parser.Headers.TryGetValue("location", out var location)) {
            return false;
        }

        if (!Uri.TryCreate(UrlRequest, location, out var url)) {
            return false;
        }

        if (!url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        _urlRequest = new UriBuilder(url);
        return true;
    }
}