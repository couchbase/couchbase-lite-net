*TL;DR (well...DWTR)*:  This is how to enable the various storage modes of Couchbase Lite:

1. System SQLite - Do nothing
2. SQLite with encryption - Add the Couchbase.Lite.Storage.SQLCipher nuget package and add CBL_SQLCIPHER to your project's define symbols.
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

Note that I have made a conscious choice to make the switch between 1 and 2 explicit.  I don't want to leave it up to MSBuild so to enable encryption you need to install the support package and also add a define to your project properties (CBL_SQLCIPHER).  There are plans to add a fourth system (custom built regular SQLite).  If you are curious about how this system is working then keep reading.

Details about this system
=========================

All of the persistence and retrieval of data to and from disk is handled by unmanaged code (written in C and C++).  So in order to make use of it, Couchbase Lite for .NET uses P/Invoke.  This means that if we are clever about our search paths and our library naming that we can swap out behavior by replacing or overriding native dynamic libraries at build time.  Each of the packages for each platform contains these native libraries and inserts them if the proper conditions are met.  The exception is iOS, which needs to be statically compiled since we are still supporting iOS 7.  In the case of iOS, each package contains two managed DLLs:  The first contains a SQLite interop DLL that P/Invokes into libsqlite3.0.dylib and a "fake" ForestDB interop dll that will not function because the static library is missing (this is so that the project can still compile).  The second contains a SQLite interop DLL that contains a static build of SQLCipher, and a fake ForestDB interop.  The third contains the real ForestDB interop with the static ForestDB native support library embedded.  

So taking .NET 4.5 on OS X as an example, here is what will happen in various scenarios:

Scenario 1: Couchbase.Lite is installed and no further changes are made.  If a user tries to use a SQLite based database, then the runtime will search for libsqlite3.0.dylib and find it installed in the system directory.  Encryption features will throw an exception indicating that encryption is not available.  If the user tries to use a ForestDB based database, the runtime will search for libCBForest-Interop.dylib and not find it and throw an exception indicating that ForestDB is not available.  

Scenario 2: Couchbase.Lite and the SQLCipher support package are installed.  If a user tries to use a SQLite based database, the package included a special file that will instruct the runtime to change all requests for sqlite3 to libsqlcipher.dylib.  The runtime will search for libsqlcipher.dylib and find it in the working directory.  Standard and encryption features will all be enabled.  ForestDB will behave the same as scenario 1.

Scenario 3: Couchbase.Lite and the ForestDB support package are installed.  SQLite will behave as in scenario 1.  If a user tries to use a ForestDB database then the runtime will search for libCBForest-Interop.dylib and find it in the working directory.  All features, including ForestDB encryption, will be available.  

All other platforms behave in a similar fashion, except that iOS is trading actual managed DLLs with or without embedded static libraries instead of dynamic libraries.  
