[![Stories in Ready](https://badge.waffle.io/couchbase/couchbase-lite-net.png?label=ready&title=Scheduled)](https://waffle.io/couchbase/couchbase-lite-net)
[![Stories in Progress](https://badge.waffle.io/couchbase/couchbase-lite-net.png?label=in%20progress&title=In%20Progress)](https://waffle.io/couchbase/couchbase-lite-net)
Couchbase Lite for .NET [![GitHub release](https://img.shields.io/github/release/couchbase/couchbase-lite-net.svg?style=plastic)]()
==================

Couchbase Lite is a lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.

This is the source repo of Couchbase Lite C#. It is originally a port of Couchbase Lite from Couchbase Lite Android.

## Documentation Overview

* [Official Documentation](http://developer.couchbase.com/mobile/develop/guides/couchbase-lite/index.html)

## Building Couchbase Lite master branch from source

Please use `git` to clone the repo, rather than downloading it from a zip file.  This ensures two things:  that I will always know by looking at the logs which git commit you built your source from if you file a bug report, and that you will be able to pull the appropriate submodules if you are targeting .NET 3.5 or Unity.  After you clone the repo, or change branches, be sure to update the submodules with this command `git submodule update --init --recursive`

You can build Couchbase Lite using either of the following:

* Visual Studio 2013 Pro, Premium, or Ultimate, Update 2 or later.
    * Must also have the [Shared Project Reference Manager](https://visualstudiogallery.msdn.microsoft.com/315c13a7-2787-4f57-bdf7-adae6ed54450) extension.
* Xamarin Studio 5 or later.

The solution files ending in **VS2013** are for use in Visual Studio, while the ones that do not are meant for Xamarin Studio.  There are a number of projects and platforms so here are the key parts to look for.  Currently there are three "projects" and five "platforms" and you can identify them as in the following:

\<Name>.\<Platform>

\<Name> is one of the following:

* Couchbase.Lite - The core Couchbase Lite library
* Couchbase.Lite.Listener - The Couchbase Lite P2P REST API server implementation
* Couchbase.Lite.Listener.Bonjour - The optional Bonjour service discover mechanism for P2P

\<Platform> is one of the following:

* Net45 - Mono / .NET 4.5
* Net35 - Mono / .NET 3.5
* iOS - iOS
* Android - Android
* Unity - Unity (either all-purpose, or "Windows Standalone" depending on the project)
* UnityAndroid - Unity Android Player
* UnityApple - Unity iOS and OS X Standalone

Note that you must have a Xamarin paid license to build for iOS or Android.  Furthermore, to build any of the Unity projects you must supply the UnityEngine.dll file to the project.  If your Unity install is in the default location, then the project will copy it as part of the build.  Otherwise, it needs to go in the src/Couchbase.Lite.Unity/vendor/Unity folder.  If you can't build the project then file an issue here.

ForestDB requires native components to be built for each platform you want to target.  Those need to be put in `src/Couchbase.Lite.Shared/vendor/cbforest/prebuilt`.  You can either download the binaries from a GitHub release page (starting with 1.2) or build them yourself.  Building instructions can be found [here](https://github.com/couchbaselabs/cbforest/blob/master/CSharp/README.md).

## Other Notes

* [About repo branches](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/Branches.md)
* [Code style guidelines](https://github.com/couchbase/couchbase-lite-net/blob/master/Notes/StyleGuidelines.md)

## Running Tests

The replication unit tests currently require a running instance of `sync_gateway`. Prior to running the replication tests, start `sync_gateway` by running the `start_gateway` script found in the root of the project

The iOS and Android unit tests require a custom test runner so to run them you must first run the `init-unit.sh` script in the respective Couchbase.Lite.\<Platform>.Tests directory

## Example Apps
* [GrocerySync](https://github.com/couchbase/couchbase-lite-net/tree/master/samples)
	* Beginner example
* [Couchbase Connect](https://github.com/FireflyLogic/couchbase-connect-14)
	* Advanced example
	
[![GitHub license](https://img.shields.io/github/license/couchbase/couchbase-lite-net.svg?style=plastic)]()
