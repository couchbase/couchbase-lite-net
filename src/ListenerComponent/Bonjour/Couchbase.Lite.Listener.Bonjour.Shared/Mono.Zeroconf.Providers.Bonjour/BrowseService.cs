//
// BrowseService.cs
//
// Authors:
//    Aaron Bockover  <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc (http://www.novell.com)
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
//
//  Modifications by Jim Borden <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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

using System;
using System.Net;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using Couchbase.Lite.Util;

#if __IOS__
using AOT = ObjCRuntime;
#endif

namespace Mono.Zeroconf.Providers.Bonjour
{
    public sealed class ServiceErrorEventArgs : EventArgs
    {
        public readonly ServiceError ErrorCode;

        public readonly string Stage;

        internal ServiceErrorEventArgs(string stage, ServiceError errorCode)
        {
            Stage = stage;
            ErrorCode = errorCode;
        }
    }

    public sealed class BrowseService : Service, IResolvableService, IDisposable
    {
        private static readonly string Tag = typeof(BrowseService).Name;
        private bool is_resolved = false;
        private bool resolve_pending = false;
        
        private Native.DNSServiceResolveReply resolve_reply_handler;
        private Native.DNSServiceQueryRecordReply query_record_reply_handler;
        private GCHandle _self;
        
        public event ServiceResolvedEventHandler Resolved
        {
            add { _resolved = (ServiceResolvedEventHandler)Delegate.Combine(_resolved, value); }
            remove { _resolved = (ServiceResolvedEventHandler)Delegate.Remove(_resolved, value); }
        }
        private event ServiceResolvedEventHandler _resolved;

        public event EventHandler<ServiceErrorEventArgs> Error;

        public BrowseService()
        {
            SetupCallbacks();
        }
        
        public BrowseService(string name, string replyDomain, string regtype) : base(name, replyDomain, regtype)
        {
            SetupCallbacks();
        }
        
        private void SetupCallbacks()
        {
            Log.To.Discovery.D(Tag, "Initializing {0}", this);
            resolve_reply_handler = new Native.DNSServiceResolveReply(OnResolveReply);
            query_record_reply_handler = new Native.DNSServiceQueryRecordReply(OnQueryRecordReply);
        }

        public void Resolve()
        {
            Resolve(false);
        }
        
        public void Resolve(bool requery)
        {
            if(resolve_pending) {
                return;
            }
    
            is_resolved = false;
            resolve_pending = true;
            
            if(requery) {
                InterfaceIndex = 0;
            }
        
            if (!_self.IsAllocated) {
                _self = GCHandle.Alloc(this);
            }

            ServiceRef sd_ref;
            Log.To.Discovery.V(Tag, "{0} preparing to enter DNSServiceResolve", this);
            ServiceError error = Native.DNSServiceResolve(out sd_ref, ServiceFlags.None, 
                InterfaceIndex, Name, RegType, ReplyDomain, resolve_reply_handler, GCHandle.ToIntPtr(_self));
                
            if(error != ServiceError.NoError) {
                Log.To.Discovery.W(Tag, "Error from DNSServiceResolve {0}", error);
                if (Error != null) {
                    Error(this, new ServiceErrorEventArgs("Resolve", error));
                    sd_ref.Deallocate();
                    return;
                }
            }

            sd_ref.ProcessSingle();
        }
        
        public void RefreshTxtRecord()
        {
            if (!_self.IsAllocated) {
                _self = GCHandle.Alloc(this);
            }

            // Should probably make this async?
        
            ServiceRef sd_ref;
            ServiceError error = Native.DNSServiceQueryRecord(out sd_ref, ServiceFlags.None, 0,
                fullname, ServiceType.TXT, ServiceClass.IN, query_record_reply_handler, GCHandle.ToIntPtr(_self));
                
            if(error != ServiceError.NoError) {
                Error(this, new ServiceErrorEventArgs("RefreshTxtRecord", error));
                sd_ref.Deallocate();
                return;
            }
            
            sd_ref.ProcessSingle(ServiceParams.Timeout);
        }

