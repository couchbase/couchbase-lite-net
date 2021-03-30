// 
//  TestBase.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [TestClass]
#endif
    public abstract class TestBase
    {
#if !WINDOWS_UWP
        protected readonly ITestOutputHelper _output;
#else
        protected TestContext _output;

        public virtual TestContext TestContext
        {
            get => _output;
            set => _output = value;
        }
#endif
        private StringBuilder _sb = new StringBuilder();

        protected abstract int NumberOfOptions { get; }

        protected Exception CurrentException { get; private set; }

        protected abstract void SetupVariant(int option);
        protected abstract void TeardownVariant(int option);

#if !WINDOWS_UWP
        protected TestBase(ITestOutputHelper output)
        {
            _output = output;
        }
#endif

        protected void WriteLine(string line = "")
        {
            // StringBuilder is not threadsafe
            lock (_sb) {
                _output.WriteLine($"{_sb}{line}");
            }
        }

        protected void Write(string str)
        { 
            // StringBuilder is not threadsafe
            lock (_sb) {
                _sb.Append(str);
            }
        }

        protected void RunTestVariants(Action a, [CallerMemberName]string caller = null)
        {
            var exceptions = new ConcurrentDictionary<int, List<Exception>>();
            WriteLine($"Begin {caller}");
            for(int i = 0; i < NumberOfOptions; i++) {
                CurrentException = null;
                SetupVariant(i);
                try {
                    a();
                } catch(Exception e) {
                    CurrentException = e;
                    throw;
                } finally {
                    try {
                        WriteLine("Finished variant");
                        TeardownVariant(i);
                    } catch(Exception e) {
                        WriteLine($"Warning: error tearing down {e}");
                    }
                }
            }
        }
    }
}
