// 
//  LogTest.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using Couchbase.Lite;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;

using FluentAssertions;

using JetBrains.Annotations;

using Test.Util;
#if !WINDOWS_UWP
using Xunit;
using Xunit.Abstractions;
#else
using Fact = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

namespace Test
{
#if WINDOWS_UWP
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
#endif
    public sealed class LogTest
    {
#if NETCOREAPP2_0
        static LogTest()
        {
            Couchbase.Lite.Support.NetDesktop.Activate();
        }
#endif

#if !WINDOWS_UWP
        public LogTest(ITestOutputHelper output)
        {
            Database.Log.Custom = new XunitLogger(output) { Level = LogLevel.Info };
        }
#endif

#if WINDOWS_UWP
        private TestContext _testContext;
        public TestContext TestContext
        {
            get => _testContext;
            set {
                _testContext = value;
                Database.Log.Custom = new MSTestLogger(_testContext) { Level = LogLevel.Info };
            }
        }
#endif

        [Fact]
        public void TestDefaultLogFormat()
        {
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestDefaultLogFormat"));
            TestWithConfiguration(LogLevel.Info, new LogFileConfiguration(logDirectory), () =>
            {
                WriteLog.To.Database.I("TEST", "MESSAGE");
                var logFilePath = Directory.EnumerateFiles(logDirectory, "cbl_info_*").LastOrDefault();
                logFilePath.Should().NotBeNullOrEmpty();
                var logContent = ReadAllBytes(logFilePath);
                logContent.Should().StartWith(new byte[] { 0xcf, 0xb2, 0xab, 0x1b },
                    "because the log should be in binary format");
            });
        }

        [Fact]
        public void TestPlaintext()
        {
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestPlaintext"));
            var config = new LogFileConfiguration(logDirectory, Database.Log.File.Config)
            {
                UsePlaintext = true
            };

            TestWithConfiguration(LogLevel.Info, config, () =>
            {
                Database.Log.File.Config = new LogFileConfiguration(logDirectory, Database.Log.File.Config)
                {
                    UsePlaintext = true
                };

                WriteLog.To.Database.I("TEST", "MESSAGE");
                var logFilePath = Directory.EnumerateFiles(logDirectory, "cbl_info_*").LastOrDefault();
                logFilePath.Should().NotBeNullOrEmpty();
                var logContent = ReadAllLines(logFilePath);
                logContent.Any(x => x.Contains("MESSAGE") && x.Contains("TEST"))
                    .Should().BeTrue("because the message should show up in plaintext");
            });
        }

        [Fact]
        public void TestMaxSize()
        {
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestMaxSize"));
            var config = new LogFileConfiguration(logDirectory)
            {
                UsePlaintext = true,
                MaxSize = 1024
            };

            TestWithConfiguration(LogLevel.Debug, config, () =>
            {
                for (int i = 0; i < 45; i++) {
                    WriteLog.To.Database.E("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.W("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.I("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.V("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.D("TEST", $"MESSAGE {i}");
                }
                
                var totalCount = (Database.Log.File.Config.MaxRotateCount + 1) * 5;
#if !DEBUG
                totalCount -= 1; // Non-debug builds won't log debug files
#endif

                Directory.EnumerateFiles(logDirectory).Should()
                    .HaveCount(totalCount, "because old log files should be getting pruned");
            });
        }

        [Fact]
        public void TestDisableLogging()
        {
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestDisableLogging"));
            var config = new LogFileConfiguration(logDirectory)
            {
                UsePlaintext = true
            };

            TestWithConfiguration(LogLevel.None, config, () =>
            {
                var sentinel = Guid.NewGuid().ToString();
                WriteLog.To.Database.E("TEST", sentinel);
                WriteLog.To.Database.W("TEST", sentinel);
                WriteLog.To.Database.I("TEST", sentinel);
                WriteLog.To.Database.V("TEST", sentinel);
                WriteLog.To.Database.D("TEST", sentinel);
                foreach (var file in Directory.EnumerateFiles(logDirectory)) {
                    foreach (var line in ReadAllLines(file)) {
                        line.Should().NotContain(sentinel);
                    }
                }
            });
        }

        [Fact]
        public void TestReEnableLogging()
        {
            TestDisableLogging();
            var sentinel = Guid.NewGuid().ToString();
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "ReEnableLogs"));
            var config = new LogFileConfiguration(logDirectory)
            {
                UsePlaintext = true
            };

            TestWithConfiguration(LogLevel.Verbose, config, () =>
            {
                WriteLog.To.Database.E("TEST", sentinel);
                WriteLog.To.Database.W("TEST", sentinel);
                WriteLog.To.Database.I("TEST", sentinel);
                WriteLog.To.Database.V("TEST", sentinel);

                foreach (var file in Directory.EnumerateFiles(logDirectory)) {
                    if (file.Contains("debug")) {
                        continue;
                    }

                    var found = false;
                    foreach (var line in ReadAllLines(file)) {
                        if (line.Contains(sentinel)) {
                            found = true;
                            break;
                        }
                    }

                    found.Should().BeTrue();
                }
            });
        }

