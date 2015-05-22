### Couchbase Lite for Unity3D

This project builds Couchbase Lite for use inside of a Unity3D project.  However, there are several things that you must do to ensure that it works correctly.

- **Get a copy of UnityEngine.dll**
  - For whatever reason, Xamarin Studio won't honor MSBuild targets when building against .NET 3.5, but xbuild on the command line will so do one of the following
    - Run xbuild on the Couchbase.Lite.Unity.csproj project
    - Copy UnityEngine.dll from your Unity installation into the Couchbase.Lite.Unity/vendor/Unity folder
    
- **Setup platform specific DLL files**
  - Couchbase Lite uses SQLite, which is platform-specific and needs to bind to the correct native library on each platform (Standalone, iOS, Android).  There is a DLL file for each of these.  Inside of the Couchbase.Lite.Net35/vendor/SQLitePCL folder, there is SQLitePCL.raw.dll for standalone, as well as iOS/SQLitePCL.raw.dll and Android/SQLitePCL.raw.dll for iOS and Android.  These need to be imported into Unity and set up to be applied only to those platforms.  Furthermore, in the x64 and x86 folders lives the native SQLite library for Windows (the only platform in which it is not preinstalled or easily installed in a shared location).  These also need to be imported into Unity.
  
- **Project Settings**
  - You must build with the Unity .NET 2.0 API compatibility level (subset will not compile, and will throw TypeInitializationExceptions and such). You can find this setting under the player settings of each player (it is a shared setting so if you change it once, it changes everywhere). You must also add this string to the AOT compilation options under the iOS player settings: nimt-trampolines=8096,ntrampolines=8096. Furthermore, due to the way that Unity collapses its native plugins, this line is needed somewhere in your scripts to ensure that Windows can find the native sqlite3.dll files.

```c#
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    SQLitePCL.SQLite3Provider.SetDllDirectory (Path.Combine(Application.dataPath, "Plugins"));
#endif
```
