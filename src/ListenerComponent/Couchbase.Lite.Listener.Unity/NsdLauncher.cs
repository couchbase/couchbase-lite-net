//
//  NsdLauncher.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using UnityEngine;

namespace Couchbase.Lite.Listener.Unity
{
    // This class is not compiled, but here for reference.  I didn't want to have a project for every
    // Unity platform so I use a script to modify the outputted DLL.  This class is needed for android
    // but forbidden on ios.  It will be injected as IL later when I run a script to generate the Unity
    // platform assemblies
    public static class NsdLauncher
    {
        public static void StartNsd()
        {
            AndroidJavaClass c = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var context = c.GetStatic<AndroidJavaObject>("currentActivity");
            var arg = new AndroidJavaObject("java.lang.String", "servicediscovery");
            context.Call<AndroidJavaObject>("getSystemService", arg);

            context.Dispose();
            arg.Dispose();
            c.Dispose();
        }
    }
}
