#!/bin/bash

# This script is meant to set up nunit to run in an environment that is as
# close to Unity3D as possible.  However, you may need to set up a few things
# to get it working first.  I was getting errors about "unable to find
# configuration" and yadda yadda and it turns out that Unity3D is a bit dumb
# about where it loads configs from.  To solve this, I edited the following
# file:
#
# /Applications/Unity/Unity.app/Contents/Frameworks/Mono/lib/mono/2.0/
# nunit-console.exe.config
#
# by adding the following in the <configSections> area:

# <section name="appSettings" type="System.Configuration.AppSettingsSection,
# System.Configuration, Version=2.0.0.0, Culture=neutral,
# PublicKeyToken=b03f5f7f11d50a3a" />
# 
# <section name="system.diagnostics"
# type="System.Diagnostics.DiagnosticsConfigurationHandler, System,
# Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" />
# 
# <section name="startup" type="System.Configuration.IgnoreSection,
# System.Configuration, Version=2.0.0.0, Culture=neutral,
# PublicKeyToken=b03f5f7f11d50a3a" allowLocation="false"/> 
# 
# <section name="runtime" type="System.Configuration.IgnoreSection,
# System.Configuration, Version=2.0.0.0, Culture=neutral,
# PublicKeyToken=b03f5f7f11d50a3a" allowLocation="false"/>
#
# This script will ensure that the old version of mono is used to launch the
# version of nunit-console that comes with Unity, and that it runs against the
# forked version of Mono that Unity includes.  If you do not have Unity3D
# installed in the standard location you can use the environment variable
# $UNITY_HOME to set the location

config=Debug

nunit_console_args=""
unity_location=${UNITY_HOME:?"/Applications/Unity"}
unity_location=${unity_location%%/}
while getopts rt:f: FLAG; do
  case $FLAG in
    r) config=Release;;
    t) nunit_console_args="-run $OPTARG";;
    f) nunit_console_args="-load $OPTARG";;
  esac
done

echo "Executing nunit-console $nunit_console_args"
echo

export MONO_PATH=$unity_location/Unity.app/Contents/Frameworks/Mono/lib/mono/2.0 

$unity_location/Unity.app/Contents/Frameworks/Mono/bin/mono \
$unity_location/Unity.app/Contents/Frameworks/Mono/lib/mono/2.0/nunit-console.exe \
../src/Couchbase.Lite.Net35.Tests/bin/$config/Couchbase.Lite.Net35.Tests.dll \
-labels $nunit_console_args
