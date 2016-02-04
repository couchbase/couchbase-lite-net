#!/bin/sh

rm -rf tmp
mkdir tmp

for f in *.nupkg; do
  mkdir tmp/${f%.*}
  unzip $f -d tmp/${f%.*}
done
