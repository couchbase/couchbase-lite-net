param(
    [Parameter(Mandatory=$true)][string]$NexusRepo,
    [Parameter(Mandatory=$true)][string[]]$Variants,
    [Parameter(Mandatory=$true)][string]$Sha,
    [switch]$DebugLib
)

pushd $PSScriptRoot\..\..\vendor\couchbase-lite-core\build_cmake
$suffix = ""
if($DebugLib) {
    $suffix = "-debug"
}

Write-Host "Fetching variants for $Sha..."
if($Variants[0].ToLower() -eq "all") {
    $Variants = @("macosx", "linux", "ios", "android-x86", "android-armeabi-v7a", "android-arm64-v8a")
}

$VARIANT_EXT = @{
  "macosx" = "zip"; 
  "ios" = "zip"; 
  "linux" = "tar.gz"; 
  "android-x86" = "zip"; 
  "android-armeabi-v7a" = "zip";
  "android-arm64-v8a" = "zip"
}

try {
    $i = 0
    foreach ($variant in $Variants) {
        echo "Fetching $variant..."
        $extension = $VARIANT_EXT[$variant]
        
        echo $NexusRepo/couchbase-litecore-$variant/$Sha/couchbase-litecore-$variant-$Sha$suffix.$extension
        Invoke-WebRequest $NexusRepo/couchbase-litecore-$variant/$Sha/couchbase-litecore-$variant-$Sha$suffix.$extension -Out litecore-$variant$suffix.$extension
    }
} catch [System.Net.WebException] {
    popd
    if($_.Exception.Status -eq [System.Net.WebExceptionStatus]::ProtocolError) {
        $res = $_.Exception.Response.StatusCode
        if($res -eq 404) {
            Write-Host "$variant for $Sha is not ready yet!"
            exit 1
        }
    }
    
    throw
}

if(Test-Path "litecore-macosx$suffix.zip"){
    & 7z e -y litecore-macosx$suffix.zip lib/libLiteCore.dylib
    rm litecore-macosx$suffix.zip
}

if(Test-Path "litecore-linux$suffix.tar.gz"){
    & 7z x litecore-linux$suffix.tar.gz
    & 7z e -y litecore-linux$suffix.tar lib/libLiteCore.so lib/libsqlite3.so lib/libc++.so lib/libc++abi.so
    rm litecore-linux$suffix.tar
    rm litecore-linux$suffix.tar.gz
}

if(Test-Path "litecore-ios$suffix.zip") {
    New-Item -Type directory -ErrorAction Ignore ios-fat
    cd ios-fat
    mv ..\litecore-ios$suffix.zip .
    & 7z e -y litecore-ios$suffix.zip
    rm litecore-ios$suffix.zip
    cd ..
}

foreach($arch in @("x86", "armeabi-v7a", "arm64-v8a")) {
    if(Test-Path "litecore-android-$arch$suffix.zip") {
        New-Item -Type directory -ErrorAction Ignore android\lib\$arch
        cd android\lib\$arch
        Move-Item ..\..\..\litecore-android-$arch$suffix.zip .
        & 7z e -y litecore-android-$arch$suffix.zip
        rm litecore-android-$arch$suffix.zip
        cd ..\..\..
    }
}
popd
