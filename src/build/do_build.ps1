Push-Location $PSScriptRoot\..\Couchbase.Lite

$VSRegistryKey = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7"
$VSInstall = (Get-ItemProperty -Path $VSRegistryKey -Name "15.0") | Select-Object -ExpandProperty "15.0"
if(-Not $VSInstall) {
    throw "Unable to locate VS2017 installation"
}

$MSBuild = "$VSInstall\MSBuild\15.0\Bin\MSBuild.exe"

Write-Host
Write-Host *** TRANSFORMING TEMPLATES ***
Write-Host
& $MSBuild Couchbase.Lite.csproj /t:Transform /p:TransformFile="Properties\DynamicAssemblyInfo.tt"

Write-Host
Write-Host *** RESTORING PACKAGES ***
Write-Host *** MAIN ASSEMBLY ***
Write-Host
dotnet restore
Push-Location ..\Couchbase.Lite.Support.NetDesktop
Write-Host
Write-Host *** NET DESKTOP ***
Write-Host
dotnet restore

cd ..\Couchbase.Lite.Support.UWP
Write-Host
Write-Host *** UWP ***
Write-Host
& $MSBuild /t:Restore

Write-Host
Write-Host *** IOS ***
Write-Host
cd ..\Couchbase.Lite.Support.Apple\iOS
& $MSBuild /t:Restore

Write-Host
Write-Host *** ANDROID ***
Write-Host
cd ..\..\Couchbase.Lite.Support.Android
& $MSBuild /t:Restore

Write-Host
Write-Host *** COPYING NATIVE RESOURCES ***
Write-Host
if(-Not (Test-Path "..\Couchbase.Lite.Support.Apple\iOS\Resources")) {
    New-Item -ItemType Directory ..\Couchbase.Lite.Support.Apple\iOS\Resources
} 

xcopy /Y ..\..\vendor\couchbase-lite-core\build_cmake\ios-fat\libLiteCore.dylib ..\Couchbase.Lite.Support.Apple\iOS\Resources
Write-Host *** BUILDING ***
Write-Host

Pop-Location
cd ..
& $MSBuild Couchbase.Lite.sln /p:Configuration=Release /p:SourceLinkCreate=true

Pop-Location
