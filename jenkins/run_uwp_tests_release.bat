cd couchbase-lite-net\src\Couchbase.Lite.Tests.UWP
pushd "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\Tools\"

REM workaround for Jenkins hogwash...
set path=%path:"=%

call VsDevCmd.bat
popd
run_tests.bat Release