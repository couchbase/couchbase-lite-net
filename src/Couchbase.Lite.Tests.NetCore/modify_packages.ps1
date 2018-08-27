param([Parameter(Mandatory=$true)][string]$version, [switch]$debugProject)
$sourceProjectFile = "Couchbase.Lite.Tests.NetCore.Source.csproj"
if (-NOT (Test-Path $sourceProjectFile)) {
    $scriptpath = Split-Path $MyInvocation.MyCommand.Path
    Push-Location $scriptpath
    [Environment]::CurrentDirectory = $scriptpath

    $content = [System.IO.File]::ReadAllLines("Couchbase.Lite.Tests.NetCore.csproj")
    $regex = New-Object -TypeName "System.Text.RegularExpressions.Regex" ".*?<PackageReference Include=`"Couchbase.Lite.Enterprise.*?`" Version=`"(.*?)`""
    for($i = 0; $i -lt $content.Length; $i++) {
        $line = $content[$i]
        $matches = $regex.Matches($line)
        if($matches.Count -gt 0) {
            $oldVersion = $matches[0].Groups[1]
            $line = $line.Replace($oldVersion, $version)
            $content[$i] = $line
        }
    }

    [System.IO.File]::WriteAllLines("Couchbase.Lite.Tests.NetCore.csproj", $content)
} else {
    $scriptpath = Split-Path $MyInvocation.MyCommand.Path
    Push-Location $scriptpath
    [Environment]::CurrentDirectory = $scriptpath

    if($debugProject) {
        $content = [System.IO.File]::ReadAllLines("Couchbase.Lite.Tests.NetCore.Source.csproj")
    } else {
        $content = [System.IO.File]::ReadAllLines("Couchbase.Lite.Tests.NetCore.csproj")
    }

    $regex = New-Object -TypeName "System.Text.RegularExpressions.Regex" ".*?<PackageReference Include=`"Couchbase.Lite.Enterprise.*?`" Version=`"(.*?)`""
    for($i = 0; $i -lt $content.Length; $i++) {
        $line = $content[$i]
        $matches = $regex.Matches($line)
        if($matches.Count -gt 0) {
            $oldVersion = $matches[0].Groups[1]
            $line = $line.Replace($oldVersion, $version)
            $content[$i] = $line
        }
    }

    if($debugProject) {
        [System.IO.File]::WriteAllLines("Couchbase.Lite.Tests.NetCore.Source.csproj", $content)
    } else {
        [System.IO.File]::WriteAllLines("Couchbase.Lite.Tests.NetCore.csproj", $content)
    }
}