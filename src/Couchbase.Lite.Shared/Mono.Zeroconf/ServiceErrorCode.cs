//
// ServiceErrorCode.cs
//
// Authors:
//    Aaron Bockover  <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
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

namespace Mono.Zeroconf
{
    // These are just copied from Bonjour
    
    public enum ServiceErrorCode {
        None                = 0,
        Unknown             = -65537,       /* 0xFFFE FFFF */
        NoSuchName          = -65538,
        NoMemory            = -65539,
        BadParam            = -65540,
        BadReference        = -65541,
        BadState            = -65542,
        BadFlags            = -65543,
        Unsupported         = -65544,
        NotInitialized      = -65545,
        AlreadyRegistered   = -65547,
        NameConflict        = -65548,
        Invalid             = -65549,
        Firewall            = -65550,
        Incompatible        = -65551,        /* client library incompatible with daemon */
        BadInterfaceIndex   = -65552,
        Refused             = -65553,
        NoSuchRecord        = -65554,
        NoAuth              = -65555,
        NoSuchKey           = -65556,
        NATTraversal        = -65557,
        DoubleNAT           = -65558,
        BadTime             = -65559
    }
}
