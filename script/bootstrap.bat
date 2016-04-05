call git submodule update --init --recursive

rem Save off the script directory
set script_dir=%~dp0

rem Pop the current working directory so we can return later
pushd %cd%

rem For paket to work off the script directory, we have to change the working directory
cd %script_dir%

.paket\paket.bootstrapper.exe
if errorlevel 1 (
    exit /b %errorlevel%
)

.paket\paket.exe restore
if errorlevel 1 (
    exit /b %errorlevel%
)

rem Return to the directory we originally called this from
popd 

call move %cd%\script\paket-files\github.com\* %cd%\src\Couchbase.Lite.Shared\vendor\cbforest\CSharp\prebuilt
call move %cd%\script\paket-files\github.com\arm64-v8a %cd%\src\Couchbase.Lite.Shared\vendor\cbforest\CSharp\prebuilt
call move %cd%\script\paket-files\github.com\arm64-v7a %cd%\src\Couchbase.Lite.Shared\vendor\cbforest\CSharp\prebuilt
call move %cd%\script\paket-files\github.com\x86 %cd%\src\Couchbase.Lite.Shared\vendor\cbforest\CSharp\prebuilt
call move %cd%\script\paket-files\github.com\x86_64 %cd%\src\Couchbase.Lite.Shared\vendor\cbforest\CSharp\prebuilt