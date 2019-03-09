// 
//  CouchbaseDependencyAttribute.cs
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
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Lite.DI
{
    /// <summary>
    /// An attribute to indicate that the specified class implements a dependency for
    /// Couchbase Lite (e.g. <see cref="IDefaultDirectoryResolver"/>
    /// </summary>
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CouchbaseDependencyAttribute : Attribute
    {
        #region Properties

        /// <summary>
        /// Gets or sets if the dependency should be created when it is
        /// first requested (<c>true</c>) or immediately upon registration.
        /// </summary>
        public bool Lazy { get; set; }

        /// <summary>
        /// Gets or sets if the dependency is transient (i.e. should be created
        /// on each request)
        /// </summary>
        public bool Transient { get; set; }

        #endregion
    }
}