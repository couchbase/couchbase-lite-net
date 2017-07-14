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
    & 7z e -y litecore-linux$suffix.tar lib/libLiteCore.so lib/libsqlite3.so
    rm litecore-linux$suffix.tar
    rm litecore-linux$suffix.tar.gz
}

if(Test-Path "litecore-ios$suffix.zip") {
    mkdir -ErrorAction Ignore ios-fat
    cd ios-fat
    & 7z e -y ..\litecore-ios$suffix.zip
    cd ..
    rm litecore-ios$suffix.zip
}

foreach($arch in @("x86", "armeabi-v7a", "arm64-v8a")) {
    if(Test-Path "litecore-android-$arch$suffix.zip") {
        mkdir -ErrorAction Ignore android\lib\$arch
        cd android\lib\$arch
        & 7z e -y ..\..\..\litecore-android-$arch$suffix.zip
        cd ..\..\..
        rm litecore-android-$arch$suffix.zip
    }
}
popd