param (
    [Parameter(Mandatory=$true)][string]$version
)

$scriptpath = Split-Path $MyInvocation.MyCommand.Path
Push-Location $scriptpath
[Environment]::CurrentDirectory = $scriptpath

$files = "project.json"
foreach($file in $files) {
    $content = cat $file | ConvertFrom-Json
    foreach($key in ($content.dependencies | Get-Member Couchbase.Lite* -MemberType NoteProperty).Name) {
        $content.dependencies.$key = $version
    }

    $text = ConvertTo-Json $content
    [System.IO.File]::WriteAllText($file, $text)
}