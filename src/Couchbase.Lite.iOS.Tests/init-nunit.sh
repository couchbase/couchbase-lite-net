#!/bin/sh

pushd vendor
if [ ! -d Touch.Unit ]; then
  git clone https://github.com/spouliot/Touch.Unit.git
  pushd Touch.Unit
  git checkout e7d73c1a4d037193f589a08c22a18afd4d3baaf7
  git apply ../../touch.unit-ios_fixes.patch
  popd
fi

if [ ! -d NUnitLite ]; then
  git clone https://github.com/spouliot/NUnitLite.git
  pushd NUnitLite
  git checkout c6f1cb01efe5d687875fcc6243abc6910a064410
  git apply ../../../Couchbase.Lite.Tests.Shared/nunitlite_fixes.patch
  popd
fi

popd
