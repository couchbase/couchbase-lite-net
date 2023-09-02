Couchbase Lite for .NET [![GitHub release](https://img.shields.io/nuget/v/Couchbase.Lite.svg?style=plastic)]() [![Coverage Status](https://coveralls.io/repos/github/couchbase/couchbase-lite-net/badge.svg?branch=master)](https://coveralls.io/github/couchbase/couchbase-lite-net?branch=master)
==================

Couchbase Lite is a lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.

This is the source repo of Couchbase Lite C#.  The main supported platforms are .NET 6 Desktop (Linux will run anywhere, but is most tested on CentOS and Ubuntu), .NET 7 iOS, .NET 7 Android, .NET 7 Mac Catalyst, UWP, Xamarin iOS, and Xamarin Android.

## Documentation Overview

* [Official Documentation](https://docs.couchbase.com/couchbase-lite/current/index.html)
* API References - See the release notes for each release on this repo

## Getting Couchbase Lite

The following package IDs will be on Nuget for the 2.0 release and beyond (with Enterprise equivalents)
- Couchbase.Lite
- Couchbase.Lite.Support.UWP
- Couchbase.Lite.Support.Android
- Couchbase.Lite.Support.iOS

The following package ID was added in 3.1
- Couchbase.Lite.Support.WinUI

# Native Components Needed

You will notice that Couchbase.Lite, and each of the support projects make references to some missing native libraries.  These need to be built ahead of time.  For that you will need to check out [LiteCore](https://github.com/couchbase/couchbase-lite-core/).  You can look at the "ce" hash in core_version.ini in the root of this repo, or use `src/build/get_litecore_source.py` if you want to have a script do it.  LiteCore uses CMake, and includes various build scripts in the `build_cmake/scripts` folder.  Note:  Building for Android on Windows is not supported.  For some platforms you will need to install some prerequisites, and for any build system you will need CMake available.  More info about this can be found at the [LiteCore](https://github.com/couchbase/couchbase-lite-core/) repo.

There are a lot of platforms that .NET has to run on, and this makes building the entire library quite annoying if you don't have the proper toolchains.  However, there are a few tricks to make this easier.  If you are only interesting in a certain platform, you can remove the others from the `TargetFrameworks` in Couchbase.Lite.csproj before building, and then you won't need to worry about so many native libs.  Alternatively, you can put empty files, or copy files out of existing nuget packages to use as placeholders.  Be warned, however, that the LiteCore API often changes and so you might not be able to use an existing build as-is (though if you are only using it for placeholders on platforms that you will not run, then it doesn't matter).

The project expects these native components to exist in the `vendor/prebuilt_core` directory with the following structure:

- `windows/x86_64/bin/LiteCore.{dll,pdb}`
- `macos/lib/libLiteCore.dylib` (fat mach-o)
- `linux/x86_64/lib/libLiteCore.so`
  - There are many other libraries in here (icu / stdc++).  These rarely change and can usually be copied from an existing nupkg
- `prebuilt_core\ios\LiteCore.xcframework`
  - Remove the mac catalyst portion before attempting to build.  It contains symlinks that cause nuget.exe to fail
- `prebuilt_core\ios\couchbase-lite-core-ios.zip`
  - A zipped version of the full LiteCore.xcframework with mac catalyst included
- `prebuilt_core\android\<arch>\lib\libLiteCore.so`

# Once you build native

You can build Couchbase Lite using either of the following:

* Visual Studio 2022 or later

There is one solution file with everything needed to build Couchbase Lite for any platform we support, and it includes the following projects:

* Couchbase.Lite - The .NET Standard base Couchbase Lite library
* Couchbase.Lite.Support.* - The support classes that inject support classes for a particular platform

If you simply build the solution file that will cause a build of *all* projects by default.  You can change which projects get built in the Configuration Manager of Visual Studio.  Alternatively, you could build the projects you want instead of the whole solution.

## Other Notes

* [About repo branches](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/Branches.md)
* [Code style guidelines](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/StyleGuidelines.md)

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg?style=plastic)](https://opensource.org/licenses/Apache-2.0)
