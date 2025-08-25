// 
//  BackgroundMonitor.cs
// 
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

#if __IOS__ && !MACCATALYST

using CoreFoundation;
using Foundation;
using System;
using System.IO;
using System.Threading;

using UIKit;

namespace Couchbase.Lite.Sync;

internal sealed class BackgroundMonitor
{
    private nint _bgTask = UIApplication.BackgroundTaskInvalid;
    private NSObject? _bgObserver;
    private NSObject? _fgObserver;
    private readonly Lock _locker = new Lock();

    public event EventHandler? OnAppBackgrounding;
    public event EventHandler? OnAppForegrounding;
    public event EventHandler? OnBackgroundTaskExpired;

    public void Start()
    {
        if(IsRunningInAppExtension()) {
            return;
        }

        _bgObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidEnterBackgroundNotification, AppBackgrounding);
        _fgObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillEnterForegroundNotification, AppForegrounding);

        // Already in the background? Better start a background session now:
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            if(UIApplication.SharedApplication.ApplicationState == UIApplicationState.Background) {
                AppBackgrounding(null);
            }
        });
    }

    public void Stop()
    {
        if (IsRunningInAppExtension()) {
            return;
        }

        if (_bgObserver != null) {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_bgObserver);
            _bgObserver = null;
        }

        if(_fgObserver != null) {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_fgObserver);
            _fgObserver = null;
        }

        EndBackgroundTask();
    }

    public bool EndBackgroundTask()
    {
        if (IsRunningInAppExtension()) {
            return false;
        }

        lock(_locker) {
            if(_bgTask == UIApplication.BackgroundTaskInvalid) {
                return false;
            }

            UIApplication.SharedApplication.EndBackgroundTask(_bgTask);
            _bgTask = UIApplication.BackgroundTaskInvalid;
            return true;
        }
    }

    public bool BeginBackgroundTask(string name)
    {
        if (IsRunningInAppExtension()) {
            return false;
        }

        lock (_locker) {
            if (_bgTask == UIApplication.BackgroundTaskInvalid) {
                _bgTask = UIApplication.SharedApplication.BeginBackgroundTask(name, () =>
                {
                    if (_bgTask != UIApplication.BackgroundTaskInvalid) {
                        OnBackgroundTaskExpired?.Invoke(this, EventArgs.Empty);
                        EndBackgroundTask();
                    }
                });
            }

            return _bgTask != UIApplication.BackgroundTaskInvalid;
        }
    }

    public bool HasBackgroundTask()
    {
        lock(_locker) {
            return _bgTask != UIApplication.BackgroundTaskInvalid;
        }
    }

    private static bool IsRunningInAppExtension() => Path.GetExtension(NSBundle.MainBundle.BundlePath) == "appex";

    private void AppBackgrounding(NSNotification? notification)
    {
        OnAppBackgrounding?.Invoke(this, EventArgs.Empty);
    }

    private void AppForegrounding(NSNotification? notification)
    {
        OnAppForegrounding?.Invoke(this, EventArgs.Empty);
    }
}

#endif
