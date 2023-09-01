param(
    [Parameter(Mandatory=$true)][string[]]$Variants,
    [Parameter(Mandatory=$true)][string]$Sha,
    [switch]$DebugLib
)

Push-Location $PSScriptRoot\..\..\vendor\couchbase-lite-core\build_cmake

$isDebug = $null
if($DebugLib) {
    $isDebug = "-d"
}

python.exe -m venv venv
venv\Scripts\activate 
pip3 install GitPython
python.exe "$PSScriptRoot\..\LiteCore\tools\fetch_litecore.py" -v $Variants $isDebug -s $Sha -o .
deactivate

# Process MacOS Library
if(Test-Path "macos/lib/libLiteCore.dylib") {
	if(Test-Path "libLiteCore.dylib"){
		Remove-item "libLiteCore.dylib"
	}
	Move-Item "macos/lib/libLiteCore.dylib" .
	Remove-Item "macos" -Recurse
}

# Process Linux Libraries
foreach($arch in @("libLiteCore.so", "libstdc++.so", "libstdc++.so.6", "libicudata.so.71", "libicui18n.so.71", "libicuuc.so.71")) {
    if(Test-Path linux\x86_64\lib\$arch){
	    if($arch -eq 'libicudata.so.71' -Or $arch -eq 'libicui18n.so.71' -Or $arch -eq 'libicuuc.so.71'){
			$arch1 = $arch.Replace(".71", ".71.1")
		    Move-Item -Force linux\x86_64\lib\$arch1 $arch
		} else {
	        Move-Item -Force linux\x86_64\lib\$arch .
		}
    }
}

foreach($arch in @("x86", "x86_64", "armeabi-v7a", "arm64-v8a")) {
    if(Test-Path android\$arch\lib\libLiteCore.so) {
        New-Item -Type directory -ErrorAction Ignore android\lib\$arch
        Set-Location android\lib\$arch
        Move-Item ..\..\$arch\lib\libLiteCore.so . -Force
        Set-Location ..\..
		Remove-Item $arch -Recurse
		Set-Location ..
    }
}

if(Test-Path linux){
    Remove-Item linux -Recurse
}

if(Test-Path "windows/arm64-store/bin"){
	if(Test-Path "arm64_store/RelWithDebInfo"){
		Remove-item "arm64_store/RelWithDebInfo" -Recurse
	}
	
	New-Item -Type directory -ErrorAction Ignore arm64_store\RelWithDebInfo
	Set-Location arm64_store\RelWithDebInfo
	Move-Item ..\..\windows\arm64-store\bin\LiteCore.dll,..\..\windows\arm64-store\bin\LiteCore.pdb .
    Set-Location ..\..
}

if(Test-Path "windows/x86_64-store/bin"){
	if(Test-Path "x64_store/RelWithDebInfo"){
		Remove-item "x64_store/RelWithDebInfo" -Recurse
	}
	
	New-Item -Type directory -ErrorAction Ignore x64_store\RelWithDebInfo
	Set-Location x64_store\RelWithDebInfo
	Move-Item ..\..\windows\x86_64-store\bin\LiteCore.dll,..\..\windows\x86_64-store\bin\LiteCore.pdb .
    Set-Location ..\..
}

if(Test-Path "windows/x86_64/bin"){
	if(Test-Path "x64/RelWithDebInfo"){
		Remove-item "x64/RelWithDebInfo" -Recurse
	} 
	
	New-Item -Type directory -ErrorAction Ignore x64\RelWithDebInfo
	Set-Location x64\RelWithDebInfo
	Move-Item ..\..\windows\x86_64\bin\LiteCore.dll,..\..\windows\x86_64\bin\LiteCore.pdb .
    Set-Location ..\..
}

if(Test-Path "windows/arm64/bin"){
	if(Test-Path "arm64/RelWithDebInfo"){
		Remove-item "arm64/RelWithDebInfo" -Recurse
	} 
	
	New-Item -Type directory -ErrorAction Ignore arm64\RelWithDebInfo
	Set-Location arm64\RelWithDebInfo
	Move-Item ..\..\windows\arm64\bin\LiteCore.dll,..\..\windows\arm64\bin\LiteCore.pdb .
    Set-Location ..\..
}

if(Test-Path windows){
    Remove-Item windows -Recurse
}

if(Test-Path venv){
    Remove-Item venv -Recurse
}
Pop-Location
