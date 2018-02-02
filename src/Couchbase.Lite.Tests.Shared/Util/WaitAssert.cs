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
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    internal sealed class WaitAssert
    {
        private ManualResetEvent _mre = new ManualResetEvent(false);
        private Exception _caughtException;

        public IList<Exception> CaughtExceptions { get; } = new List<Exception>();

        public static void WaitFor(TimeSpan timeout, params WaitAssert[] asserts)
        {
            foreach (var assert in asserts) {
                assert.WaitForResult(timeout);
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
                await Task.Factory.StartNew(assertAction).ConfigureAwait(false);
            }
            catch (Exception e) {
                _caughtException = e;
                CaughtExceptions.Add(e);
            }
            finally {
                _mre.Set();
            }
        }

        public async Task RunAssertAsync<T>(Action<T> assertAction, T arg)
        {
            try {
                await Task.Factory.StartNew(() => assertAction(arg));
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
            if (!_mre.WaitOne(timeout)) {
                throw new TimeoutException("Timeout waiting for WaitAssert");
            }

            if (_caughtException != null) {
                throw _caughtException;
            }
        }
    }
}
