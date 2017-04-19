//
//  AccessibilityModeAttribute.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
//

using System;

namespace Couchbase.Lite
{
    /// <summary>
    /// Specifies the allowed access to a property or method if an IThreadSafe interface
    /// </summary>
    public enum AccessMode
    {
        /// <summary>
        /// Access is only allowed from the owning thread
        /// of the object
        /// </summary>
        FromOwningThreadOnly,

        /// <summary>
        /// Access is allowed from anywhere
        /// </summary>
        FromAnywhere
    }

    /// <summary>
    /// An attribute indicating when a given property or method is allowed to be accessed
    /// (owning thread only vs anytime)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class AccessibilityModeAttribute : Attribute
    {
        #region Properties

        /// <summary>
        /// Gets the access mode of the property or method in question
        /// </summary>
        public AccessMode AccessMode { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mode">The access mode of the property or method in question</param>
        public AccessibilityModeAttribute(AccessMode mode)
        {
            AccessMode = mode;
        }

        #endregion
    }
}
