// 
//  Replicator+Backgrounding.cs
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

using Couchbase.Lite.Internal.Logging;
using Foundation;
using LiteCore.Interop;
using System;
using System.Threading;
using UIKit;

namespace Couchbase.Lite.Sync;

public partial class Replicator
{
    private BackgroundMonitor? _bgMonitor;
    private NSObject? _dataUnavailableHandler;
    private NSObject? _dataAvailableHandler;
    private bool _filesystemUnavailable;
    private bool _inBackground;
    private bool _suspended;
    private bool _conflictResolutionSuspended;

    private NSFileProtection FileProtection
    {
        get {
#pragma warning disable CS0618 // Type or member is obsolete
            var attrs = NSFileManager.DefaultManager.GetAttributes(Config.Database.Path!);
            return attrs?.ProtectionKey ?? NSFileProtection.None;
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    private unsafe bool Suspended
    {
        get => _suspended;
        set {
            DispatchQueue.DispatchSync(() =>
            {
                _suspended = value;
                if(value && _state > ReplicatorState.Suspending) {
                    // Currently not in any suspend* or stop* state:
                    _state = ReplicatorState.Suspending;
                }

                Native.c4repl_setSuspended(_repl, value);
                SetConflictResolutionSuspended(value);
            });
        }
    }

    private void StartBackgroundingMonitor()
    {
        if(_bgMonitor != null) {
            WriteLog.To.Sync.I(Tag, "Ignored starting backgrounding monitor as already started");
            return;
        }

        WriteLog.To.Sync.I(Tag, "Starting background monitor...");
        var prot = FileProtection;
        if(prot == NSFileProtection.Complete || prot == NSFileProtection.CompleteUnlessOpen) {
            _dataUnavailableHandler = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ProtectedDataWillBecomeUnavailable, FileAccessChanged);
            _dataAvailableHandler = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ProtectedDataDidBecomeAvailable, FileAccessChanged);
        }

        _bgMonitor = new BackgroundMonitor();
        _bgMonitor.OnAppBackgrounding += AppBackgrounding;
        _bgMonitor.OnAppForegrounding += AppForegrounding;
        _bgMonitor.Start();
    }

    private void SetConflictResolutionSuspended(bool suspended)
    {
        _conflictResolutionSuspended = suspended;
        if(suspended) {
            var oldCts = Interlocked.Exchange(ref _conflictCancelSource, new());
            oldCts.Cancel();
        }
    }

    private void FileAccessChanged(NSNotification notification)
    {
        WriteLog.To.Sync.I(Tag, $"Device lock status and file access changed to {notification.Name}");
        _filesystemUnavailable = notification.Name == UIApplication.ProtectedDataWillBecomeUnavailable;
        UpdateSuspended();
    }

    private void AppBackgrounding(object? sender, EventArgs args)
    {
        WriteLog.To.Sync.I(Tag, "App backgrounding, suspending the replicator");
        _inBackground = true;
        UpdateSuspended();
    }

    private void AppForegrounding(object? sender, EventArgs args)
    {
        if (_inBackground) {
            WriteLog.To.Sync.I(Tag, "App foregrounding, resuming the replicator");
            _inBackground = false;
            UpdateSuspended();
        }
        
    }

    private void UpdateSuspended()
    {
        var suspended = _filesystemUnavailable || _inBackground;
        WriteLog.To.Sync.I(Tag, "Update suspended status to {0}", suspended ? "suspended" : "resumed");
        Suspended = suspended;
    }
}

#endif
