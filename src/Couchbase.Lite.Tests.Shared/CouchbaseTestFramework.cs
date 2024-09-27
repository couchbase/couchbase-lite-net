//
// CouchbaseTestFramework.cs
//
// Copyright (c) 2023 Couchbase, Inc All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Test
{
    // The final level after all the factories (to read this file, start at the bottom)
    // in which a single test is ready for execution, and any final processing is done.
    // We use this opportunity to log some messages (notably that the test started) for 
    // diagnostic purposes.
    internal sealed class CouchbaseTestMethodRunner : XunitTestMethodRunner
    {
        private readonly IMessageSink _diagnosticSink;

        public CouchbaseTestMethodRunner(ITestMethod method, IReflectionTypeInfo classInfo, IReflectionMethodInfo methodInfo,
            IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticSink, IMessageBus messageBus, ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource, object[] constructorArgs)
            : base(method, classInfo, methodInfo, testCases, diagnosticSink, messageBus, aggregator, cancellationTokenSource, constructorArgs)
        {
            _diagnosticSink = diagnosticSink;
        }

        protected override async Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
        {
            _diagnosticSink.OnMessage(new DiagnosticMessage($"Starting {testCase.TestMethod.Method.Name}..."));

            try {
                var result = await base.RunTestCaseAsync(testCase);
                var status = result.Failed > 0 ? "FAIL" : "PASS";
                _diagnosticSink.OnMessage(new DiagnosticMessage($"[{status}] {testCase.TestMethod.Method.Name}"));
                return result;
            } catch (Exception e) {
                _diagnosticSink.OnMessage(new DiagnosticMessage($"[ERROR] {testCase.TestMethod.Method.Name}"));
                _diagnosticSink.OnMessage(new DiagnosticMessage(e.ToString()));
                throw;
            }
        }
    }

    // Factory level 5, in which the discovered tests in a given class are grouped by test method name
    // and processed before being sent to the final level.  Unless there are variations in the passed
    // arguments for a test (aka Theory) this probably will only result in one test being sent on.  We 
    // do no processing at the moment.
    internal sealed class CouchbaseTestClassRunner : XunitTestClassRunner
    {
        public CouchbaseTestClassRunner(ITestClass testClass, IReflectionTypeInfo classInfo, IEnumerable<IXunitTestCase> testCases, 
            IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, 
            CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings)
            : base(testClass, classInfo, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
        {

        }

        protected override Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, 
            object[] constructorArguments)
        {
            var methodRunner = new CouchbaseTestMethodRunner(testMethod, Class, method, testCases, DiagnosticMessageSink, MessageBus, Aggregator,
                CancellationTokenSource, constructorArguments);
            return methodRunner.RunAsync();
        }
    }

    // Factory level 4, in which the discovered tests in a given collection are grouped by test class name
    // and processed before being sent onto level 5 (we do no processing at the moment)
    internal sealed class CouchbaseTestCollectionRunner : XunitTestCollectionRunner
    {
        public CouchbaseTestCollectionRunner(ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, 
            IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
        {

        }

        protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
        {
            DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"Starting class {testClass.Class.Name}..."));
            var classRunner = new CouchbaseTestClassRunner(testClass, @class, testCases, DiagnosticMessageSink, MessageBus, TestCaseOrderer,
                Aggregator, CancellationTokenSource, CollectionFixtureMappings);
            return classRunner.RunAsync().ContinueWith(t =>
            {
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"Finished class {testClass.Class.Name}..."));
                return t.Result;
            });
        }
    }

    // Factory level 3, in which the discovered tests are grouped by collection and each collection processed
    // before being sent onto level 4 (we do no processing at the moment)
    internal sealed class CouchbaseAssemblyRunner : XunitTestAssemblyRunner
    {
        public CouchbaseAssemblyRunner(ITestAssembly testAssembly, IEnumerable<IXunitTestCase> testCases, IMessageSink diagnosticMessageSink, 
            IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
            : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        {
        }

        protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, 
            IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
        {
            var collectionRunner = new CouchbaseTestCollectionRunner(testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer,
                Aggregator, cancellationTokenSource);
            return collectionRunner.RunAsync();
        }
    }

    // Factory level 2, in which all the discovered test cases in a given assembly are processed
    // before being sent onto level 3 (we do no processing at the moment, just sent them on as-is)
    internal sealed class CouchbaseExecutor : XunitTestFrameworkExecutor
    {
        public CouchbaseExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink messageSink)
            : base(assemblyName, sourceInformationProvider, messageSink)
        {
            
        }

        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using var assemblyRunner = new CouchbaseAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
            await assemblyRunner.RunAsync();
        }
    }

    // This is where we start injecting custom behavior into Xunit.  It is the factory
    // that starts the chain of factories all the way down to the exact method invocation
    // to use for each test.  Each assembly to be processed by Xunit will be sent to level 2.
    public sealed class CouchbaseTestFramework : XunitTestFramework
    {
        public CouchbaseTestFramework(IMessageSink messageSink)
           : base(messageSink)
        {
            messageSink.OnMessage(new DiagnosticMessage("Using CouchbaseTestFramework..."));
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new CouchbaseExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
        }
    }
}
