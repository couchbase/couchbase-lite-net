*TL;DR (well...DWTR)*:  This is how to enable the various storage modes of Couchbase Lite:

1. System SQLite - Do nothing
2. SQLite with encryption - Add the Couchbase.Lite.Storage.SQLCipher nuget package
3. ForestDB (with or without encryption is the same) - Add the Couchbase.Lite.Storage.ForestDB nuget package

Changes in 1.2
==============

In order to make the library more modular and keep down library size, the various support libs for the storage engine types have been split into separate packages.  Currently there are three support packages:

1. System SQLite
2. SQLCipher
3. ForestDB

The first one is a required dependency by default so that the library can function without manually adding more packages.  It will use whatever SQLite is installed by default on the platform being used (except for Windows, which has no SQLite by default.  It will use one that is shipped in the package).

The second one uses SQLCipher, which is a custom build of SQLite with some added features for encryption.  Note that if you try to use encryption features without installing this package, you will be met with an exception explaining what you did.

The third one uses ForestDB, which is a key value storage library that Couchbase developed.  This is the first release of this library for use in .NET but the underlying native library has been available in the Objective-C version for quite some time.  If you try to open a ForestDB database without installing this package then you will be met with an exception explaining what you did.

Note that to make the selection process as automatic as possible, I have implemented a sort of hierarchy.  If you include the SQLCipher package it will override the SystemSQLite package.  You can check the logs to figure out which plugin has been loaded if you are unsure.  Make sure if you want to switch back to SystemSQLite from SQLCipher that you clean the project and ensure that the SQLCipher plugin DLL is not present.

Details about this system
=========================

All of the managed functionality, and native image binding, has been refactored into separate pluggable assemblies.  The system will attempt to use `Assembly.Load` to load the correct assemblies at runtime.  There is no need to worry about binding to the wrong native library since the binding is not chosen via program logic directly, but rather by which managed code gets loaded.
