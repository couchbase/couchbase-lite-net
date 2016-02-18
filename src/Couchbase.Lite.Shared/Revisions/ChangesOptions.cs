//
// ChangesOptions.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
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
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//
using System;
using Couchbase.Lite;

namespace Couchbase.Lite.Internal
{
    /// <summary>Options for _changes feed</summary>
    internal struct ChangesOptions
    {

        public bool Descending { get; set; }

        public static ChangesOptions Default
        {
            get {
                return new ChangesOptions {
                    Limit = Int32.MaxValue,
                    SortBySequence = true,
                };
            }
        }

        public int Limit { get; set; }

        public bool IncludeConflicts { get; set; }

        public bool IncludeDocs { get; set; }

        public bool SortBySequence { get; set; }

        public DocumentContentOptions ContentOptions { get; set; }

        public override string ToString()
        {
            return string.Format("[ChangesOptions: Descending={0}, Limit={1}, IncludeConflicts={2}, IncludeDocs={3},{6}" +
                "SortBySequence={4}, ContentOptions={5}]", Descending, Limit, IncludeConflicts, IncludeDocs, 
                SortBySequence, ContentOptions, Environment.NewLine);
        }
    }
}
