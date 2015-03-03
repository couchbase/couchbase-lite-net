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
using Mono.Zeroconf.Providers;

namespace Mono.Zeroconf
{
    public class RegisterService : IRegisterService
    {
        private IRegisterService register_service;
        
        public RegisterService()
        {
            register_service = (IRegisterService)Activator.CreateInstance(
                ProviderFactory.SelectedProvider.RegisterService);
        }
        
        public void Register()
        {
            register_service.Register();
        }

        public void Unregister()
        {
            register_service.Unregister();
        }
        
        public void Dispose()
        {
            register_service.Dispose();
        }
        
        public event RegisterServiceEventHandler Response {
            add { register_service.Response += value; }
            remove { register_service.Response -= value; }
        }
        
        public string Name {
            get { return register_service.Name; }
            set { register_service.Name = value; }
        }
        
        public string RegType {
            get { return register_service.RegType; }
            set { register_service.RegType = value; }
        }
        
        public string ReplyDomain {
            get { return register_service.ReplyDomain; }
            set { register_service.ReplyDomain = value; }
        }
        
        public ITxtRecord TxtRecord { 
            get { return register_service.TxtRecord; }
            set { register_service.TxtRecord = value; }
        }
        
        public short Port {
            get { return register_service.Port; }
            set { register_service.Port = value; }
        }
        
        public ushort UPort {
            get { return register_service.UPort; }
            set { register_service.UPort = value; }
        }
    }
}
