//
// IValidationContext.cs
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
using System.Collections.Generic;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite.Portable
{

    /// <summary>
    /// Context passed to a <see cref="Couchbase.Lite.ValidateDelegate"/>.
    /// </summary>
    public partial interface IValidationContext {

    #region Instance Members
        //Properties
        /// <summary>
        /// Gets the current <see cref="Couchbase.Lite.Revision"/> of the <see cref="Couchbase.Lite.Document"/>, 
        /// or null if this is a new <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <value>The current revision.</value>
        ISavedRevision CurrentRevision { get; }

        /// <summary>
        /// Gets the keys whose values have changed between the current and new <see cref="Couchbase.Lite.Revision"/>s.
        /// </summary>
        /// <value>The changed keys.</value>
        IEnumerable<String> ChangedKeys { get; }

        //Methods
        /// <summary>
        /// Rejects the new <see cref="Couchbase.Lite.Revision"/>.
        /// </summary>
        void Reject();

        /// <summary>
        /// Rejects the new <see cref="Couchbase.Lite.Revision"/>. The specified message will be included with 
        /// the resulting error.
        /// </summary>
        /// <param name="message">The message to include with the resulting error.</param>
        void Reject(String message);

        /// <summary>
        /// Calls the ValidateChangeDelegate for each key/value that has changed, passing both the old and new values. 
        /// If any delegate call returns false, the enumeration stops and false is returned, otherwise true is returned.
        /// </summary>
        /// <returns><c>false</c> if any call to the ValidateChangeDelegate, otherwise <c>true</c>.</returns>
        /// <param name="changeValidator">The delegate to use to validate each change.</param>
        Boolean ValidateChanges(ValidateChangeDelegate changeValidator);
    #endregion

    }

}
