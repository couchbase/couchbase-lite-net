// 
//  WindowsProxy.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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

// Windows 2012 doesn't define the more generic variants
#if (NETFRAMEWORK || NET462 || NET6_0_OR_GREATER) && !NET6_0_WINDOWS10_0_19041_0
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;

using Microsoft.Win32.SafeHandles;

#if !NET8_0_OR_GREATER
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#endif

namespace Couchbase.Lite.Support;

[ExcludeFromCodeCoverage]
internal class SafeWinHttpHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
{
    private SafeWinHttpHandle? _parentHandle;

    public static void DisposeAndClearHandle(ref SafeWinHttpHandle? safeHandle)
    {
        safeHandle?.Dispose();
        safeHandle = null;
    }

    public void SetParentHandle(SafeWinHttpHandle parentHandle)
    {
        Debug.Assert(_parentHandle == null);
        Debug.Assert(parentHandle != null);
        Debug.Assert(!parentHandle.IsInvalid);

        var ignore = false;
        parentHandle.DangerousAddRef(ref ignore);
                
        _parentHandle = parentHandle;
    }

    [DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpCloseHandle(
        IntPtr handle);

    // Important: WinHttp API calls should not happen while another WinHttp call for the same handle did not 
    // return. During finalization that was not initiated by the Dispose pattern we don't expect any other WinHttp
    // calls in progress.
    protected override bool ReleaseHandle()
    {
        if (_parentHandle != null)
        {
            _parentHandle.DangerousRelease();
            _parentHandle = null;
        }
                
        return WinHttpCloseHandle(handle);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal sealed class WindowsProxy : IProxy
{
    private const uint ERROR_WINHTTP_AUTODETECTION_FAILED = 12180;
    private const string Tag = nameof(WindowsProxy);
    private const uint WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY = 4;

    private const uint WINHTTP_AUTO_DETECT_TYPE_DHCP = 0x00000001;
    private const uint WINHTTP_AUTO_DETECT_TYPE_DNS_A = 0x00000002;

    private const uint WINHTTP_AUTOPROXY_AUTO_DETECT = 0x00000001;
    private const string? WINHTTP_NO_PROXY_BYPASS = null;

    private const string? WINHTTP_NO_PROXY_NAME = null;

    [DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpGetIEProxyConfigForCurrentUser(
        out WINHTTP_CURRENT_USER_IE_PROXY_CONFIG proxyConfig);

    [DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpGetProxyForUrl(
        SafeWinHttpHandle sessionHandle, string url,
        ref WINHTTP_AUTOPROXY_OPTIONS autoProxyOptions,
        out WINHTTP_PROXY_INFO proxyInfo);

    [DllImport("winhttp.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern SafeWinHttpHandle WinHttpOpen([In] [Optional] [MarshalAs(UnmanagedType.LPWStr)]
        string? pwszUserAgent,
        [In] uint dwAccessType, [In] [MarshalAs(UnmanagedType.LPWStr)] string? pwszProxyName,
        [In] [MarshalAs(UnmanagedType.LPWStr)] string? pwszProxyBypass, [In] uint dwFlags);

    public Task<WebProxy?> CreateProxyAsync(Uri destination)
    {
        var success = WinHttpGetIEProxyConfigForCurrentUser(out var ieProxy);
        if (success && ieProxy.Proxy != IntPtr.Zero) {
            var proxyUrl = Marshal.PtrToStringUni(ieProxy.Proxy);
            var bypassList = Marshal.PtrToStringUni(ieProxy.ProxyBypass)?.Split(';', ' ', '\t', '\r', '\n');
            return Task.FromResult<WebProxy?>(new WebProxy(new Uri($"http://{proxyUrl}"), bypassList?.Contains("<local>") ?? false, bypassList));
        }

        var session = WinHttpOpen(null, WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY, WINHTTP_NO_PROXY_NAME,
            WINHTTP_NO_PROXY_BYPASS, 0);
        if (session.IsInvalid) {
            WriteLog.To.Sync.W(Tag,
                $"Unable to open WinHttp session to query for proxy (error code: {Marshal.GetLastWin32Error()})");
            return Task.FromResult(default(WebProxy));
        }

        var options = new WINHTTP_AUTOPROXY_OPTIONS
        {
            AutoConfigUrl = null,
            AutoDetectFlags = WINHTTP_AUTO_DETECT_TYPE_DHCP | WINHTTP_AUTO_DETECT_TYPE_DNS_A,
            AutoLoginIfChallenged = false,
            Flags = WINHTTP_AUTOPROXY_AUTO_DETECT,
            Reserved1 = IntPtr.Zero,
            Reserved2 = 0
        };

        success = WinHttpGetProxyForUrl(session, destination.AbsoluteUri, ref options, out var info);
        session.Close();
        if (!success) {
            var lastErr = Marshal.GetLastWin32Error();
            WriteLog.To.Sync.W(Tag,
                lastErr != ERROR_WINHTTP_AUTODETECTION_FAILED 
                    ? $"Call to WinHttpGetProxyForUrl failed (error code: {lastErr})" 
                    : "Call to WinHttpGetProxyForUrl failed (possible direct connection)...");

            return Task.FromResult(default(WebProxy));
        }

        if (info.Proxy == IntPtr.Zero) {
            WriteLog.To.Sync.W(Tag, "Call to WinHttpGetProxyForUrl succeed, however, proxy server list is null.");
            return Task.FromResult(default(WebProxy));
        }

        var url = Marshal.PtrToStringUni(info.Proxy);
        var bypass = Marshal.PtrToStringUni(info.ProxyBypass)?.Split(';', ' ', '\t', '\r', '\n');
        //TODO: free memory allocated by proxy server list and bypass list
        return Task.FromResult<WebProxy?>(new WebProxy(new Uri($"http://{url}"), bypass?.Contains("<local>") ?? false, bypass));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINHTTP_AUTOPROXY_OPTIONS
    {
        public uint Flags;
        public uint AutoDetectFlags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? AutoConfigUrl;
        public IntPtr Reserved1;
        public uint Reserved2;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AutoLoginIfChallenged;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WINHTTP_CURRENT_USER_IE_PROXY_CONFIG
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool AutoDetect;
        public IntPtr AutoConfigUrl;
        public IntPtr Proxy;
        public IntPtr ProxyBypass;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINHTTP_PROXY_INFO
    {
        public uint AccessType;
        public IntPtr Proxy;
        public IntPtr ProxyBypass;
    }
}
#endif