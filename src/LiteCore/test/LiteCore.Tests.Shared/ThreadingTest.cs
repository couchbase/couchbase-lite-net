// 
//  ThreadingTest.cs
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
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Couchbase.Lite.Interop;

using FluentAssertions;
using LiteCore.Interop;

using ObjCRuntime;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace LiteCore.Tests
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public unsafe class ThreadingTest : Test
    {
        private bool Log = false;
        private const int NumDocs = 10000;
        private const bool SharedHandle = false; // Use same C4Database on all threads_
        private object _observerMutex = new object();
        private static readonly C4DatabaseObserverCallback ObserverCallback = ObsCallback;
        private bool _changesToObserve;

#if !WINDOWS_UWP
        public ThreadingTest(ITestOutputHelper output) : base(output)
        {

        }
#endif

        [Fact]
        public void TestCreateVsEnumerate()
        {
            RunTestVariants(() => {
                var task1 = Task.Factory.StartNew(AddDocsTask);
                var task2 = Task.Factory.StartNew(ObserverTask);

                Task.WaitAll(task1, task2);
            });
        }

        private void AddDocsTask()
        {
            // This implicitly uses the 'db' connection created (but not used) by the main thread
            if(Log) {
                WriteLine("Adding documents...");
            }

            for(int i = 1; i <= NumDocs; i++) {
                if(Log) {
                    Write($"({i}) ");
                } else if(i%10 == 0) {
                    Write(":");
                }

                var docID = $"doc-{i:D5}";
                CreateRev(docID, RevID, FleeceBody);
            }
            WriteLine();
        }

        private void ObserverTask()
        {
            var database = OpenDB();
            var handle = GCHandle.Alloc(this);
            var observer = Native.c4dbobs_create(database, ObserverCallback, GCHandle.ToIntPtr(handle).ToPointer());
            var lastSequence = 0UL;

            try {
                do {
                    lock (_observerMutex)
                    {
                        if (!_changesToObserve)
                        {
                            continue;
                        }

                        Write("8");
                        _changesToObserve = false;
                    }

                    var changes = new C4DatabaseChange[10];
                    uint nDocs;
                    bool external;
                    while (0 < (nDocs = Native.c4dbobs_getChanges(observer, changes, 10U, &external)))
                    {
                        try
                        {
                            external.Should().BeTrue("because all changes will be external in this test");
                            for (int i = 0; i < nDocs; ++i)
                            {
                                changes[i].docID.CreateString().Should().StartWith("doc-",
                                    "because otherwise the document ID is not what we created");
                                lastSequence = changes[i].sequence;
                            }
                        }
                        finally
                        {
                            Native.c4dbobs_releaseChanges(changes, nDocs);
                        }
                    }
                    
                    Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
                } while (lastSequence < NumDocs);
            } finally {
                Native.c4dbobs_free(observer);
                handle.Free();
                CloseDB(database);
            }
        }

        [MonoPInvokeCallback(typeof(C4DatabaseObserverCallback))]
        private static void ObsCallback(C4DatabaseObserver* observer, void* context)
        {
            var obj = GCHandle.FromIntPtr((IntPtr) context).Target as ThreadingTest;
            obj.Observe(observer);
        }

        private void Observe(C4DatabaseObserver* observer)
        {
            Write("!");
            lock(_observerMutex) {
                _changesToObserve = true;
            }
        }

        private C4Database* OpenDB()
        {
            var database = (C4Database *)LiteCoreBridge.Check(err => Native.c4db_open(DatabasePath(), 
                Native.c4db_getConfig(Db), err));
            return database;
        }

        private void CloseDB(C4Database* db)
        {
            Native.c4db_close(db, null);
            Native.c4db_free(db);
        }
    }
}