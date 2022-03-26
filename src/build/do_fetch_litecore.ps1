param(
    [Parameter(Mandatory=$true)][string[]]$Variants,
    [Parameter(Mandatory=$true)][string]$Sha,
    [switch]$DebugLib
)

#python "..\..\vendor\couchbase-lite-core\scripts\fetch_litecore.py" --variants "windows-win64", "windows-win32" --ce "C:\Development\couchbase-lite-net-ee\couchbase-lite-net\vendor\couchbase-lite-core"
python "..\..\vendor\couchbase-lite-core\scripts\fetch_litecore.py" -v $Variants -d -s $Sha -o "..\..\vendor\couchbase-lite-core\build_cmake"

Push-Location $PSScriptRoot\..\..\vendor\couchbase-lite-core\build_cmake

# Process MacOS Library
if(Test-Path "macos/x86_64/lib/libLiteCore.dylib"){
	if(Test-Path "libLiteCore.dylib"){
	    Remove-item "libLiteCore.dylib"
	}
	
	Move-Item "macos/x86_64/lib/libLiteCore.dylib" .
	Remove-Item "macos" -Recurse
}

# Process Linux Libraries
foreach($arch in @("libLiteCore.so", "libstdc++.so", "libstdc++.so.6", "libicudata.so.54.1", "libicui18n.so.54.1", "libicuuc.so.54.1")) {
	if(Test-Path linux\x86_64\lib\$arch){
        if(Test-Path $arch){
		    Remove-item $arch
	    }
	
	    Move-Item -Force linux\x86_64\lib\$arch .
    }
}

Remove-Item "linux" -Recurse
	
# Process iOS Library
if(Test-Path "ios/LiteCore.framework/LiteCore") {
    New-Item -Type directory -ErrorAction Ignore ios-fat
    Set-Location ios-fat
    Move-Item ..\ios\LiteCore.framework\LiteCore .
    Set-Location ..
	Remove-Item "ios" -Recurse
}

# Process Android Libraries
foreach($arch in @("x86", "x86_64", "armeabi-v7a", "arm64-v8a")) {
    if(Test-Path android\$arch\lib\libLiteCore.so) {
        New-Item -Type directory -ErrorAction Ignore android\lib\$arch
        Set-Location android\lib\$arch
        Move-Item ..\..\$arch\lib\libLiteCore.so .
        Set-Location ..\..
		Remove-Item $arch -Recurse
		Set-Location ..
    }
}

# Process Windows Libraries
if(Test-Path "windows/arm-store"){
	if(Test-Path "arm/RelWithDebInfo"){
		Remove-item "arm/RelWithDebInfo" -Recurse
	}
	
	if(!(Test-Path "arm")){
		New-Item -ItemType directory "arm" -Force
    }
	
	Move-Item "windows/arm-store" "arm" -Force
	Rename-Item "arm/arm-store" "RelWithDebInfo"
}

if(Test-Path "windows/x86-store"){
	if(Test-Path "x86_store/RelWithDebInfo"){
		Remove-item "x86_store/RelWithDebInfo" -Recurse
	} 
	
	if(!(Test-Path "x86_store")){
		New-Item -ItemType directory "x86_store" -Force
	}
	
	Move-Item "windows/x86-store" "x86_store" -Force
	Rename-Item "x86_store/x86-store" "RelWithDebInfo"
}

if(Test-Path "windows/x86_64-store"){
	if(Test-Path "x64_store/RelWithDebInfo"){
		Remove-item "x64_store/RelWithDebInfo" -Recurse
	} 
	
	if(!(Test-Path "x64_store")){
		New-Item -ItemType directory "x64_store" -Force
	}
	
	Move-Item "windows/x86_64-store" "x64_store" -Force
	Rename-Item "x64_store/x86_64-store" "RelWithDebInfo"
}

if(Test-Path "windows/x86"){
	if(Test-Path "x86/RelWithDebInfo"){
		Remove-item "x86/RelWithDebInfo" -Recurse
	}
	
	if(!(Test-Path "x86")){
		New-Item -ItemType directory "x86" -Force
	}
	
	Move-Item "windows/x86" "x86" -Force
	Rename-Item "x86/x86" "RelWithDebInfo"
}

if(Test-Path "windows/x86_64"){
	if(Test-Path "x64/RelWithDebInfo"){
		Remove-item "x64/RelWithDebInfo" -Recurse
	} 
	
	if(!(Test-Path "x64")){
		New-Item -ItemType directory "x64" -Force
	}
	
	Move-Item "windows/x86_64" "x64" -Force
	Rename-Item "x64/x86_64" "RelWithDebInfo"
}

Remove-Item "windows"

Pop-Location
