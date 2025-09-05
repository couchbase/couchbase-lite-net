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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Couchbase.Lite;
using Couchbase.Lite.DI;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Logging;
using Couchbase.Lite.Query;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Test;

public sealed class LogTest(ITestOutputHelper output)
{
    [Fact]
    public void TestDefaultLogFormat()
    {
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestDefaultLogFormat"));
        void Test()
        {
            WriteLog.To.Database.I("TEST", "MESSAGE");
            var logFilePath = Directory.EnumerateFiles(logDirectory, "cbl_info_*").LastOrDefault();
            logFilePath.ShouldNotBeNullOrEmpty();
            var logContent = ReadAllBytes(logFilePath);
            logContent.Take(4).ShouldBe([0xcf, 0xb2, 0xab, 0x1b], "because the log should be in binary format");
        }

        TestWithConfiguration(new FileLogSink(LogLevel.Info, logDirectory), Test);
    }

    [Fact]
    public void TestPlaintext()
    {
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestPlaintext"));

        void Test()
        {
            WriteLog.To.Database.I("TEST", "MESSAGE");
            var logFilePath = Directory.EnumerateFiles(logDirectory, "cbl_info_*").LastOrDefault();
            logFilePath.ShouldNotBeNullOrEmpty();
            var logContent = ReadAllLines(logFilePath);
            logContent.Any(x => x.Contains("MESSAGE") && x.Contains("TEST"))
                .ShouldBeTrue("because the message should show up in plaintext");
        }

        var newSink = new FileLogSink(LogLevel.Info, logDirectory)
        {
            UsePlaintext = true
        };
        TestWithConfiguration(newSink, Test);
    }

    [Fact]
    public void TestMaxSize()
    {
        string logDirectory;
            
        // ReSharper disable once ConvertToConstant.Local
        var totalCount = 10;
#if !DEBUG
            totalCount -= 1; // Non-debug builds won't log debug files
#endif

        void Test()
        {
            for (var i = 0; i < 45; i++) {
                WriteLog.To.Database.E("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.W("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.I("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.V("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.D("TEST", $"MESSAGE {i}");
            }

            Directory.EnumerateFiles(logDirectory)
                .Count().ShouldBe(totalCount, "because old log files should be getting pruned");
        }

        logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestMaxSize"));
        var newSink = new FileLogSink(LogLevel.Debug, logDirectory)
        {
            UsePlaintext = true,
            MaxSize = 1024
        };
        TestWithConfiguration(newSink, Test);
    }

    [Fact]
    public void TestDisableLogging()
    {
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestDisableLogging"));

        void Test()
        {
            var sentinel = Guid.NewGuid().ToString();
            WriteLog.To.Database.E("TEST", sentinel);
            WriteLog.To.Database.W("TEST", sentinel);
            WriteLog.To.Database.I("TEST", sentinel);
            WriteLog.To.Database.V("TEST", sentinel);
            WriteLog.To.Database.D("TEST", sentinel);
            foreach (var file in Directory.EnumerateFiles(logDirectory)) {
                foreach (var line in ReadAllLines(file)) {
                    line.ShouldNotContain(sentinel);
                }
            }
        }

        var newSink = new FileLogSink(LogLevel.None, logDirectory)
        {
            UsePlaintext = true
        };
        TestWithConfiguration(newSink, Test);
    }

    [Fact]
    public void TestReEnableLogging()
    {
        TestDisableLogging();
        var sentinel = Guid.NewGuid().ToString();
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "ReEnableLogs"));
        var newSink = new FileLogSink(LogLevel.Verbose, logDirectory)
        {
            UsePlaintext = true
        };

        TestWithConfiguration(newSink, () =>
        {
            WriteLog.To.Database.E("TEST", sentinel);
            WriteLog.To.Database.W("TEST", sentinel);
            WriteLog.To.Database.I("TEST", sentinel);
            WriteLog.To.Database.V("TEST", sentinel);

            foreach (var file in Directory.EnumerateFiles(logDirectory)) {
                if (file.Contains("debug")) {
                    continue;
                }

                var found = ReadAllLines(file).Any(line => line.Contains(sentinel));
                found.ShouldBeTrue();
            }
        });
    }

#if !SANITY_ONLY
    [Fact]
    [SuppressMessage("Performance", "SYSLIB1045:Convert to \'GeneratedRegexAttribute\'.")]
    public void TestLogFilename()
    {
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestLogFilename"));
        var newSink = new FileLogSink(LogLevel.Info, logDirectory);
        TestWithConfiguration(newSink, () =>
        {
            var allFiles = Directory.EnumerateFiles(logDirectory, "*.cbllog").ToArray();
            var regex = new Regex(@"cbl_(debug|verbose|info|warning|error)_\d+\.cbllog");
            allFiles.Any(x => !regex.IsMatch(x)).ShouldBeFalse("because all files should match the pattern");
        });
    }
#endif

