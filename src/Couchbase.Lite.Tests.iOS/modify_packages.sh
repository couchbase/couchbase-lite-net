#!/bin/bash

pushd `dirname $0`
sed "s/$1-b..../$1-$2/g" packages.config > tmp
mv tmp packages.config
popd