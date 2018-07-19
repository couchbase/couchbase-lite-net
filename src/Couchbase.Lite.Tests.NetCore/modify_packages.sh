#!/bin/bash

pushd `dirname $0`
sed "s/[0-9].[0-9].[0-9]-b..../$1-$2/g" Couchbase.Lite.Tests.NetCore.csproj > tmp
mv tmp Couchbase.Lite.Tests.NetCore.csproj
sed "s/[0-9].[0-9].[0-9]-b..../$1-$2/g" Couchbase.Lite.Tests.NetCore.Source.csproj > tmp
mv tmp Couchbase.Lite.Tests.NetCore.Source.csproj
popd
