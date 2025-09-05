// 
//  IOSProxy.cs
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

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Couchbase.Lite.DI;

namespace Couchbase.Lite.Support;

[CouchbaseDependency]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal sealed partial class IOSProxy  : IProxy
{
    private const string libSystemLibrary = "/usr/lib/libSystem.dylib";

    private const string CFNetworkLibrary =
        "/System/Library/Frameworks/CFNetwork.framework/CFNetwork";

    private const string CFNetworkLibraryOld =
        "/System/Library/Frameworks/CoreServices.framework/Frameworks/CFNetwork.framework/CFNetwork";

    private const string CoreFoundationLibrary =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private static readonly IntPtr kCFProxyTypeKey = ReadCFNetworkPointer(nameof(kCFProxyTypeKey));
    private static readonly IntPtr kCFProxyTypeNone = ReadCFNetworkPointer(nameof(kCFProxyTypeNone));
    private static readonly IntPtr kCFProxyHostNameKey = ReadCFNetworkPointer(nameof(kCFProxyHostNameKey));
    private static readonly IntPtr kCFProxyPortNumberKey = ReadCFNetworkPointer(nameof(kCFProxyPortNumberKey));

    private const uint kCFStringEncodingASCII = 0x0600;
    private const int kCFNumberIntType = 9;

    public unsafe Task<WebProxy?> CreateProxyAsync(Uri destination)
    {
        IntPtr cFNetworkHandle = LoadCFNetwork();
        if (cFNetworkHandle == IntPtr.Zero) {
            return Task.FromResult(default(WebProxy));
        }

        var proxySettings = CopySystemProxySettings(cFNetworkHandle);
        if (proxySettings == IntPtr.Zero) {
            return Task.FromResult(default(WebProxy));
        }

        var cfUrlString = CFStringCreateWithCString(IntPtr.Zero, destination.AbsoluteUri,
            kCFStringEncodingASCII);
        if (cfUrlString == IntPtr.Zero) {
            CFRelease(proxySettings);
            return Task.FromResult(default(WebProxy));
        }

        var cfDestination = CFURLCreateWithString(IntPtr.Zero, cfUrlString, IntPtr.Zero);
        if (cfDestination == IntPtr.Zero) {
            CFRelease(proxySettings);
            CFRelease(cfUrlString);
            return Task.FromResult(default(WebProxy));
        }

        var proxies = CopyProxiesForURL(cFNetworkHandle, cfDestination, proxySettings);
        CFRelease(proxySettings);
        CFRelease(cfDestination);
        CFRelease(cfUrlString);

        if (CFArrayGetCount(proxies) == 0) {
            CFRelease(proxies);
            return Task.FromResult(default(WebProxy));
        }

        var proxy = CFArrayGetValueAtIndex(proxies, 0);
        var proxyKeyValue = CFDictionaryGetValue(proxy, kCFProxyTypeKey);
        if (proxyKeyValue == kCFProxyTypeNone) {
            CFRelease(proxies);
            return Task.FromResult(default(WebProxy));
        }

        proxyKeyValue = CFDictionaryGetValue(proxy, kCFProxyHostNameKey);
        var hostUrlString = GetCString(proxyKeyValue);
        proxyKeyValue = CFDictionaryGetValue(proxy, kCFProxyPortNumberKey);
        var port = 0;
        if (!CFNumberGetValue(proxyKeyValue, kCFNumberIntType, &port)) {
            CFRelease(proxies);
            return Task.FromResult(default(WebProxy));
        }

        CFRelease(proxies);
        return Task.FromResult<WebProxy?>(new WebProxy(new Uri($"{hostUrlString}:{port}")));
    }

    private static IntPtr LoadCFNetwork() => 
        LoadLibrary(File.Exists(CFNetworkLibraryOld) ? CFNetworkLibraryOld : CFNetworkLibrary);

    private static IntPtr LoadLibrary(string libPath)
    {
        var libHandle = dlopen(libPath, 0);
        return libHandle == IntPtr.Zero ? throw new DllNotFoundException($"Unable to find or open library at {libPath}") : libHandle;
    }

    private static IntPtr GetPointer(IntPtr libHandle, string symbolName, string? libPath = null)
    {
        var symbolHandle = dlsym(libHandle, symbolName);
        return symbolHandle == IntPtr.Zero ? throw new EntryPointNotFoundException($"Unable to find the symbol {symbolName} in {libPath ?? libHandle.ToString()}") : symbolHandle;
    }

    private static T GetDelegate<T>(IntPtr libHandle)
    {
        var symbolHandle = GetPointer(libHandle, typeof(T).Name);
        return Marshal.GetDelegateForFunctionPointer<T>(symbolHandle);
    }

    private static IntPtr ReadCFNetworkPointer(string symbolName)
    {
        var libHandle = LoadCFNetwork();
        var symbolHandle = GetPointer(libHandle, symbolName, "CFNetwork");
        return Marshal.ReadIntPtr(symbolHandle);
    }

    private static string? GetCString(IntPtr /* CFStringRef */ theString)
    {
        var pointer = CFStringGetCStringPtr(theString, kCFStringEncodingASCII);
        return Marshal.PtrToStringAnsi(pointer);
    }

    private static IntPtr CopySystemProxySettings(IntPtr cFNetworkHandle)
    {
        return GetDelegate<CFNetworkCopySystemProxySettings>(cFNetworkHandle)();
    }

    private static IntPtr CopyProxiesForURL(IntPtr cFNetworkHandle, IntPtr url, IntPtr proxySettings)
    {
        return GetDelegate<CFNetworkCopyProxiesForURL>(cFNetworkHandle)(url, proxySettings);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate /* CFDictionaryRef __nullable */ IntPtr CFNetworkCopySystemProxySettings();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate /* CFArrayRef __nonnull */ IntPtr CFNetworkCopyProxiesForURL(/* CFURLRef __nonnull */ IntPtr url, /* CFDictionaryRef __nonnull */ IntPtr proxySettings);

    [LibraryImport(CoreFoundationLibrary)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static unsafe partial bool CFNumberGetValue(IntPtr /* CFNumberRef */ number, int /* CFNumberType */ theType, void* valuePtr);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial IntPtr /* const char* */ CFStringGetCStringPtr(IntPtr /* CFStringRef */ theString, uint /* CFStringEncoding */ encoding);

    [LibraryImport(libSystemLibrary)]
    private static partial IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)]string symbol);

    [LibraryImport(libSystemLibrary)]
    private static partial IntPtr dlopen([MarshalAs(UnmanagedType.LPUTF8Str)]string path, int mode);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial /* void* */ IntPtr CFDictionaryGetValue(/* CFDictionaryRef */ IntPtr theDict, /* const void* */ IntPtr key);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial /* void* */ IntPtr CFArrayGetValueAtIndex(/* CFArrayRef */ IntPtr theArray, /* CFIndex */ long idx);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial /* CFIndex */ long CFArrayGetCount(/* CFArrayRef */ IntPtr theArray);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial void CFRelease(IntPtr obj);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial /* CFURLRef */ IntPtr CFURLCreateWithString(/* CFAllocatorRef */ IntPtr allocator,
        /* CFStringRef */ IntPtr URLString,
        /* CFStringRef */ IntPtr baseURL);

    [LibraryImport(CoreFoundationLibrary)]
    private static partial /* CFStringRef */ IntPtr CFStringCreateWithCString(/* CFAllocatorRef */ IntPtr alloc, 
        [MarshalAs(UnmanagedType.LPStr)]string cStr, uint encoding);
}
