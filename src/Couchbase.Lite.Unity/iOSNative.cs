// Copyright (C) 2010 Novell, Inc (http://www.novell.com)
// Copyright 2012-2014 Xamarin Inc.
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

using MSX = Mono.Security.X509;

using System;
using System.Runtime.InteropServices;
using Couchbase.Lite.Util;
using System.IO;
using System.Collections.Generic;
using Mono.Security.X509;

namespace Couchbase.Lite.Unity {
    public static class Foo
    {
        public static void AddRootCertificate(Stream certStream)
        {
            var data = default(byte[]);
            using (var ms = new MemoryStream()) {
                certStream.CopyTo(ms);
                data = ms.ToArray();
            }
                
            var cert = new X509Certificate(data);
            Log.I("Foo", "Adding {0} (issued by {1}) to trusted roots", cert.SubjectName, cert.IssuerName);
            OSX509Certificates.TrustedRoots.Add(new X509Certificate(data));
        }
    }

    static class OSX509Certificates {
        internal static readonly List<X509Certificate> TrustedRoots = new List<X509Certificate>();
        private const string Tag = "OSX509Certificates";
        private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport (SecurityLibrary)]
        extern static IntPtr SecCertificateCreateWithData (IntPtr allocator, IntPtr nsdataRef);

        [DllImport (SecurityLibrary)]
        extern static /* OSStatus */ int SecTrustCreateWithCertificates (IntPtr certOrCertArray, IntPtr policies, out IntPtr sectrustref);

        [DllImport (SecurityLibrary)]
        extern static /* OSStatus */ int SecTrustSetAnchorCertificates(IntPtr trust, IntPtr anchorCertificates); 

        [DllImport(SecurityLibrary)]
        extern static /* OSStatus */ int SecTrustSetAnchorCertificatesOnly(IntPtr trust, [MarshalAs (UnmanagedType.I1)]bool anchorCertificatesOnly);

        [DllImport (SecurityLibrary)]
        extern static IntPtr SecPolicyCreateSSL ([MarshalAs (UnmanagedType.I1)] bool server, IntPtr cfStringHostname);

        [DllImport (SecurityLibrary)]
        extern static /* OSStatus */ int SecTrustEvaluate (IntPtr secTrustRef, out SecTrustResult secTrustResultTime);

        [DllImport (CoreFoundationLibrary, CharSet=CharSet.Unicode)]
        extern static IntPtr CFStringCreateWithCharacters (IntPtr allocator, string str, /* CFIndex */ IntPtr count);

        [DllImport (CoreFoundationLibrary)]
        unsafe extern static IntPtr CFDataCreate (IntPtr allocator, byte *bytes, /* CFIndex */ IntPtr length);

        [DllImport (CoreFoundationLibrary)]
        extern static void CFRelease (IntPtr handle);

        [DllImport (CoreFoundationLibrary)]
        extern static IntPtr CFArrayCreate (IntPtr allocator, IntPtr values, /* CFIndex */ IntPtr numValues, IntPtr callbacks);

        // uint32_t
        public enum SecTrustResult {
            Invalid,
            Proceed,
            Confirm,
            Deny,
            Unspecified,
            RecoverableTrustFailure,
            FatalTrustFailure,
            ResultOtherError,
        }

        static IntPtr MakeCFData (byte [] data)
        {
            unsafe {
                fixed (byte *ptr = &data [0])
                return CFDataCreate (IntPtr.Zero, ptr, (IntPtr) data.Length);
            }
        }

        static unsafe IntPtr FromIntPtrs (IntPtr [] values)
        {
            fixed (IntPtr* pv = values) {
                return CFArrayCreate (
                    IntPtr.Zero, 
                    (IntPtr) pv,
                    (IntPtr) values.Length,
                    IntPtr.Zero);
            }
        }

        public static SecTrustResult TrustEvaluateSsl (MSX.X509CertificateCollection certificates, string host)
        {
            if (certificates == null) {
                return SecTrustResult.Deny;
            }

            try {
                return _TrustEvaluateSsl (certificates, host);
            } catch {
                UnityEngine.Debug.LogError("Error, returning deny");
                return SecTrustResult.Deny;
            }
        }

        static SecTrustResult _TrustEvaluateSsl (MSX.X509CertificateCollection certificates, string hostName)
        {
            int certCount = certificates.Count;
            int anchorCount = TrustedRoots.Count;
            IntPtr [] cfDataPtrs = new IntPtr [certCount];
            IntPtr [] secCerts = new IntPtr [certCount];
            IntPtr[] cfDataAnchor = new IntPtr[anchorCount];
            IntPtr[] anchorCerts = new IntPtr[anchorCount];
            IntPtr certArray = IntPtr.Zero;
            IntPtr anchorArray = IntPtr.Zero;
            IntPtr sslsecpolicy = IntPtr.Zero;
            IntPtr host = IntPtr.Zero;
            IntPtr sectrust = IntPtr.Zero;
            SecTrustResult result = SecTrustResult.Deny;

            try {
                for (int i = 0; i < certCount; i++) {
                    cfDataPtrs [i] = MakeCFData (certificates [i].RawData);
                    secCerts [i] = SecCertificateCreateWithData (IntPtr.Zero, cfDataPtrs [i]);
                    if (secCerts [i] == IntPtr.Zero) {
                        return SecTrustResult.Deny;
                    }

                }

                for(int i = 0; i < anchorCount; i++) {
                    cfDataAnchor[i] = MakeCFData(TrustedRoots[i].RawData);
                    anchorCerts [i] = SecCertificateCreateWithData(IntPtr.Zero, cfDataAnchor[i]);
                }

                certArray = FromIntPtrs (secCerts);
                host = CFStringCreateWithCharacters (IntPtr.Zero, hostName, (IntPtr) hostName.Length);
                sslsecpolicy = SecPolicyCreateSSL (true, host);

                int code = SecTrustCreateWithCertificates (certArray, sslsecpolicy, out sectrust);
                if(code == 0 && TrustedRoots.Count > 0) {
                    anchorArray = FromIntPtrs (anchorCerts);
                    code = SecTrustSetAnchorCertificates(sectrust, anchorArray);
                    if(code == 0)  {
                        SecTrustSetAnchorCertificatesOnly(sectrust, true);
                    }
                }

                if (code == 0) {
                    code = SecTrustEvaluate (sectrust, out result);
                }

                UnityEngine.Debug.LogFormat("TrustEvaluateSsl returning {0} ({1})", result, (uint)result);
                return result;
            } finally {
                for (int i = 0; i < certCount; i++) {
                    if (cfDataPtrs[i] != IntPtr.Zero)
                        CFRelease(cfDataPtrs[i]);

                    if (secCerts[i] != IntPtr.Zero)
                        CFRelease(secCerts[i]);
                }

                for (int i = 0; i < anchorCount; i++) {
                    if (cfDataAnchor[i] != IntPtr.Zero)
                        CFRelease(cfDataAnchor[i]);

                    if (anchorCerts[i] != IntPtr.Zero) {
                        CFRelease(anchorCerts[i]);
                    }
                }

                if (certArray != IntPtr.Zero)
                    CFRelease (certArray);

                if (anchorArray != IntPtr.Zero)
                    CFRelease(anchorArray);

                if (sslsecpolicy != IntPtr.Zero)
                    CFRelease (sslsecpolicy);
                if (host != IntPtr.Zero)
                    CFRelease (host);
                if (sectrust != IntPtr.Zero)
                    CFRelease (sectrust);
            }
        }
    }
}
