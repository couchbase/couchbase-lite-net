//
// WaitAssert.cs
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class WaitAssert : IDisposable
    {
        private ManualResetEventSlim _mre = new ManualResetEventSlim();
        private Exception _caughtException;

        public IList<Exception> CaughtExceptions { get; } = new List<Exception>();

        public static bool WaitAll(IEnumerable<ManualResetEventSlim> handles, TimeSpan timeout)
        {
            bool allSignaled = false;
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
                // STA thread workaround
                var startTime = DateTime.Now;
                while (DateTime.Now - startTime < timeout) {
                    if(handles.All(h => h.IsSet)) { 
                        allSignaled = true;
                        break;
                    }
                    Thread.Sleep(100);
                }
            } else {
                // MTA thread can use WaitAll directly
                allSignaled = WaitHandle.WaitAll(
                    handles.Select(x => x.WaitHandle).ToArray(),
                    TimeSpan.FromSeconds(20)
                );
            }

            return allSignaled;
        }

        public static void WaitFor(TimeSpan timeout, params WaitAssert[] asserts)
        {
            if(!WaitAll(asserts.Select(x => x._mre), timeout)) {
                throw new TimeoutException("Timeout waiting for array of WaitAsserts");
            }
        }

        public void RunAssert(Action assertAction)
        {
            try {
                assertAction();
            } catch (Exception e) {
                _caughtException = e;
                CaughtExceptions.Add(e);
            } finally {
                _mre.Set();
            }
        }

        public async Task RunAssertAsync(Action assertAction)
        {
            try {
                await Task.Run(assertAction).ConfigureAwait(false);
            } catch (Exception e) {
                _caughtException = e;
                CaughtExceptions.Add(e);
            } finally {
                _mre.Set();
            }
        }

        public async Task RunAssertAsync<T>(Action<T> assertAction, T arg)
        {
            try {
                await Task.Run(() => assertAction(arg)).ConfigureAwait(false);
            } catch (Exception e) {
                _caughtException = e;
                CaughtExceptions.Add(e);
            } finally {
                _mre.Set();
            }
        }

        public void RunConditionalAssert(Func<bool> assertAction)
        {
            var done = false;
            try {
                done = assertAction();
            } catch (Exception e) {
                _caughtException = e;
            } finally {
                if (done || _caughtException != null) {
                    _mre.Set();
                }
            }
        }

        public void Fulfill()
        {
            _mre.Set();
        }

        public void WaitForResult(TimeSpan timeout)
        {
            if (!_mre.Wait(timeout)) {
                throw new TimeoutException("Timeout waiting for WaitAssert");
            }

            if (_caughtException != null) {
                throw _caughtException;
            }
        }

        public void Dispose()
        {
            _mre.Dispose();
        }
    }
}
