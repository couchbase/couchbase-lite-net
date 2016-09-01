[![Stories in Ready](https://badge.waffle.io/couchbase/couchbase-lite-net.png?label=ready&title=Scheduled)](https://waffle.io/couchbase/couchbase-lite-net)
[![Stories in Progress](https://badge.waffle.io/couchbase/couchbase-lite-net.png?label=in%20progress&title=In%20Progress)](https://waffle.io/couchbase/couchbase-lite-net)
Couchbase Lite for .NET [![GitHub release](https://img.shields.io/github/release/couchbase/couchbase-lite-net.svg?style=plastic)]()
==================

Couchbase Lite is a lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.

This is the source repo of Couchbase Lite C#. It is originally a port of Couchbase Lite from Couchbase Lite Android.

## Documentation Overview

* [Official Documentation](http://developer.couchbase.com/mobile/develop/guides/couchbase-lite/index.html)

## Getting Couchbase Lite from Nuget

These are the packages that are created by this repo:  [Couchbase.Lite](https://www.nuget.org/packages/Couchbase.Lite/), [Couchbase.Lite.Listener](https://www.nuget.org/packages/Couchbase.Lite.Listener/), [Couchbase.Lite.Listener.Bonjour](https://www.nuget.org/packages/Couchbase.Lite.Listener.Bonjour/), [Couchbase.Lite.Storage.SystemSQLite](https://www.nuget.org/packages/Couchbase.Lite.Storage.SystemSQLite/), [Couchbase.Lite.Storage.SQLCipher](https://www.nuget.org/packages/Couchbase.Lite.Storage.SQLCipher/), [Couchbase.Lite.Storage.ForestDB](https://www.nuget.org/packages/Couchbase.Lite.Storage.ForestDB/).  For more information about the last three, see [StorageEngineOverview.md](https://github.com/couchbase/couchbase-lite-net/blob/release/1.2/Notes/StorageEngineOverview.md)

## Building Couchbase Lite master branch from source

Please use `git` to clone the repo, rather than downloading it from a zip file.  This ensures two things:  that I will always know by looking at the logs which git commit you built your source from if you file a bug report, and that you will be able to pull the appropriate submodules.  After you clone the repo, or change branches, be sure to update the submodules with this command `git submodule update --init --recursive`

You can build Couchbase Lite using either of the following:

* Visual Studio 2015 Pro, Premium, or Ultimate or later.
* Xamarin Studio 5 or later.

There are solution files per platform and the projects included can be identified in the following way:

\<Name>.\<Platform>

\<Name> is one of the following:

* Couchbase.Lite - The core Couchbase Lite library (required)
* Couchbase.Lite.Listener - The Couchbase Lite P2P REST API server implementation (optional)
* Couchbase.Lite.Listener.Bonjour - The optional Bonjour service discover mechanism for P2P (optional)
* storage.systemsqlite - Couchbase Lite system SQLite storage engine (optional*)
* storage.sqlcipher - Couchbase Lite SQLCipher storage engine (optional*)
* storage.forestdb - Couchbase Lite ForestDB storage engine (optional*)

\* You must have at least one of these, and it must be [properly registered at runtime](Notes/StorageEngineOverview.md)

\<Platform> is one of the following:

* Net45 - Mono / .NET 4.5
* Net35 - Mono / .NET 3.5
* iOS - iOS
* Android - Android
* Unity - Unity (either all-purpose, or "Windows Standalone" depending on the project)
* UnityAndroid - Unity Android Player
* UnityApple - Unity iOS and OS X Standalone

If you simply build the solution file that will cause a build of *all* projects by default.  You can change which projects get built in the solution options of Xamarin Studio, or the Configuration Manager of Visual Studio.  Alternatively, you could build the projects you want instead of the whole solution.  

Note that the Unity project needs rebasing, and likely does not build correctly on master.  There is a release/unity-ga branch currently tracking the state of Unity.  So far this has not been an issue, but I'd like to wait for Unity to upgrade their .NET runtime before attempting a rebase.  This will allow the removal of ***a lot*** of cruft code around ancient .NET profiles.  To build any of the Unity projects you must supply the UnityEngine.dll file to the project.  If your Unity install is in the default location, then the project will copy it as part of the build.  Otherwise, it needs to go in the src/Couchbase.Lite.Unity/vendor/Unity folder.  If you can't build the project then file an issue here.

ForestDB requires native components to be built for each platform you want to target.  Those need to be put in `src/Couchbase.Lite.Shared/vendor/cbforest/prebuilt`.  You can either download the binaries from a GitHub release page (starting with 1.2) or build them yourself.  Building instructions can be found [here](https://github.com/couchbaselabs/cbforest/blob/1.3-net/CSharp/README.md).

## Other Notes

* [About repo branches](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/Branches.md)
* [Code style guidelines](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/StyleGuidelines.md)

## Running Tests

The replication unit tests currently require a running instance of `sync_gateway`. Prior to running the replication tests, start `sync_gateway` by running the `start_gateway` script found in the root of the project

## Example Apps
* [GrocerySync](https://github.com/couchbase/couchbase-lite-net/tree/master/samples)
	* Beginner example
* [Couchbase Connect](https://github.com/FireflyLogic/couchbase-connect-14)
	* Advanced example
	
[![GitHub license](https://img.shields.io/github/license/couchbase/couchbase-lite-net.svg?style=plastic)]()