        #if __IOS__ || __UNITY_APPLE__
        [AOT.MonoPInvokeCallback(typeof(Native.DNSServiceResolveReply))]
        #endif
        private static void OnResolveReply(ServiceRef sdRef, ServiceFlags flags, uint interfaceIndex,
            ServiceError errorCode, string fullname, string hosttarget, ushort port, ushort txtLen, 
            IntPtr txtRecord, IntPtr context)
        {
            var handle = GCHandle.FromIntPtr(context);
            var browseService = handle.Target as BrowseService;
            Log.To.Discovery.V(Tag, "Resolve reply received for {0} (0x{1}), entering DNSServiceQueryRecord next", 
                browseService, sdRef.Raw.ToString("X"));
            browseService.is_resolved = true;
            browseService.resolve_pending = false;
            
            browseService.InterfaceIndex = interfaceIndex;
            browseService.FullName = fullname;
            browseService.Port = (ushort)IPAddress.NetworkToHostOrder((short)port);
            browseService.TxtRecord = new TxtRecord(txtLen, txtRecord);
            sdRef.Deallocate();
            
            // Run an A query to resolve the IP address
            ServiceRef sd_ref;
            
            if (browseService.AddressProtocol == AddressProtocol.Any || browseService.AddressProtocol == AddressProtocol.IPv4) {
                
                ServiceError error = Native.DNSServiceQueryRecord(out sd_ref, ServiceFlags.None, interfaceIndex,
                    hosttarget, ServiceType.A, ServiceClass.IN, browseService.query_record_reply_handler, context);
                
                if(error != ServiceError.NoError) {
                    Log.To.Discovery.W(Tag, "Error in DNSServiceQueryRecord {0}", error);
                    browseService.Error(browseService, new ServiceErrorEventArgs("ResolveReply (IPv4)", error));
                    sd_ref.Deallocate();
                    return;
                }
            
                sd_ref.ProcessSingle(ServiceParams.Timeout);
            }
            
            if (browseService.AddressProtocol == AddressProtocol.Any || browseService.AddressProtocol == AddressProtocol.IPv6) {
                ServiceError error = Native.DNSServiceQueryRecord(out sd_ref, ServiceFlags.None, interfaceIndex,
                    hosttarget, ServiceType.AAAA, ServiceClass.IN, browseService.query_record_reply_handler, context);
                
                if(error != ServiceError.NoError) {
                    if(error != ServiceError.NoError) {
                        Log.To.Discovery.W(Tag, "Error in DNSServiceQueryRecord {0}", error);
                        browseService.Error(browseService, new ServiceErrorEventArgs("ResolveReply (IPv6)", error));
                        sd_ref.Deallocate();
                        return;
                    }
                }
            
                sd_ref.ProcessSingle(ServiceParams.Timeout);
            }
        }
     
        #if __IOS__ || __UNITY_APPLE__
        [AOT.MonoPInvokeCallback(typeof(Native.DNSServiceQueryRecordReply))]
        #endif
        private static void OnQueryRecordReply(ServiceRef sdRef, ServiceFlags flags, uint interfaceIndex,
            ServiceError errorCode, string fullname, ServiceType rrtype, ServiceClass rrclass, ushort rdlen, 
            IntPtr rdata, uint ttl, IntPtr context)
        {
            var handle = GCHandle.FromIntPtr(context);
            var browseService = handle.Target as BrowseService;
            switch(rrtype) {
                case ServiceType.A:
                    IPAddress address;

                    if(rdlen == 4) {   
                        // ~4.5 times faster than Marshal.Copy into byte[4]
                        uint address_raw = (uint)(Marshal.ReadByte (rdata, 3) << 24);
                        address_raw |= (uint)(Marshal.ReadByte (rdata, 2) << 16);
                        address_raw |= (uint)(Marshal.ReadByte (rdata, 1) << 8);
                        address_raw |= (uint)Marshal.ReadByte (rdata, 0);

                        address = new IPAddress(address_raw);
                    } else if(rdlen == 16) {
                        byte [] address_raw = new byte[rdlen];
                        Marshal.Copy(rdata, address_raw, 0, rdlen);
                        address = new IPAddress(address_raw, interfaceIndex);
                    } else {
                        break;
                    }

                    if(browseService.hostentry == null) {
                        browseService.hostentry = new IPHostEntry();
                        browseService.hostentry.HostName = browseService.hosttarget;
                    }
                    
                    if(browseService.hostentry.AddressList != null) {
                        ArrayList list = new ArrayList(browseService.hostentry.AddressList);
                        list.Add(address);
                        browseService.hostentry.AddressList = list.ToArray(typeof(IPAddress)) as IPAddress [];
                    } else {
                        browseService.hostentry.AddressList = new IPAddress [] { address };
                    }

                    Log.To.Discovery.V(Tag, "Query record reply received for {0} (0x{1})", browseService, sdRef.Raw.ToString("X"));
                    ServiceResolvedEventHandler handler = browseService._resolved;
                    if(handler != null) {
                        handler(browseService, new ServiceResolvedEventArgs(browseService));
                    }
                    
                    break;
                case ServiceType.TXT:
                    if(browseService.TxtRecord != null) {
                        browseService.TxtRecord.Dispose();
                    }
            
                    browseService.TxtRecord = new TxtRecord(rdlen, rdata);
                    break;
                default:
                    break;
            }
            
            sdRef.Deallocate();
        }
        
        public bool IsResolved {
            get { return is_resolved; }
        }

        public override string ToString()
        {
            if (IsResolved) {
                return string.Format("BrowseService[IsResolved=True Name={0} IPAddresses={1} Port={2}]", 
                    Name, new LogJsonString(hostentry == null ? new string[0] : hostentry.AddressList.ToStringArray()), Port);
            }

            return "BrowseService[IsResolved=False]";
        }

        public void Dispose() {
            _self.Free();
        }
    }
}

