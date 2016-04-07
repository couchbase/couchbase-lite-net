#/bin/sh

#Build the managed DLLs
xbuild /p:Configuration=Packaging ../../src/Couchbase.Lite.Net35.sln
xbuild /p:Configuration=Packaging ../../src/Couchbase.Lite.Net45.sln
xbuild /p:Configuration=Packaging ../../src/Couchbase.Lite.iOS.sln
xbuild /p:Configuration=Packaging ../../src/Couchbase.Lite.Android.sln

#Do the actual packing
nuget pack -BasePath ../.. couchbase-lite.nuspec 
nuget pack -BasePath ../.. couchbase-lite-listener.nuspec 
nuget pack -BasePath ../.. couchbase-lite-listener-bonjour.nuspec 
nuget pack -BasePath ../.. couchbase-lite-storage-systemsqlite.nuspec
nuget pack -BasePath ../.. couchbase-lite-storage-sqlcipher.nuspec
nuget pack -BasePath ../.. couchbase-lite-storage-forestdb.nuspec
