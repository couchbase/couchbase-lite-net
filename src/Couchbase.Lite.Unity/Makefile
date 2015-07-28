debug:
	xbuild Couchbase.Lite.Unity.csproj /p:SolutionDir=`pwd`/../
	xbuild vendor/SQLitePCL/sqlitepcl.sln
	cp ../Couchbase.Lite.Net35/bin/Debug/ugly_net35.dll bin/Debug/Assets/Plugins
	rm bin/Debug/Assets/Plugins/*.xml

release:
	xbuild Couchbase.Lite.Unity.csproj /p:SolutionDir=`pwd`/../ /p:Configuration=Release
	xbuild vendor/SQLitePCL/sqlitepcl.sln /p:Configuration=Release
	cp ../Couchbase.Lite.Net35/bin/Release/ugly_net35.dll bin/Release/Assets/Plugins
	rm bin/Release/Assets/Plugins/*.xml
