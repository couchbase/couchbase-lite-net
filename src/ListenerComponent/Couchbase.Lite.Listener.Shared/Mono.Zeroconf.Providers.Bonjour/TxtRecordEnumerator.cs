//
// TxtRecordEnumerator.cs
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
using System.Collections;

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal class TxtRecordEnumerator : IEnumerator
    {
        private TxtRecord record;
        private TxtRecordItem current_item;
        private int index;
        
        public TxtRecordEnumerator(TxtRecord record)
        {
            this.record = record;
        }
        
        public void Reset()
        {
            index = 0;
            current_item = null;
        }
        
        public bool MoveNext()
        {
            if(index < 0 || index >= record.Count) {
                return false;
            }
            
            current_item = record.GetItemAt(index++);
            return current_item != null;
        }
        
        public object Current {
            get { return current_item; }
        }
    }
}
