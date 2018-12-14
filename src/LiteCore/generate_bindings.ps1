
$startdir = [IO.Path]::Combine([string]$PSScriptRoot, '..', '..', 'vendor', 'couchbase-lite-core', 'vendor', 'fleece', 'API', 'fleece')
pushd $startdir

Copy-Item $PSScriptRoot\parse\parse_API.py .
Copy-Item $PSScriptRoot\parse\parse_structs.py .
Copy-Item $PSScriptRoot\parse\parse_enums.py .
Copy-Item $PSScriptRoot\parse\config_fleece.py .
Get-ChildItem -Path $PSScriptRoot\src\LiteCore.Shared\Interop\* -Filter "*_defs.cs" -Exclude "LiteCore_Shell_defs.cs" | foreach($_) {
    Remove-Item $_.FullName
}

Get-ChildItem $PSScriptRoot\src\LiteCore.Shared\Interop\Common\* -Exclude "Misc_native.cs" |  foreach($_) {
    Remove-Item $_.FullName
}

Get-ChildItem $PSScriptRoot\src\LiteCore.Shared\Interop\iOS\* -Exclude "Misc_native.cs" |  foreach($_) {
    Remove-Item $_.FullName
}

Copy-Item -Recurse $PSScriptRoot\parse\templates_fleece templates
python parse_API.py -c config_fleece -v
python parse_structs.py
Move-Item -Force *.template $PSScriptRoot\src\LiteCore.Shared\Interop
Move-Item -Force *.cs $PSScriptRoot\src\LiteCore.Shared\Interop
Remove-Item *.py
Remove-Item *.pyc
Remove-Item -Force -Recurse templates
popd

pushd $PSScriptRoot\..\..\vendor\couchbase-lite-core\C\include
Copy-Item $PSScriptRoot\parse\parse_API.py .
Copy-Item $PSScriptRoot\parse\parse_structs.py .
Copy-Item $PSScriptRoot\parse\parse_enums.py .
Copy-Item $PSScriptRoot\parse\config_c4.py .
Copy-Item -Recurse $PSScriptRoot\parse\templates_c4 templates
python parse_API.py -c config_c4 -v
python parse_structs.py
Move-Item -Force *.template $PSScriptRoot\src\LiteCore.Shared\Interop
Move-Item -Force *.cs $PSScriptRoot\src\LiteCore.Shared\Interop
Remove-Item *.py
Remove-Item *.pyc
Remove-Item -Force -Recurse templates
pushd $PSScriptRoot\src\LiteCore.Shared\Interop
python gen_bindings.py
Remove-Item *.template
.\interfacer.ps1
Move-Item -Force *_native_ios.cs $PSScriptRoot\src\LiteCore.Shared\Interop\iOS
Move-Item -Force *_native.cs $PSScriptRoot\src\LiteCore.Shared\Interop\Common
popd
popd
popd