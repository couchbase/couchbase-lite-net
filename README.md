[![Stories in Ready](https://badge.waffle.io/couchbaselabs/couchbase-lite-net.png?label=ready&title=Ready)](https://waffle.io/couchbase/couchbase-lite-net)
couchbase-lite-net
==================

Couchbase Lite is a lightweight embedded NoSQL database that has built-in sync to larger backend structures, such as Couchbase Server.

This is the source repo of Couchbase Lite C#. It is a port of Couchbase Lite from Couchbase Lite Android.

## Architecture

![](http://tleyden-misc.s3.amazonaws.com/couchbase-lite/couchbase-lite-architecture.png)

Couchbase Lite databases are able to sync with eachother via [Sync Gateway](https://github.com/couchbase/sync_gateway/) backed by [Couchbase Server](http://www.couchbase.com/couchbase-server/overview)

## Documentation Overview

* [Official Documentation](http://developer.couchbase.com/mobile/develop/guides/couchbase-lite/index.html)

## Building Couchbase Lite master branch from source

If you plan to use Visual Studio, you'll need meet one these prereqs:
* VS 2013 Pro, Premium, or Ultimate, Update 2 or later.
	* Must also have the [Shared Project Reference Manager](https://visualstudiogallery.msdn.microsoft.com/315c13a7-2787-4f57-bdf7-adae6ed54450) extension.
* Xamarin Studio 5 or later.

## Running Tests

The replication unit tests currently require a running
instance of `sync_gateway`. Prior to running the
replication tests, start `sync_gateway` with the following
command:

*nix:
   /path/to/sync_gateway -pretty -verbose=true Couchbase.Lite/Couchbase.Lite.Tests/Assets/GatewayConfig.json

## Example Apps
* [GrocerySync](https://github.com/couchbase/couchbase-lite-net/tree/master/samples)
	* Beginner example
* [Couchbase Connect](https://github.com/FireflyLogic/couchbase-connect-14)
	* Advanced example
	
## License

Apache License 2.0
