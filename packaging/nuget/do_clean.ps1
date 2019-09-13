param(
    [Parameter(Mandatory=$true)][string]$apikey)
    
$ErrorActionPreference = "Stop"

$url="http://mobile.nuget.couchbase.com/nuget/CI/Packages()?`$format=json"
$content=$(Invoke-WebRequest $url).Content
$results = $(ConvertFrom-Json $content).d.results
foreach($result in $results) {
    $ms = [long]$result.Published.Substring(7,13)
    $published = $(New-Object -Type DateTime -ArgumentList 1970, 1, 1, 0, 0, 0, 0).AddMilliseconds($ms)

    $now = $(Get-Date).ToUniversalTime()
    $limit = New-TimeSpan -Days 30
    if(($now - $published) -gt $limit) {
        Write-Host "Deleting $($result.Id)-$($result.Version)"
        
        # Nuget won't fail when deleting a non-existent package, and Internal is a strict subset of CI
        dotnet nuget delete --api-key $apikey --source http://mobile.nuget.couchbase.com/nuget/CI --non-interactive $result.Id $result.Version
        dotnet nuget delete --api-key $apikey --source http://mobile.nuget.couchbase.com/nuget/Internal --non-interactive $result.Id $result.Version
    }
}
