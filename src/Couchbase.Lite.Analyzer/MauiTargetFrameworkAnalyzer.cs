// 
//  MauiTargetFrameworkAnalyzer.cs
// 
//  Copyright (c) 2025 Couchbase. All rights reserved.
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
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Couchbase.Lite.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MauiTargetFrameworkAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CBL0001";
    private const int MauiTarget = 10;

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Unsupported MAUI target framework",
        messageFormat: $"Couchbase Lite requires .NET {MauiTarget} or later for MAUI targets. '{{1}}' is not supported; use net{MauiTarget}.0-{{2}} or later.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: $"MAUI targets (Android, iOS, Mac Catalyst, Windows) require net{MauiTarget} or later.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly Regex TfmPattern = new(
        @"^net(\d+)\.0-(android|ios|maccatalyst|windows)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (!options.TryGetValue("build_property.TargetFramework", out var tf))
            return;

        var match = TfmPattern.Match(tf);
        if (!match.Success)
            return;

        if (!Int32.TryParse(match.Groups[1].Value, out var version) || version >= MauiTarget)
            return;

        var platform = match.Groups[2].Value.ToLowerInvariant();
        context.ReportDiagnostic(Diagnostic.Create(Rule, Location.None, tf, platform));
    }
}