    [Fact]
    public void TestLogHeader()
    {
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestLogHeader"));

        void Test()
        {
            for (var i = 0; i < 45; i++) {
                WriteLog.To.Database.E("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.W("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.I("TEST", $"MESSAGE {i}");
                WriteLog.To.Database.V("TEST", $"MESSAGE {i}");
            }

            foreach (var file in Directory.EnumerateFiles(logDirectory, "*.cbllog")) {
                var lines = ReadAllLines(file);
                foreach (var key in new[] { "serialNo", "logDirectory", "fileLogLevel", "fileMaxSize", "fileMaxCount" }) {
                    lines[0].Contains($"{key}=").ShouldBeTrue("because otherwise a metadata entry is missing on the first line");
                }
                lines[1].Contains("CouchbaseLite/").ShouldBeTrue();
                lines[1].Contains("Build/").ShouldBeTrue();
                lines[1].Contains("Commit/").ShouldBeTrue();
            }
        }

        var newSink = new FileLogSink(LogLevel.Verbose, logDirectory)
        {
            UsePlaintext = true,
            MaxSize = 1024
        };
        TestWithConfiguration(newSink, Test);
    }

#if CBL_PLATFORM_DOTNET || CBL_PLATFORM_DOTNETFX
    [Fact]
    public void TestConsoleLoggingLevels()
    {
        var initialConsoleSink = LogSinks.Console;
        try {
            LogSinks.Console = null;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            stringWriter.Flush();
            stringWriter.ToString().ShouldBeEmpty("because logging is disabled");

            LogSinks.Console = new ConsoleLogSink(LogLevel.Verbose);
            stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            WriteLog.To.Database.V("TEST", "TEST VERBOSE");
            WriteLog.To.Database.I("TEST", "TEST INFO");
            WriteLog.To.Database.W("TEST", "TEST WARNING");
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            stringWriter.Flush();
            stringWriter.ToString().Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Length.ShouldBe(4, "because all levels should be logged");

            var currentCount = 1;
            foreach (var level in new[] { LogLevel.Error, LogLevel.Warning,
                         LogLevel.Info}) {
                LogSinks.Console = new ConsoleLogSink(level);
                stringWriter = new StringWriter();
                Console.SetOut(stringWriter);
                WriteLog.To.Database.V("TEST", "TEST VERBOSE");
                WriteLog.To.Database.I("TEST", "TEST INFO");
                WriteLog.To.Database.W("TEST", "TEST WARNING");
                WriteLog.To.Database.E("TEST", "TEST ERROR");
                stringWriter.Flush();
                stringWriter.ToString().Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                    .Length.ShouldBe(currentCount, $"because {currentCount} levels should be logged for {level}");
                currentCount++;
            }
        } finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            LogSinks.Console = initialConsoleSink;
        }
    }

    [Fact]
    public void TestConsoleLoggingDomains()
    {
        var initialConsoleSink = LogSinks.Console;
        WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message
        try {
            LogSinks.Console = new ConsoleLogSink(LogLevel.Info)
            {
                Domains = LogDomain.None
            };

            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            foreach (var domain in WriteLog.To.All) {
                domain.I("TEST", "TEST MESSAGE");
            }

            stringWriter.Flush();
            stringWriter.ToString().ShouldBeEmpty("because all domains are disabled");
            foreach (var domain in WriteLog.To.All) {
                LogSinks.Console = new ConsoleLogSink(LogLevel.Info)
                {
                    Domains = domain.Domain
                };

                stringWriter = new StringWriter();
                Console.SetOut(stringWriter);
                foreach (var d in WriteLog.To.All) {
                    d.I("TEST", "TEST MESSAGE");
                }

                stringWriter.Flush();
                stringWriter.ToString().Contains(domain.Domain.ToString()).ShouldBeTrue();
            }
        } finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            LogSinks.Console = initialConsoleSink;
        }
    }

    [Fact]
    public void TestFileLogDisabledWarning()
    {
        LogSinks.File.ShouldBeNull();
            
        try {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            var fakePath = Path.Combine(Service.Provider.GetRequiredService<IDefaultDirectoryResolver>().DefaultDirectory(), "foo");

            LogSinks.File = new FileLogSink(LogLevel.Info, fakePath);
            LogSinks.File = null;
            sw.ToString().Contains("file logging is disabled").ShouldBeTrue();
        } finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            LogSinks.File = null;
        }
    }
