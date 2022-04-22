# Submodule
git submodule sync
Push-Location couchbase-lite-core-EE
git submodule sync
Pop-Location
Push-Location couchbase-lite-net
git submodule sync
git submodule update --init --recursive
Pop-Location