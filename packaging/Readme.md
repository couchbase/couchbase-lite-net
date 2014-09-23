How to build and push the Nuget component:

1. Build the Net45, iOS, and Android projects in release mode by using the Nuget build scheme.
2. Update the nuspec file, located in folder `packaging > Nuget`
3. Update the version, following semver guidelines.
4. Update the release notes.
5. Run `cd packaging/nuget`
6. Create the new package using the command: `./create_package.sh`
7. Upload the package to nuget.or using the command: nuget push Couchbase.Lite.{version}.nupkg

Note : As a one-time step before doing any steps above, run nuget SetApiKey to set the api key.

How to build the Xamarin component:

 1. Download the [xamarin-component.exe](https://components.xamarin.com/submit/xpkg) tool.
 2. Change your working path to the `packaging/component/` folder.
 3. Run `mono ~/Xamarin/Tools/xamarin-component.exe package`.
 4. You will now have `couchbase-lite-net-1.x.y.z.xam` in your folder.
 5. To install the component locally for testing, run `mono ~/Xamarin/Tools/xamarin-component.exe install couchbase-lite-1.x.y.z.xam `
 6. Navigate to Components > Edit Components... in your IDE, and you will see the component listed under the 'Installed on this machine' heading.
