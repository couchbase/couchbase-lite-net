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
    $Variants = @("macosx", "linux")
}

$VARIANT_EXT = @{"macosx" = "zip"; "linux" = "tar.gz"}
try {
    $i = 0
    foreach ($variant in $Variants) {
        echo "Fetching $variant..."
        $extension = $VARIANT_EXT[$variant]
        
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

popd