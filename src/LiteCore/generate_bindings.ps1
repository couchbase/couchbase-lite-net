$headersdir = [IO.Path]::Combine([string]$PSScriptRoot, '..', '..', 'vendor', 'prebuilt_core', 'include')
$fleecedir = [IO.Path]::Combine($headersdir, 'fleece')

if(-Not (Test-Path $headersdir)) {
    Write-Warning "Unable to find headers to parse, please copy them to couchbase-lite-net/vendor/prebuilt_core/include first"
    exit 1
}
Push-Location $fleecedir

Copy-Item $PSScriptRoot/parse/parse_API.py .
Copy-Item $PSScriptRoot/parse/parse_structs.py .
Copy-Item $PSScriptRoot/parse/parse_enums.py .
Copy-Item $PSScriptRoot/parse/config_fleece.py .
Get-ChildItem -Path $PSScriptRoot/src/LiteCore.Shared/Interop/* -Filter "*_defs.cs" | ForEach-Object($_) {
    Remove-Item $_.FullName
}

Get-ChildItem -Path $PSScriptRoot/src/LiteCore.Shared/Interop/* -Filter "*_native.cs" -Exclude "Misc_native.cs" | ForEach-Object($_) {
    Remove-Item $_.FullName
}

Copy-Item -Recurse $PSScriptRoot/parse/templates_fleece templates
python3 parse_API.py -c config_fleece -v -l $PSScriptRoot/binding_list/c4.def
python3 parse_structs.py
Move-Item -Force *.template $PSScriptRoot/src/LiteCore.Shared/Interop
Move-Item -Force *.cs $PSScriptRoot/src/LiteCore.Shared/Interop
Remove-Item *.py
Remove-Item *.pyc
Remove-Item -Force -Recurse templates
Pop-Location

Push-Location $headersdir
Copy-Item $PSScriptRoot/parse/parse_API.py .
Copy-Item $PSScriptRoot/parse/parse_structs.py .
Copy-Item $PSScriptRoot/parse/parse_enums.py .
Copy-Item $PSScriptRoot/parse/config_c4.py .
Copy-Item -Recurse $PSScriptRoot/parse/templates_c4 templates
python3 parse_API.py -c config_c4 -v -l $PSScriptRoot/binding_list/c4.def
python3 parse_structs.py
Move-Item -Force *.template $PSScriptRoot/src/LiteCore.Shared/Interop
Move-Item -Force *.cs $PSScriptRoot/src/LiteCore.Shared/Interop
Remove-Item *.py
Remove-Item *.pyc
Remove-Item -Force -Recurse templates
Push-Location $PSScriptRoot/src/LiteCore.Shared/Interop
python gen_bindings.py
Remove-Item *.template
Move-Item -Force *_native.cs $PSScriptRoot/src/LiteCore.Shared/Interop/
Pop-Location
Pop-Location
Pop-Location