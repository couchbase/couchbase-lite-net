//
// ServiceBrowser.cs
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
using System.Collections.Generic;
using Mono.Zeroconf.Providers;

namespace Mono.Zeroconf
{
    public class ServiceBrowser : IServiceBrowser
    {
        private IServiceBrowser browser;
        
        public ServiceBrowser ()
        {
            browser = (IServiceBrowser)Activator.CreateInstance (ProviderFactory.SelectedProvider.ServiceBrowser);
        }
        
        public void Dispose ()
        {
            browser.Dispose ();
        }
        
        public void Browse (uint interfaceIndex, AddressProtocol addressProtocol, string regtype, string domain)
        {
            browser.Browse (interfaceIndex, addressProtocol, regtype, domain ?? "local");
        }
        
        public void Browse (uint interfaceIndex, string regtype, string domain)
        {
            Browse (interfaceIndex, AddressProtocol.Any, regtype, domain);
        }
        
        public void Browse (AddressProtocol addressProtocol, string regtype, string domain)
        {
            Browse (0, addressProtocol, regtype, domain);
        }
        
        public void Browse (string regtype, string domain)
        {
            Browse (0, AddressProtocol.Any, regtype, domain);
        }
        
        public IEnumerator<IResolvableService> GetEnumerator ()
        {
            return browser.GetEnumerator ();
        }
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
        {
            return browser.GetEnumerator ();
        }
        
        public event ServiceBrowseEventHandler ServiceAdded {
            add { browser.ServiceAdded += value; }
            remove { browser.ServiceRemoved -= value; }
        }
        
        public event ServiceBrowseEventHandler ServiceRemoved {
            add { browser.ServiceRemoved += value; }
            remove { browser.ServiceRemoved -= value; }
        }
    }
}
