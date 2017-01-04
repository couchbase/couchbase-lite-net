//
// AssemblyInfo.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System.Runtime.CompilerServices;
using System.Reflection;


[assembly: AssemblyTitle ("Couchbase.Lite.Storage.ForestDB")]
[assembly: AssemblyDescription("Plugin for using ForestDB with Couchbase Lite")]
[assembly: AssemblyCompany("Couchbase, Inc")]
[assembly: AssemblyCopyright ("2016")]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

[assembly: InternalsVisibleTo("Couchbase.Lite.Net45.Tests")]
[assembly: InternalsVisibleTo("Couchbase.Lite.Android.Tests")]
[assembly: InternalsVisibleTo("CouchbaseLiteiOSTests")]
[assembly: InternalsVisibleTo("Couchbase.Lite.Net35.Tests")]
[assembly: InternalsVisibleTo("Couchbase.Lite.Unity.Tests")]

