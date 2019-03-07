// 
//  MacProxy.cs
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
#if NETFRAMEWORK || NETCOREAPP
using System;
using System.Net;
using System.Runtime.InteropServices;

using Couchbase.Lite.DI;

namespace Couchbase.Lite.Support
{
    internal sealed class MacProxy : IProxy
    {
        private const string libSystemLibrary = "/usr/lib/libSystem.dylib";

        private const string CFNetworkLibrary =
            "/System/Library/Frameworks/CoreServices.framework/Frameworks/CFNetwork.framework/CFNetwork";

        private const string CoreFoundationLibrary =
            "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private static readonly IntPtr kCFProxyTypeKey = GetPointer(CFNetworkLibrary, nameof(kCFProxyTypeKey));
        private static readonly IntPtr kCFProxyTypeNone = GetPointer(CFNetworkLibrary, nameof(kCFProxyTypeNone));
        private static readonly IntPtr kCFProxyHostNameKey = GetPointer(CFNetworkLibrary, nameof(kCFProxyHostNameKey));
        private static readonly IntPtr kCFProxyPortNumberKey  = GetPointer(CFNetworkLibrary, nameof(kCFProxyPortNumberKey));

        private static readonly uint kCFStringEncodingASCII = 0x0600;

        private static readonly int kCFNumberIntType = 9;

        public unsafe IWebProxy CreateProxy(Uri destination)
        {
            var proxySettings = CFNetworkCopySystemProxySettings();
            if (proxySettings == IntPtr.Zero) {
                return null;
            }

            var cfUrlString = CFStringCreateWithCString(IntPtr.Zero, destination.AbsoluteUri,
                kCFStringEncodingASCII);
            if (cfUrlString == IntPtr.Zero) {
                CFRelease(proxySettings);
                return null;
            }

            var cfDestination = CFURLCreateWithString(IntPtr.Zero, cfUrlString, IntPtr.Zero);
            if (cfDestination == IntPtr.Zero) {
                CFRelease(proxySettings);
                CFRelease(cfUrlString);
                return null;
            }

            var proxies = CFNetworkCopyProxiesForURL(cfDestination, proxySettings);
            CFRelease(proxySettings);
            CFRelease(cfDestination);
            CFRelease(cfUrlString);

            if (CFArrayGetCount(proxies) == 0) {
                CFRelease(proxies);
                return null;
            }

            var proxy = CFArrayGetValueAtIndex(proxies, 0);
            var proxyKeyValue = CFDictionaryGetValue(proxy, kCFProxyTypeKey);
            if (proxyKeyValue == kCFProxyTypeNone) {
                CFRelease(proxies);
                return null;
            }

            proxyKeyValue = CFDictionaryGetValue(proxy, kCFProxyHostNameKey);
            var hostUrlString = GetCString(proxyKeyValue);
            proxyKeyValue = CFDictionaryGetValue(proxy, kCFProxyPortNumberKey);
            var port = 0;
            if(!CFNumberGetValue(proxyKeyValue, kCFNumberIntType, &port)) {
                CFRelease(proxies);
                return null;
            }

            CFRelease(proxies);
            return new WebProxy(new Uri($"{hostUrlString}:{port}"));
        }

        private static IntPtr GetPointer(string libPath, string symbolName)
        {
            var libHandle = dlopen(libPath, 0);
            if (libHandle == IntPtr.Zero) {
                throw new DllNotFoundException($"Unable to find or open library at {libPath}");
            }

            var indirect = dlsym(libHandle, symbolName);
            if (indirect == IntPtr.Zero) {
                throw new EntryPointNotFoundException($"Unable to find the symbol {symbolName} in {libPath}");
            }

            return Marshal.ReadIntPtr(indirect);
        }

        private static string GetCString(IntPtr /* CFStringRef */ theString)
        {
            var pointer = CFStringGetCStringPtr(theString, kCFStringEncodingASCII);
            return Marshal.PtrToStringAnsi(pointer);
        }

        [DllImport(CoreFoundationLibrary)]
        private static extern unsafe bool CFNumberGetValue(IntPtr /* CFNumberRef */ number, int /* CFNumberType */ theType, void *valuePtr);

        [DllImport(CoreFoundationLibrary)]
        private static extern IntPtr /* const char* */ CFStringGetCStringPtr(IntPtr /* CFStringRef */ theString, uint /* CFStringEncoding */ encoding);

        [DllImport (libSystemLibrary)]
        private static extern IntPtr dlsym (IntPtr handle, string symbol);

        [DllImport (libSystemLibrary)]
        private static extern IntPtr dlopen (string path, int mode);

        [DllImport (CoreFoundationLibrary)]
        private static extern /* void* */ IntPtr CFDictionaryGetValue (/* CFDictionaryRef */ IntPtr theDict, /* const void* */ IntPtr key);

        [DllImport (CoreFoundationLibrary)]
        private static extern /* void* */ IntPtr CFArrayGetValueAtIndex (/* CFArrayRef */ IntPtr theArray, /* CFIndex */ long idx);

        [DllImport (CoreFoundationLibrary)]
        private static extern /* CFIndex */ long CFArrayGetCount (/* CFArrayRef */ IntPtr theArray);

        [DllImport (CFNetworkLibrary)]
        private static extern /* CFArrayRef __nonnull */ IntPtr CFNetworkCopyProxiesForURL (
            /* CFURLRef __nonnull */ IntPtr url, 
            /* CFDictionaryRef __nonnull */ IntPtr proxySettings);

        [DllImport(CFNetworkLibrary)]
        private static extern /* CFDictionaryRef __nullable */ IntPtr CFNetworkCopySystemProxySettings ();

        [DllImport (CoreFoundationLibrary)]
        private static extern void CFRelease (IntPtr obj);

        [DllImport (CoreFoundationLibrary)]
        private static extern /* CFURLRef */ IntPtr CFURLCreateWithString (/* CFAllocatorRef */ IntPtr allocator, 
            /* CFStringRef */ IntPtr URLString, 
            /* CFStringRef */ IntPtr baseURL);
        
        [DllImport (CoreFoundationLibrary)]
        private static extern /* CFStringRef */ IntPtr CFStringCreateWithCString(/* CFAllocatorRef */ IntPtr  alloc, string cStr, uint encoding);
    }
}
#endif