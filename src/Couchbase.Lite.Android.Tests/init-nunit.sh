#!/bin/sh

mkdir -p vendor
pushd vendor
if [ ! -d NUnitLite ]; then
  git clone https://github.com/spouliot/NUnitLite.git
  pushd NUnitLite
  git checkout c6f1cb01efe5d687875fcc6243abc6910a064410
  git apply ../../../Couchbase.Lite.Tests.Shared/nunitlite_fixes.patch
  popd
fi

if [ ! -d MonoDroid.Dialog ]; then
  git clone https://github.com/couchbaselabs/MonoDroid.Dialog
fi

if [ ! -d Andr.Unit ]; then
  git clone https://github.com/couchbaselabs/Andr.Unit
fi
popd