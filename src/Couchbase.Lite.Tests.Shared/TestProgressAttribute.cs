//
// TestProgressAttribute.cs
//
// Copyright (c) 2026 Couchbase, Inc All rights reserved.
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
using System.Reflection;

using Xunit;
using Xunit.Sdk;
using Xunit.v3;

// Applied to every assembly that imports this shared project, so that a hung test can be
// identified from the run log.  This replaces the old CouchbaseTestFramework, which hooked
// the same diagnostics into runner internals that xUnit v3 no longer exposes.
[assembly: Test.TestProgress]

namespace Test;

/// <summary>
/// Writes a diagnostic message before and after every test, so that a run which stops
/// producing output identifies the test it is stuck in.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true, Inherited = true)]
public sealed class TestProgressAttribute : BeforeAfterTestAttribute
{
    /// <inheritdoc />
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        TestContext.Current.SendDiagnosticMessage("Starting {0}...", test.TestDisplayName);
    }

    /// <inheritdoc />
    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        // After() runs before the outcome is finalized for some failure modes, so treat a
        // missing state as "finished" rather than reporting a false PASS.
        var status = TestContext.Current.TestState?.Result switch
        {
            TestResult.Passed => "PASS",
            TestResult.Failed => "FAIL",
            TestResult.Skipped => "SKIP",
            _ => "DONE"
        };

        TestContext.Current.SendDiagnosticMessage("[{0}] {1}", status, test.TestDisplayName);
    }
}
