param (
    [Parameter(Mandatory=$true)][string]$version,
    [switch]$debugProject
)

$scriptpath = Split-Path $MyInvocation.MyCommand.Path
Push-Location $scriptpath
[Environment]::CurrentDirectory = $scriptpath

if($debugProject){
    $content = [System.IO.File]::ReadAllLines("Couchbase.Lite.Tests.UWP.csproj")
} else {
    $content = [System.IO.File]::ReadAllLines("Couchbase.Lite.Tests.UWP.Source.csproj")
}

$regex = New-Object -TypeName "System.Text.RegularExpressions.Regex" ".*?<Version>([0-9]\.[0-9]\.[0-9]-b[0-9]{4})<.*?"
for($i = 0; $i -lt $content.Length; $i++) {
    $line = $content[$i]
    $matches = $regex.Matches($line)
    if($matches.Count -gt 0) {
        $oldVersion = $matches[0].Groups[1]
        $line = $line.Replace($oldVersion, $version)
        $content[$i] = $line
    }
}

if($debugProject){
    [System.IO.File]::WriteAllLines("Couchbase.Lite.Tests.UWP.csproj", $content)
} else {
    [System.IO.File]::WriteAllLines("Couchbase.Lite.Tests.UWP.Source.csproj", $content)
}