#endif

    [Fact]
    public void TestCustomLoggingLevels()
    {
        try {
            var customSink = new LogTestSink(output, LogLevel.None);
            WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message
            LogSinks.Custom = customSink;
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            customSink.Lines.ShouldBeEmpty("because logging level is set to None");

            customSink = new LogTestSink(output, LogLevel.Verbose);
            LogSinks.Custom = customSink;
            WriteLog.To.Database.V("TEST", "TEST VERBOSE");
            WriteLog.To.Database.I("TEST", "TEST INFO");
            WriteLog.To.Database.W("TEST", "TEST WARNING");
            WriteLog.To.Database.E("TEST", "TEST ERROR");
            customSink.Lines.Count.ShouldBe(4, "because all levels should be logged");
            customSink.Reset();

            var currentCount = 1;
            foreach (var level in new[] { LogLevel.Error, LogLevel.Warning,
                         LogLevel.Info}) {
                customSink = new LogTestSink(output, level);
                LogSinks.Custom = customSink;
                WriteLog.To.Database.V("TEST", "TEST VERBOSE");
                WriteLog.To.Database.I("TEST", "TEST INFO");
                WriteLog.To.Database.W("TEST", "TEST WARNING");
                WriteLog.To.Database.E("TEST", "TEST ERROR");
                customSink.Lines
                    .Count.ShouldBe(currentCount, $"because {currentCount} levels should be logged for {level}");
                currentCount++;
                customSink.Reset();
            }
        } finally {
            LogSinks.Custom = null;
        }
    }

    private static void VerifyPlaintextLogFile(string path)
    {
        var lines = ReadAllLines(path);
        string textToFind;
        int expectedFindCount;
        if (path.Contains(nameof(LogLevel.Verbose))) {
            textToFind = "TEST VERBOSE";
            expectedFindCount = 1;
        } else if (path.Contains(nameof(LogLevel.Info))) {
            textToFind = "TEST INFO";
            expectedFindCount = 2;
        } else if (path.Contains(nameof(LogLevel.Warning))) {
            textToFind = "TEST WARNING";
            expectedFindCount = 3;
        } else if (path.Contains(nameof(LogLevel.Error))) {
            textToFind = "TEST ERROR";
            expectedFindCount = 4;
        } else {
            return;
        }

        lines[0].Contains("serialNo=").ShouldBeTrue($"because otherwise the first line of {path} is invalid");
        lines[1].Contains("CouchbaseLite/").ShouldBeTrue($"because otherwise the second line of {path} is invalid");
        var foundCount = lines.Skip(2).Count(line => line.Contains(textToFind));
        foundCount.ShouldBe(expectedFindCount, $"because there should be {expectedFindCount} instance of '{textToFind}'");
    }

    [Fact]
    public void TestPlaintextLoggingLevels()
    {
        WriteLog.To.Database.I("IGNORE", "IGNORE"); // Skip initial message
        var logDirectory = EmptyDirectory(Path.Combine(Path.GetTempPath(), "TestPlaintextLoggingLevels"));

        void Test()
        {
            foreach (var file in Directory.EnumerateFiles(logDirectory)) {
                VerifyPlaintextLogFile(file);
            }
        }

        try {
            foreach (var level in new[]
                         { LogLevel.None, LogLevel.Error, LogLevel.Warning, LogLevel.Info, LogLevel.Verbose }) {
                LogSinks.File = new FileLogSink(level, logDirectory)
                {
                    UsePlaintext = true,
                    MaxKeptFiles = 1
                };
                WriteLog.To.Database.V("TEST", "TEST VERBOSE");
                WriteLog.To.Database.I("TEST", "TEST INFO");
                WriteLog.To.Database.W("TEST", "TEST WARNING");
                WriteLog.To.Database.E("TEST", "TEST ERROR");
            }

            Test();
        } finally {
            LogSinks.File = null;
        }
    }

    [Fact]
    public void TestNonAscii()
    {
        // Not worth testing both APIs on this one since
        // It's the actual underlying implementation in LiteCore
        // being tested
        var customSink = new LogTestSink(output, LogLevel.Verbose);
        var initialConsole = LogSinks.Console;
        LogSinks.Custom = customSink;
        LogSinks.Console = new ConsoleLogSink(LogLevel.Verbose);
        try {
            // ReSharper disable once StringLiteralTypo
            const string hebrew = "מזג האוויר נחמד היום"; // The weather is nice today.
            Database.Delete("test_non_ascii", null);
            using var db = new Database("test_non_ascii");
            using var doc = new MutableDocument();
            doc.SetString("hebrew", hebrew);
            db.GetDefaultCollection().Save(doc);

            using (var q = QueryBuilder.Select(SelectResult.All())
                       .From(DataSource.Collection(db.GetDefaultCollection()))) {
                q.Execute().Count().ShouldBe(1);
            }

            const string expectedHebrew = $"[{{\"hebrew\":\"{hebrew}\"}}]";
            var lines = customSink.Lines;
            lines.Any(x => x.Contains(expectedHebrew)).ShouldBeTrue();
        } finally {
            LogSinks.Custom = null;
            LogSinks.Console = initialConsole;
        }
    }

    private static void TestWithConfiguration(FileLogSink logSink, Action a)
    {
        var old = LogSinks.File;
        LogSinks.File = logSink;
        try {
            a();
        } finally {
            LogSinks.File = old;
        }
    }

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
            while (reader.ReadLine() is { } line) {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    private static byte[] ReadAllBytes(string path)
    {
        using var fin = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(fin);
        var retVal = new byte[fin.Length];
        _ = reader.Read(retVal, 0, retVal.Length);
        return retVal;
    }

    private class LogTestSink(ITestOutputHelper output, LogLevel level) : BaseLogSink(level)
    {
        private readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        protected override void WriteLog(LogLevel level, LogDomain domain, string message)
        {
            output.WriteLine(message);
            _lines.Add(message);
        }

        public void Reset() => _lines.Clear();
    }
}