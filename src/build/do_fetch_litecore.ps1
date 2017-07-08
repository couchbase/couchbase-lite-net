param(
    [Parameter(Mandatory=$true)][string]$NexusRepo
)

pushd $PSScriptRoot\..\..\vendor\couchbase-lite-core\build_cmake
$sha = $(& 'C:\Program Files\Git\bin\git.exe' rev-parse HEAD)
Write-Host "Fetching variants for $sha..."
$VARIANTS = @("macosx", "linux")
$EXTENSIONS = @("zip", "tar.gz")
try {
    $i = 0
    foreach ($variant in $VARIANTS) {
        echo "Fetching $variant..."
        Invoke-WebRequest $NexusRepo/couchbase-litecore-$variant/$sha/couchbase-litecore-$variant-$sha.$($EXTENSIONS[$i]) -Out litecore-$variant.$($EXTENSIONS[$i++])
    }
} catch [System.Net.WebException] {
    popd
    if($_.Exception.Status -eq [System.Net.WebExceptionStatus]::ProtocolError) {
        $res = $_.Exception.Response.StatusCode
        if($res -eq 404) {
            Write-Host "$variant for $sha is not ready yet!"
            exit 1
        }
    }
    
    throw
}

& 7z e -y litecore-macosx.zip lib/libLiteCore.dylib
& 7z x litecore-linux.tar.gz
& 7z e -y litecore-linux.tar lib/*
rm litecore-macosx.zip
rm litecore-linux.tar
rm litecore-linux.tar.gz
popd