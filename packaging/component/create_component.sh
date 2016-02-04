#!/bin/sh

if [ "$#" -lt 1 ]; then
  echo "Need to specify a path to xamarin_component.exe"
  exit 1 
fi

mkdir tmpios
mkdir tmpdroid
mv ../../samples/CouchbaseSample.iOS/UsesSource tmpios/UsesSource
mv ../../samples/SimpleAndroidSync/UsesSource tmpdroid/UsesSource

component_path="${1/#\~/$HOME}"
mono $component_path package

mv tmpios/UsesSource ../../samples/CouchbaseSample.iOS/UsesSource
mv tmpdroid/UsesSource ../../samples/SimpleAndroidSync/UsesSource

rmdir tmpios
rmdir tmpdroid

