@echo off
cls

rem Save off the script directory
set script_dir=%~dp0

call %script_dir%packages\FAKE\tools\FAKE.exe %script_dir%build.fsx
