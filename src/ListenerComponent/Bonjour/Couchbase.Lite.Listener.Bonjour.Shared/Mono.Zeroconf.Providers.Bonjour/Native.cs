//
// Native.cs
//
// Authors:
//    Aaron Bockover  <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc (http://www.novell.com)
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

using System;
using System.Runtime.InteropServices;

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal static class Native
    {
        #if __UNITY_APPLE__
        private const string DNSSD_DLL = "libc";
        #elif __UNITY_ANDROID__
        private const string DNSSD_DLL = "mdnssd";
        #elif __UNITY_LINUX__
        private const string DNSSD_DLL = "dns_sd";
        #else
        private const string DNSSD_DLL = "dnssd.dll";
        #endif
        // ServiceRef

        [DllImport(DNSSD_DLL)]
        public static extern void DNSServiceRefDeallocate(IntPtr sdRef);
        
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError DNSServiceProcessResult(IntPtr sdRef);
       
        [DllImport(DNSSD_DLL)]
        public static extern int DNSServiceRefSockFD(IntPtr sdRef);
        
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError DNSServiceCreateConnection(out ServiceRef sdRef);

        // DNSServiceBrowse
        
        public delegate void DNSServiceBrowseReply(ServiceRef sdRef, ServiceFlags flags, uint interfaceIndex,
            ServiceError errorCode, string serviceName, string regtype, string replyDomain, 
            IntPtr context);
            
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError DNSServiceBrowse(out ServiceRef sdRef, ServiceFlags flags,
            uint interfaceIndex, string regtype, string domain, DNSServiceBrowseReply callBack, 
            IntPtr context);
        
        // DNSServiceResolve
        
        public delegate void DNSServiceResolveReply(ServiceRef sdRef, ServiceFlags flags, uint interfaceIndex,
            ServiceError errorCode, string fullname, string hosttarget, ushort port, ushort txtLen, 
            IntPtr txtRecord, IntPtr context);
            
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError DNSServiceResolve(out ServiceRef sdRef, ServiceFlags flags,
            uint interfaceIndex, string name, string regtype, string domain, DNSServiceResolveReply callBack,
            IntPtr context);
        
        // DNSServiceRegister
    
        public delegate void DNSServiceRegisterReply(ServiceRef sdRef, ServiceFlags flags, ServiceError errorCode,
            string name, string regtype, string domain, IntPtr context);
    
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError DNSServiceRegister(out ServiceRef sdRef, ServiceFlags flags,
            uint interfaceIndex, string name, string regtype, string domain, string host, ushort port,
            ushort txtLen, byte [] txtRecord, DNSServiceRegisterReply callBack, IntPtr context);

        // DNSServiceQueryRecord
        
        public delegate void DNSServiceQueryRecordReply(ServiceRef sdRef, ServiceFlags flags, uint interfaceIndex,
            ServiceError errorCode, string fullname, ServiceType rrtype, ServiceClass rrclass, ushort rdlen, 
            IntPtr rdata, uint ttl, IntPtr context);
        
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError DNSServiceQueryRecord(out ServiceRef sdRef, ServiceFlags flags, 
            uint interfaceIndex, string fullname, ServiceType rrtype, ServiceClass rrclass, 
            DNSServiceQueryRecordReply callBack, IntPtr context);
        
        // TXT Record Handling
        
        [DllImport(DNSSD_DLL)]
        public static extern void TXTRecordCreate( IntPtr txtRecord, ushort bufferLen, IntPtr buffer);
    
        [DllImport(DNSSD_DLL)]
        public static extern void TXTRecordDeallocate(IntPtr txtRecord);
    
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError TXTRecordGetItemAtIndex(ushort txtLen, IntPtr txtRecord,
            ushort index, ushort keyBufLen, byte [] key, out byte valueLen, out IntPtr value);
            
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError TXTRecordSetValue(IntPtr txtRecord, byte [] key, 
            sbyte valueSize, byte [] value);
            
        [DllImport(DNSSD_DLL)]
        public static extern ServiceError TXTRecordRemoveValue(IntPtr txtRecord, byte [] key);
        
        [DllImport(DNSSD_DLL)]
        public static extern ushort TXTRecordGetLength(IntPtr txtRecord);
        
        [DllImport(DNSSD_DLL)]
        public static extern IntPtr TXTRecordGetBytesPtr(IntPtr txtRecord);
        
        [DllImport(DNSSD_DLL)]
        public static extern ushort TXTRecordGetCount(ushort txtLen, IntPtr txtRecord);
    }
}
