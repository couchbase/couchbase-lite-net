debug:
	xbuild Couchbase.Lite.Unity.csproj /p:SolutionDir=`pwd`/../
	xbuild vendor/SQLitePCL/sqlitepcl.sln
	rm bin/Debug/Assets/Plugins/*.xml

release:
	xbuild Couchbase.Lite.Unity.csproj /p:SolutionDir=`pwd`/../ /p:Configuration=Release
	xbuild vendor/SQLitePCL/sqlitepcl.sln /p:Configuration=Release
	rm bin/Release/Assets/Plugins/*.xml
