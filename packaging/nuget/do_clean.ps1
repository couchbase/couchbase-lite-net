param(
    [Parameter(Mandatory=$true)][string]$apikey)
    
$url="http://mobile.nuget.couchbase.com/nuget/CI/Packages()?`$format=json"
$content=$(Invoke-WebRequest $url).Content
$results = $(ConvertFrom-Json $content).d.results
foreach($result in $results) {
    $ticks = [long]$result.Published.Substring(7,18)
    $published = New-Object DateTime($ticks)
    $now = Get-Date
    $limit = New-TimeSpan -Days 30
    if(($now - $published) -gt $limit) {
        echo "Deleting $($result.Id)-$($result.Version)"
        
        # Nuget won't fail when deleting a non-existent package, and Internal is a strict subset of CI
        nuget delete -ApiKey $apikey -Source http://mobile.nuget.couchbase.com/nuget/CI -NonInteractive $result.Id $result.Version
        nuget delete -ApiKey $apikey -Source http://mobile.nuget.couchbase.com/nuget/Internal -NonInteractive $result.Id $result.Version
    }
}