        [Fact]
        public void TestLogFilename()
        {
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestLogFilename"));
            var config = new LogFileConfiguration(logDirectory);
            TestWithConfiguration(LogLevel.Info, config, () =>
            {
                var allFiles = Directory.EnumerateFiles(logDirectory, "*.cbllog").ToArray();
                var regex = new Regex($"cbl_(debug|verbose|info|warning|error)_\\d+\\.cbllog");
                allFiles.Any(x => !regex.IsMatch(x)).Should().BeFalse("because all files should match the pattern");
            });
        }

        [Fact]
        public void TestLogHeader()
        {
            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestLogHeader"));
            var config = new LogFileConfiguration(logDirectory)
            {
                UsePlaintext = true,
                MaxSize = 1024
            };

            TestWithConfiguration(LogLevel.Verbose, config, () =>
            {
                for (int i = 0; i < 45; i++) {
                    WriteLog.To.Database.E("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.W("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.I("TEST", $"MESSAGE {i}");
                    WriteLog.To.Database.V("TEST", $"MESSAGE {i}");
                }

                foreach (var file in Directory.EnumerateFiles(logDirectory, "*.cbllog")) {
                    var lines = ReadAllLines(file);
                    lines[0].Should().Contain("CouchbaseLite/").And.Subject.Should().Contain("Build/")
                        .And.Subject.Should().Contain("Commit/");
                }
            });
        }

#if !__ANDROID__
        [Fact]
        public void TestConsoleLoggingLevels()
        {
            WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message
            Database.Log.Console.Level = LogLevel.None;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            stringWriter.Flush();
            stringWriter.ToString().Should().BeEmpty("because logging is disabled");

            Database.Log.Console.Level = LogLevel.Verbose;
            stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            WriteLog.To.Database.V("TEST", "TEST VERBOSE");
            WriteLog.To.Database.I("TEST", "TEST INFO");
            WriteLog.To.Database.W("TEST", "TEST WARNING");
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            stringWriter.Flush();
            stringWriter.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should()
                .HaveCount(4, "because all levels should be logged");

            var currentCount = 1;
            foreach (var level in new[] { LogLevel.Error, LogLevel.Warning, 
                LogLevel.Info}) {
                Database.Log.Console.Level = level;
                stringWriter = new StringWriter();
                Console.SetOut(stringWriter);
                WriteLog.To.Database.V("TEST", "TEST VERBOSE");
                WriteLog.To.Database.I("TEST", "TEST INFO");
                WriteLog.To.Database.W("TEST", "TEST WARNING");
                WriteLog.To.Database.E("TEST", "TEST ERROR");
                stringWriter.Flush();
                stringWriter.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should()
                    .HaveCount(currentCount, "because {0} levels should be logged for {1}", currentCount, level);
                currentCount++;
            }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            Database.Log.Console.Level = LogLevel.Warning;
        }

        [Fact]
        public void TestConsoleLoggingDomains()
        {
            WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message
            Database.Log.Console.Domains = LogDomain.None;
            Database.Log.Console.Level = LogLevel.Info;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            foreach (var domain in WriteLog.To.All) {
                domain.I("TEST", "TEST MESSAGE");
            }

            stringWriter.Flush();
            stringWriter.ToString().Should().BeEmpty("because all domains are disabled");
            foreach (var domain in WriteLog.To.All) {
                Database.Log.Console.Domains = domain.Domain;
                stringWriter = new StringWriter();
                Console.SetOut(stringWriter);
                foreach (var d in WriteLog.To.All) {
                    d.I("TEST", "TEST MESSAGE");
                }

                stringWriter.Flush();
                stringWriter.ToString().Should().Match(x => x.Contains(domain.Domain.ToString()));
            }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            Database.Log.Console.Domains = LogDomain.All;
            Database.Log.Console.Level = LogLevel.Warning;
        }

        [Fact]
        public void TestFileLogDisabledWarning()
        {
            Database.Log.File.Config.Should().BeNull();
            var sw = new StringWriter();
            Console.SetOut(sw);
            using (var db = new Database("tmp")) {
                sw.ToString().Contains("file logging is disabled").Should().BeTrue();
                db.Delete();
            }

            sw.Dispose();
            sw = new StringWriter();
            Console.SetOut(sw);
            Database.Log.File.Config = new LogFileConfiguration("foo");
            Database.Log.File.Config = null;
            sw.ToString().Contains("file logging is disabled").Should().BeTrue();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
        }
#endif

        [Fact]
        public void TestCustomLoggingLevels()
        {
            var customLogger = new LogTestLogger();
            WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message
            Database.Log.Custom = customLogger;
            customLogger.Level = LogLevel.None;
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            customLogger.Lines.Should().BeEmpty("because logging level is set to None");
            

            customLogger.Level = LogLevel.Verbose;
            WriteLog.To.Database.V("TEST", "TEST VERBOSE");
            WriteLog.To.Database.I("TEST", "TEST INFO");
            WriteLog.To.Database.W("TEST", "TEST WARNING");
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            customLogger.Lines.Should().HaveCount(4, "because all levels should be logged");
            customLogger.Reset();;

            var currentCount = 1;
            foreach (var level in new[] { LogLevel.Error, LogLevel.Warning, 
                LogLevel.Info}) {
                customLogger.Level = level;
                WriteLog.To.Database.V("TEST", "TEST VERBOSE");
                WriteLog.To.Database.I("TEST", "TEST INFO");
                WriteLog.To.Database.W("TEST", "TEST WARNING");
                WriteLog.To.Database.E("TEST", "TEST ERROR");
                customLogger.Lines.Should()
                    .HaveCount(currentCount, "because {0} levels should be logged for {1}", currentCount, level);
                currentCount++;
                customLogger.Reset();
            }
        }

        [Fact]
        public void TestPlaintextLoggingLevels()
        {
            WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message

            var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestPlaintextLoggingLevels"));
            var config = new LogFileConfiguration(logDirectory)
            {
                UsePlaintext = true,
                MaxRotateCount = 0
            };

            TestWithConfiguration(LogLevel.Info, config, () =>
            {
                foreach (var level in new[]
                    { LogLevel.None, LogLevel.Error, LogLevel.Warning, LogLevel.Info, LogLevel.Verbose }) {
                    Database.Log.File.Level = level;
                    WriteLog.To.Database.V("TEST", "TEST VERBOSE");
                    WriteLog.To.Database.I("TEST", "TEST INFO");
                    WriteLog.To.Database.W("TEST", "TEST WARNING");
                    WriteLog.To.Database.E("TEST", "TEST ERROR");
                }

                foreach (var file in Directory.EnumerateFiles(logDirectory)) {
                    if (file.Contains(LogLevel.Verbose.ToString().ToLowerInvariant())) {
                        ReadAllLines(file).Should()
                            .HaveCount(2, "because there should be 1 log line and 1 meta line");
                    } else if (file.Contains(LogLevel.Info.ToString().ToLowerInvariant())) {
                        ReadAllLines(file).Should()
                            .HaveCount(3, "because there should be 2 log lines and 1 meta line");
                    } else if (file.Contains(LogLevel.Warning.ToString().ToLowerInvariant())) {
                        ReadAllLines(file).Should()
                            .HaveCount(4, "because there should be 3 log lines and 1 meta line");
                    } else if (file.Contains(LogLevel.Error.ToString().ToLowerInvariant())) {
                        ReadAllLines(file).Should()
                            .HaveCount(5, "because there should be 4 log lines and 1 meta line");
                    }
                }
            });
        }

        private void TestWithConfiguration(LogLevel level, LogFileConfiguration config, [NotNull]Action a)
        {
            var old = Database.Log.File.Config;
            Database.Log.File.Config = config;
            Database.Log.File.Level = level;
            try {
                a();
            } finally {
                Database.Log.File.Level = LogLevel.Info;
                Database.Log.File.Config = old;
            }
        }

        [NotNull]
        private static string EmptyDirectory(string path)
        {
            if (String.IsNullOrEmpty(path)) {
                throw new ArgumentOutOfRangeException(nameof(path), "Failed to create path string!");
            }

            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }

            return path;
        }

        private static string[] ReadAllLines(string path)
        {
            var lines = new List<string>();
            using(var fin = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fin)) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        private static byte[] ReadAllBytes(string path)
        {
            using(var fin = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fin)) {
                var retVal = new byte[fin.Length];
                reader.Read(retVal, 0, retVal.Length);
                return retVal;
            }
        }

        private class LogTestLogger : ILogger
        {
            [NotNull]
            private readonly List<string> _lines = new List<string>();

            public IReadOnlyList<string> Lines => _lines;
            public LogLevel Level { get; set; }

            public void Reset()
            {
                _lines.Clear();
            }

            public void Log(LogLevel level, LogDomain domain, string message)
            {
                if (level >= Level) {
                    _lines.Add(message);
                }
            }
        }
    }
}
