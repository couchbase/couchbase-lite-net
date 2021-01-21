﻿using System.Resources;
using System.Reflection;
using System.Runtime.CompilerServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
/*[assembly: AssemblyTitle("Couchbase.Lite")]
[assembly: AssemblyDescription("A lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Couchbase, Inc.")]
[assembly: AssemblyProduct("Couchbase Lite .NET")]
[assembly: AssemblyCopyright("Copyright ©  2021")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: NeutralResourcesLanguage("en")]*/

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
#if false
[assembly: InternalsVisibleTo("Couchbase.Lite.Support.Net5,PublicKey=002400000c800000940000000602000000240000525341310004000001000100df5890b688df55bfd31409e7dc9be89984bfba0ae2d99a429c6490d16b53d029dd09b741d1e15fdbf5f056e235fd8e17b51775a4075c37aca89e558d4022c2e037bc05fc54808d15d926ebdb97c1bbf063c0dbd20e81550a72f2641f3b63ed76c446750e7b35aaeb52b444336a008939b3ab70fca8bcc3b62c5978d8800069f1")]
[assembly: InternalsVisibleTo("Couchbase.Lite.Tests.Net5,PublicKey=002400000c800000940000000602000000240000525341310004000001000100df5890b688df55bfd31409e7dc9be89984bfba0ae2d99a429c6490d16b53d029dd09b741d1e15fdbf5f056e235fd8e17b51775a4075c37aca89e558d4022c2e037bc05fc54808d15d926ebdb97c1bbf063c0dbd20e81550a72f2641f3b63ed76c446750e7b35aaeb52b444336a008939b3ab70fca8bcc3b62c5978d8800069f1")]
#else
[assembly: InternalsVisibleTo("Couchbase.Lite.Tests.Net5")]
[assembly: InternalsVisibleTo("Couchbase.Lite.TestsCoverage")]
#endif