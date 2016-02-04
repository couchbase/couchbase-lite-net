#!/bin/sh

pushd vendor
if [ ! -d Touch.Unit ]; then
  git clone https://github.com/couchbasedeps/Touch.Unit.git
fi

if [ ! -d NUnitLite ]; then
  git clone https://github.com/spouliot/NUnitLite.git
  pushd NUnitLite
  git checkout c6f1cb01efe5d687875fcc6243abc6910a064410
  git apply ../../../Couchbase.Lite.Tests.Shared/nunitlite_fixes.patch
  popd
fi

popd
