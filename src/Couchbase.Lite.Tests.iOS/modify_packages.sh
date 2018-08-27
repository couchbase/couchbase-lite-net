#!/bin/bash

if [ -f Couchbase.Lite.Tests.iOS.Source.csproj ];
then
pushd `dirname $0`
sed "s/[0-9].[0-9].[0-9]-b..../$1-$2/g" Couchbase.Lite.Tests.iOS.csproj > tmp
mv tmp Couchbase.Lite.Tests.iOS.csproj
sed "s/[0-9].[0-9].[0-9]-b..../$1-$2/g" Couchbase.Lite.Tests.iOS.Source.csproj > tmp
mv tmp Couchbase.Lite.Tests.iOS.Source.csproj
popd
fi
