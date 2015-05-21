//
// RegisterService.cs
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
using System.Threading;
using System.Runtime.InteropServices;
using Couchbase.Lite.Util;

#if __IOS__
using AOT = ObjCRuntime;
#endif

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal sealed class RegisterService : Service, IRegisterService, IDisposable
    {
        private Thread thread;
        private ServiceRef sd_ref;
        private bool auto_rename = true;
    
        private Native.DNSServiceRegisterReply register_reply_handler;
        private GCHandle _self;
    
        public event RegisterServiceEventHandler Response;
    
        public RegisterService()
        {
            SetupCallback();
        }
        
        public RegisterService(string name, string replyDomain, string regtype) : base(name, replyDomain, regtype)
        {
            SetupCallback();
        }
        
        private void SetupCallback()
        {
            register_reply_handler = new Native.DNSServiceRegisterReply(OnRegisterReply);
        }
        
        public void Register()
        {
            Register(true);
        }

        public void Unregister()
        {
            Dispose();
        }
    
        public void Register(bool @async)
        {
            if(thread != null) {
                throw new InvalidOperationException("RegisterService registration already in process");
            }
            
            if(@async) {
                thread = new Thread(new ThreadStart(ThreadedRegister));
                thread.IsBackground = true;
                thread.Start();
            } else {
                ProcessRegister();
            }
        }
        
        public void RegisterSync()
        {
            Register(false);
        }
    
        private void ThreadedRegister()
        {
            try {
                ProcessRegister();
            } catch(ThreadAbortException) {
                Thread.ResetAbort();
                Log.D("RegisterService", "Register thread aborted");
            }
            
            thread = null;
        }
    
        public void ProcessRegister()
        {
            ushort txt_rec_length = 0;
            byte [] txt_rec = null;
            
            if(TxtRecord != null) {
                txt_rec_length = ((TxtRecord)TxtRecord.BaseRecord).RawLength;
                txt_rec = new byte[txt_rec_length];
                Marshal.Copy(((TxtRecord)TxtRecord.BaseRecord).RawBytes, txt_rec, 0, txt_rec_length);
            }

            _self = GCHandle.Alloc(this);
            ServiceError error = Native.DNSServiceRegister(out sd_ref, 
                auto_rename ? ServiceFlags.None : ServiceFlags.NoAutoRename, InterfaceIndex,
                Name, RegType, ReplyDomain, HostTarget, (ushort)IPAddress.HostToNetworkOrder((short)Port), txt_rec_length, txt_rec,
                register_reply_handler, GCHandle.ToIntPtr(_self));

            if(error != ServiceError.NoError) {
                throw new ServiceErrorException(error);
            }

            sd_ref.Process(ServiceParams.Timeout.Add(ServiceParams.Timeout));
        }
        
        public void Dispose()
        {
            _self.Free();
            sd_ref.Deallocate();

            if (thread != null) {
                thread.Abort();
                thread = null;
            }
        }

        #if __IOS__ || __UNITY_APPLE__
        [AOT.MonoPInvokeCallback(typeof(Native.DNSServiceRegisterReply))]
        #endif
        private static void OnRegisterReply(ServiceRef sdRef, ServiceFlags flags, ServiceError errorCode,
            string name, string regtype, string domain, IntPtr context)
        {
            var handle = GCHandle.FromIntPtr(context);
            var registerService = handle.Target as RegisterService;
            RegisterServiceEventArgs args = new RegisterServiceEventArgs();
            
            args.Service = registerService;
            args.IsRegistered = false;
            args.ServiceError = (ServiceErrorCode)errorCode;
            
            if(errorCode == ServiceError.NoError) {
                registerService.Name = name;
                registerService.RegType = regtype;
                registerService.ReplyDomain = domain;
                args.IsRegistered = true;
            }
            
            RegisterServiceEventHandler handler = registerService.Response;
            if(handler != null) {
                handler(registerService, args);
            }
        }
        
        public bool AutoRename {
            get { return auto_rename; }
            set { auto_rename = value; }
        }
    }
}
