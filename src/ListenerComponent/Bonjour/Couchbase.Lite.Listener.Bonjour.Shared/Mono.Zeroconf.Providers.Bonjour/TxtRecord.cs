//
// TxtRecord.cs
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
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using Couchbase.Lite.Util;

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal class TxtRecord : ITxtRecord
    {
        private static readonly string Tag = typeof(TxtRecord).Name;
        private IntPtr handle = IntPtr.Zero;
        private ushort length;
        private IntPtr buffer;
    
        private static readonly Encoding encoding = new ASCIIEncoding();
            
        public TxtRecord()
        {
            handle = Marshal.AllocHGlobal(16);
            Native.TXTRecordCreate(handle, 0, IntPtr.Zero);
        }
            
        public TxtRecord(ushort length, IntPtr buffer)
        {
            this.length = length;
            this.buffer = buffer;
        }
        
        public void Dispose()
        {
            if(handle != IntPtr.Zero) {
                Native.TXTRecordDeallocate(handle);
                handle = IntPtr.Zero;
            }
        }
        
        public void Add(string key, string value)
        {
            Add(new TxtRecordItem(key, value));
        }
        
        public void Add(string key, byte [] value)
        {
            Add(new TxtRecordItem(key, value));
        }
        
        public void Add(TxtRecordItem item)
        {
            if(handle == IntPtr.Zero) {
                Log.To.Discovery.E(Tag, "Attempt to call Add() on a readonly object, throwing...");
                throw new InvalidOperationException("This TXT Record is read only");
            }
        
            string key = item.Key;
            if(key[key.Length - 1] != '\0') {
                key += "\0";
            }
        
            ServiceError error = Native.TXTRecordSetValue(handle, encoding.GetBytes(key + "\0"), 
                (sbyte)item.ValueRaw.Length, item.ValueRaw);
                
            if(error != ServiceError.NoError) {
                Log.To.Discovery.E(Tag, "Got error in TXTRecordSetValue ({0}), throwing...", error);
                throw new ServiceErrorException(error);
            }        
        }
        
        public void Remove(string key)
        {
            if(handle == IntPtr.Zero) {
                Log.To.Discovery.E(Tag, "Attempt to call Remove() on a readonly object, throwing...");
                throw new InvalidOperationException("This TXT Record is read only");
            }
            
            ServiceError error = Native.TXTRecordRemoveValue(handle, encoding.GetBytes(key));
            
            if(error != ServiceError.NoError) {
                Log.To.Discovery.E(Tag, "Got error in TXTRecordRemoveValue ({0}), throwing...", error);
                throw new ServiceErrorException(error);
            }
        }
        
        public TxtRecordItem GetItemAt(int index)
        {
            byte [] key = new byte[32];
            byte value_length = 0;
            IntPtr value_raw = IntPtr.Zero;
            
            if(index < 0 || index >= Count) {
                Log.To.Discovery.E(Tag, "GetItemAt() received invalid index {0} (not between 0 and {1}), throwing...",
                    index, Count - 1);
                throw new IndexOutOfRangeException();
            }
            
            ServiceError error = Native.TXTRecordGetItemAtIndex(RawLength, RawBytes, (ushort)index, 
                (ushort)key.Length, key, out value_length, out value_raw);
                
            if(error != ServiceError.NoError) {
                Log.To.Discovery.E(Tag, "Got error in TXTRecordGetItemAtIndex ({0}), throwing...", error);
                throw new ServiceErrorException(error);
            }
            
            byte [] buffer = new byte[value_length];
            for(int i = 0; i < value_length; i++) {
                buffer[i] = Marshal.ReadByte(value_raw, i);
            }
            
            int pos = 0;
            for(; pos < key.Length && key[pos] != 0; pos++);

            return new TxtRecordItem(encoding.GetString(key, 0, pos), buffer);
        }
        
        public IEnumerator GetEnumerator()
        {
            return new TxtRecordEnumerator(this);
        }
        
        public override string ToString()
        {
            string ret = String.Empty;
            int i = 0;
            int count = Count;
            
            foreach(TxtRecordItem item in this) {
                ret += "\"" + item.ToString() + "\"";
                if(i < count - 1) {
                    ret += ", ";
                }
                
                i++;
            }
            
            return ret;
        }
        
        public TxtRecordItem this[string key] {
            get {
                foreach(TxtRecordItem item in this) {
                    if(item.Key == key) {
                        return item;
                    }
                }
                
                return null;
            }
        }
        
        public IntPtr RawBytes {
            get { return handle == IntPtr.Zero ? buffer : Native.TXTRecordGetBytesPtr(handle); }
        }
        
        public ushort RawLength {
            get { return handle == IntPtr.Zero ? length : Native.TXTRecordGetLength(handle); }
        }
                        
        public int Count {
            get { return Native.TXTRecordGetCount(RawLength, RawBytes); }
        }
        
        public ITxtRecord BaseRecord {
            get { return this; }
        }
    }
}
