// 
//  Activate.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using AndroidX.Startup;
using Android.Content;
using Android.Runtime;
using Java.Lang;
using Object = Java.Lang.Object;

namespace Couchbase.Lite.Support;

/// <summary>
/// Android specific support logic
/// </summary>
public static class Droid
{
    private static int Activated;

    internal static Context? Context { get; private set; }

    /// <summary>
    /// Activates the support classes for Android
    /// </summary>
    /// <param name="context">The main context of the Android application</param>
    /// <exception cref="ArgumentNullException">Thrown if context is <c>null</c></exception>
    /// <exception cref="InvalidProgramException">Thrown if versions of Couchbase.Lite and
    /// Couchbase.Lite.Support.Android do not match</exception>
    public static void Activate(Context context)
    {
        if (Interlocked.Exchange(ref Activated, 1) == 1) {
            return;
        }

        Context = context;
    }

    internal static void CheckVersion()
    {
        var version1 = typeof(Droid).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (version1 == null) {
            throw new InvalidProgramException("This version of Couchbase.Lite.Support.Android has no version!");
        }
            
        var cblAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Couchbase.Lite");
        if (cblAssembly == null) {
            global::Android.Util.Log.Warn("CouchbaseLite", "Failed to detect loaded Couchbase.Lite, skipping version verification...");
            return;
        }

        var version2 = cblAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!version1.Equals(version2)) {
            throw new InvalidProgramException(
                $"Mismatch between Couchbase.Lite ({version2?.InformationalVersion}) and Couchbase.Lite.Support.Android ({version1.InformationalVersion})");
        }
    }
}

public sealed class CouchbaseLiteInitializer : Object, IInitializer
{
    public Object Create(Context context)
    {
        Droid.Activate(context);
        return new Object(IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
    }

    public IList<Class> Dependencies() => [];
}