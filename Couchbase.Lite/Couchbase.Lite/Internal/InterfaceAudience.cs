/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using Com.Couchbase.Lite.Internal;
using Sharpen;
using System;

namespace Com.Couchbase.Lite.Internal
{
	/// <summary>Annotations to help mark methods as being public or private.</summary>
	/// <remarks>
	/// Annotations to help mark methods as being public or private.  This is needed to
	/// help with the issue that Java‚Äôs scoping is not very complete. One is often forced to
	/// make a class public in order for other internal components to use it. It does not have
	/// friends or sub-package-private like C++
	/// Motivated by http://hadoop.apache.org/docs/current/hadoop-project-dist/hadoop-common/InterfaceClassification.html
	/// </remarks>
    public class InterfaceAudience
	{
        public class PublicAttribute : Attribute {

        }
	}
}
