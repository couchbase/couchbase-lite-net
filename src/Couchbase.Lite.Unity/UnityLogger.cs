//
//  UnityTraceListener.cs
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
using Couchbase.Lite.Util;
using System.Diagnostics;

namespace Couchbase.Lite.Unity
{
    public sealed class UnityLogger : ILogger
    {
        private readonly SourceLevels _level;

        public UnityLogger() : this(SourceLevels.Information)
        {

        }

        public UnityLogger(SourceLevels logLevel)
        {
            _level = logLevel;
        }

        static Exception Flatten (Exception tr)
        {
            if (!(tr is AggregateException))
                return tr;
            var err = ((AggregateException)tr).Flatten().InnerException;
            return err;
        }

        public void V (string tag, string msg)
        {
            if (!(_level.HasFlag(SourceLevels.Verbose)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogFormat("{0}: {1}", tag, msg);
            });
        }

        public void V (string tag, string msg, Exception tr)
        {
            if (!(_level.HasFlag(SourceLevels.Verbose)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogFormat("{0}: {1}: {2}", tag, msg, Flatten(tr));
            });
        }

        public void V (string tag, string format, params object[] args)
        {
            V(tag, string.Format(format, args));
        }

        public void D (string tag, string format, params object[] args)
        {
            D(tag, string.Format(format, args));
        }

        public void D (string tag, string msg)
        {
            if (!(_level.HasFlag(SourceLevels.ActivityTracing)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogFormat("{0}: {1}", tag, msg);
            });
        }

        public void D (string tag, string msg, Exception tr)
        {
            if (!(_level.HasFlag(SourceLevels.ActivityTracing)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogFormat("{0}: {1}: {2}", tag, msg, Flatten(tr));
            });
        }

        public void I (string tag, string msg)
        {
            if (!(_level.HasFlag(SourceLevels.Information)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogFormat("{0}: {1}", tag, msg);
            });
        }

        public void I (string tag, string msg, Exception tr)
        {
            if (!(_level.HasFlag(SourceLevels.Information)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogFormat("{0}: {1}: {2}", tag, msg, Flatten(tr));
            });
        }

        public void I (string tag, string format, params object[] args)
        {
            I(tag, string.Format(format, args));
        }

        public void W (string tag, string msg)
        {
            if (!(_level.HasFlag(SourceLevels.Warning)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogWarningFormat("{0}: {1}", tag, msg);
            });
        }

        public void W (string tag, Exception tr)
        {
            if (!(_level.HasFlag(SourceLevels.Warning)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogWarningFormat("{0}: {1}", tag, Flatten(tr).Message);
            });
        }

        public void W (string tag, string msg, Exception tr)
        {
            if (!(_level.HasFlag(SourceLevels.Warning)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogWarningFormat("{0}: {1}: {2}", tag, msg, Flatten(tr));
            });
        }

        public void W (string tag, string format, params object[] args)
        {
            W(tag, string.Format(format, args));
        }

        public void E (string tag, string msg)
        {
            if (!(_level.HasFlag(SourceLevels.Error)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogErrorFormat("{0}: {1}", tag, msg);
            });
        }

        public void E (string tag, string msg, Exception tr)
        {
            if (!(_level.HasFlag(SourceLevels.Error)))
                return;

            UnityMainThreadScheduler.TaskFactory.StartNew(() =>
            {
                UnityEngine.Debug.LogErrorFormat("{0}: {1}: {2}", tag, msg, Flatten(tr));
            });
        }

        public void E (string tag, string format, params object[] args)
        {
            E(tag, string.Format(format, args));
        }
    }
}

