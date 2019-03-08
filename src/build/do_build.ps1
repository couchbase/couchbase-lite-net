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
Write-Host
dotnet restore
Push-Location ..
& $MSBuild /t:Restore Couchbase.Lite.sln

Write-Host
Write-Host *** COPYING NATIVE RESOURCES ***
Write-Host
if(-Not (Test-Path "Couchbase.Lite.Support.Apple\iOS\Native")) {
    New-Item -ItemType Directory Couchbase.Lite.Support.Apple\iOS\Native
} 

Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios-fat\LiteCore.framework Couchbase.Lite.Support.Apple\iOS\Native
Write-Host *** BUILDING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /p:Configuration=Packaging /p:SourceLinkCreate=true

Pop-Location
Pop-Location
