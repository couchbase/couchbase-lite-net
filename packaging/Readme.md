The Xamarin component can be build from the command line.

 1. Download the `xamarin-component.exe` tool.
 2. Change your working path to the `packaging/component/` folder.
 3. Run `mono ~/Xamarin/Tools/xamarin-component.exe package`.
 4. You will now have `couchbase-lite-net-1.x.y.z.xam` in your folder.
 5. To install the component locally for testing, run `mono ~/Xamarin/Tools/xamarin-component.exe install couchbase-lite-1.x.y.z.xam `
 6. Navigate to Components > Edit Components... in your IDE, and you will see the component listed under the 'Installed on this machine' heading.
