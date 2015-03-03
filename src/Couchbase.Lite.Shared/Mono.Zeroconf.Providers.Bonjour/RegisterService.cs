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

using System;
using System.Net;
using System.Threading;
using System.Runtime.InteropServices;

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal sealed class RegisterService : Service, IRegisterService, IDisposable
    {
        private Thread thread;
        private ServiceRef sd_ref;
        private bool auto_rename = true;
    
        private Native.DNSServiceRegisterReply register_reply_handler;
    
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
            
            ServiceError error = Native.DNSServiceRegister(out sd_ref, 
                auto_rename ? ServiceFlags.None : ServiceFlags.NoAutoRename, InterfaceIndex,
                Name, RegType, ReplyDomain, HostTarget, (ushort)IPAddress.HostToNetworkOrder((short)port), txt_rec_length, txt_rec,
                register_reply_handler, IntPtr.Zero);

            if(error != ServiceError.NoError) {
                throw new ServiceErrorException(error);
            }
            
            sd_ref.Process();
        }
        
        public void Dispose()
        {
            if(thread != null) {
                thread.Abort();
                thread = null;
            }
            
            sd_ref.Deallocate();
        }
        
        private void OnRegisterReply(ServiceRef sdRef, ServiceFlags flags, ServiceError errorCode,
            string name, string regtype, string domain, IntPtr context)
        {
            RegisterServiceEventArgs args = new RegisterServiceEventArgs();
            
            args.Service = this;
            args.IsRegistered = false;
            args.ServiceError = (ServiceErrorCode)errorCode;
            
            if(errorCode == ServiceError.NoError) {
                Name = name;
                RegType = regtype;
                ReplyDomain = domain;
                args.IsRegistered = true;
            }
            
            RegisterServiceEventHandler handler = Response;
            if(handler != null) {
                handler(this, args);
            }
        }
        
        public bool AutoRename {
            get { return auto_rename; }
            set { auto_rename = value; }
        }
    }
}
