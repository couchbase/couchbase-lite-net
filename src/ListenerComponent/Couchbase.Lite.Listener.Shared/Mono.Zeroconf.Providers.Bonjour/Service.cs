//
// Service.cs
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

using Mono.Zeroconf;

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal abstract class Service : IService
    {
        protected ServiceFlags flags = ServiceFlags.None;
        protected string name;
        protected string reply_domain;
        protected string regtype;
        protected uint interface_index;
        protected AddressProtocol address_protocol;
        
        protected ITxtRecord txt_record;
        protected string fullname;
        protected string hosttarget;
        protected ushort port;
        protected IPHostEntry hostentry;
        
        public Service()
        {
        }
        
        public Service(string name, string replyDomain, string regtype)
        {
            Name = name;
            ReplyDomain = replyDomain;
            RegType = regtype;
        }
        
        public override bool Equals(object o)
        {
            if(!(o is Service)) {
                return false;
            }
            
            return (o as Service).Name == Name;
        }
        
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        
        public ServiceFlags Flags {
            get { return flags; }
            internal set { flags = value; }
        }
        
        public uint InterfaceIndex {
            get { return interface_index; }
            set { interface_index = value; }
        }

        public AddressProtocol AddressProtocol {
            get { return address_protocol; }
            set { address_protocol = value; }
        }
        
        public string Name {
            get { return name; }
            set { name = value; }
        }
        
        public string ReplyDomain {
            get { return reply_domain; }
            set { reply_domain = value; }
        }
        
        public string RegType {
            get { return regtype; }
            set { regtype = value; }
        }
        
        // Resolved Properties
         
        public ITxtRecord TxtRecord {
            get { return txt_record; }
            set { txt_record = value; }
        }
               
        public string FullName { 
            get { return fullname; }
            internal set { fullname = value; }
        }
        
        public string HostTarget {
            get { return hosttarget; }
        }
        
        public IPHostEntry HostEntry {
            get { return hostentry; }
        }

        public uint NetworkInterface {
            get { return interface_index; }
        }
                
        public short Port {
            get { return (short)UPort; }
            set { UPort = (ushort)value; }
        }

        public ushort UPort {
            get { return port; }
            set { port = value; }
        }
    }
}
