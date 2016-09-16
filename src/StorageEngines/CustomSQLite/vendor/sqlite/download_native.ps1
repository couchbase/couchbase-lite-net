push-location $PSScriptRoot
mkdir -Force arm64-v8a, armeabi, armeabi-v7a, mips, mips64, x86, x86_64, x64
$ANDROID_FILENAMES = @("arm64-v8a\libsqlite3.so", "armeabi\libsqlite3.so", "armeabi-v7a\libsqlite3.so", "mips\libsqlite3.so",
 "mips64\libsqlite3.so", "x86\libsqlite3.so", "x86_64\libsqlite3.so")

foreach ($filename in $ANDROID_FILENAMES) {
    $final_filename = $filename.replace("sqlite", "esqlite")
    if(!(Test-Path $final_filename)) {
        Write-Output "Downloading $filename (Android)"
        Invoke-WebRequest -Uri "https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/android/$filename" -OutFile $final_filename
    }
}

if(!(Test-Path "libsqlite3.a")) {
    Write-Output "Downloading libsqlite3.a (iOS)"
    Invoke-WebRequest -Uri "https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/ios/libsqlite3.a" -OutFile "libsqlite3.a"
}

if(!(Test-Path "libsqlite3.0.dylib")) {
    Write-Output "Downloading libsqlite3.dylib (OS X)"
    Invoke-WebRequest -Uri "https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/osx/libsqlite3.dylib" -OutFile "libsqlite3.0.dylib"
}

if(!(Test-Path "libsqlite3.so")) {
    Write-Output "Downloading libsqlite3.so (Linux 64-bit)"
    Invoke-WebRequest -Uri "https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/linux/x86_64/libsqlite3.so" -OutFile "libsqlite3.so"
}

if(!(Test-Path "x86\sqlite3.dll")) {
    Write-Output "Downloading x86\sqlite3.dll (Windows)"
    Invoke-WebRequest -Uri "https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/windows/x86/sqlite3.dll" -OutFile "x86\sqlite3.dll"
}

if(!(Test-Path "x64\sqlite3.dll")) {
    Write-Output "Downloading x64\sqlite3.dll (Windows)"
    Invoke-WebRequest -Uri "https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/windows/x86_64/sqlite3.dll" -OutFile "x64\sqlite3.dll"
}
pop-location