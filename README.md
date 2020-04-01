Couchbase Lite for .NET [![GitHub release](https://img.shields.io/nuget/v/Couchbase.Lite.svg?style=plastic)]() [![Coverage Status](https://coveralls.io/repos/github/couchbase/couchbase-lite-net/badge.svg?branch=master)](https://coveralls.io/github/couchbase/couchbase-lite-net?branch=master)
==================

Couchbase Lite is a lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.

This is the source repo of Couchbase Lite C#.  The main supported platforms are .NET Core Windows, .NET Core macOS, .NET Core CentOS, .NET Core Ubuntu, UWP, Xamarin iOS, and Xamarin Android.

## Documentation Overview

* [Official Documentation](https://docs.couchbase.com/couchbase-lite/current/index.html)
* API References - See the release notes for each release on this repo

## Getting Couchbase Lite

The following package IDs will be on Nuget for the 2.0 release and beyond
- Couchbase.Lite
- Couchbase.Lite.Support.UWP
- Couchbase.Lite.Support.Android
- Couchbase.Lite.Support.iOS

There will be developer builds compiled every two weeks, and available by adding `http://mobile.nuget.couchbase.com/nuget/Developer/` to your Nuget package sources.

## Building Couchbase Lite master branch from source

Please use `git` to clone the repo, rather than downloading it from a zip file.  This ensures two things:  that I will always know by looking at the logs which git commit you built your source from if you file a bug report, and that you will be able to pull the appropriate submodules.  After you clone the repo, or change branches, be sure to update the submodules with this command `git submodule update --init --recursive`

# Native Components Needed

You will notice that Couchbase.Lite, and each of the support projects make references to some missing native libraries.  These need to be built or [TODO]downloaded.  The native library project can be found in the `vendor/couchbase-lite-core` directory.  It uses CMake, and includes various build scripts in the `build_cmake/scripts` folder.  Note:  Building for Android on Windows is not supported.  For some platforms you will need to install some prerequisites, and for any build system you will need CMake available.  More info about this can be found at the [LiteCore](https://github.com/couchbase/couchbase-lite-core/) repo.

# Once you build native

You can build Couchbase Lite using either of the following:

* Visual Studio 2017 or later.
* Xamarin Studio 6.2 or later.

There is one solution file with everything needed to build Couchbase Lite for any platform we support, and it includes the following projects:

* Couchbase.Lite - The .NET Standard base Couchbase Lite library
* Couchbase.Lite.Support.* - The support classes that inject support classes for a particular platform

If you simply build the solution file that will cause a build of *all* projects by default.  You can change which projects get built in the solution options of Xamarin Studio, or the Configuration Manager of Visual Studio.  Alternatively, you could build the projects you want instead of the whole solution.

## Other Notes

* [About repo branches](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/Branches.md)
* [Code style guidelines](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/StyleGuidelines.md)

## Metrics
[![Stories in Progress](https://badge.waffle.io/couchbase/couchbase-lite-net.png?label=ready&title=In%20Progress)](https://waffle.io/couchbase/couchbase-lite-net)
[![Stories in Backlog](https://badge.waffle.io/couchbase/couchbase-lite-net.png?label=backlog&title=Backlogged)](https://waffle.io/couchbase/couchbase-lite-net)<br>
[![Throughput Graph](http://graphs.waffle.io/couchbase/couchbase-lite-net/throughput.svg)](https://waffle.io/couchbase/couchbase-lite-net/metrics)

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg?style=plastic)](https://opensource.org/licenses/Apache-2.0)
