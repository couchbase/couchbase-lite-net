function Write-Implementation($collection, $className, [ref]$result) {
    foreach($entry in $collection) {
        if(-Not $entry.Contains("(")) {
            continue
        }

        $method = $entry.Split("(")[0].Split(" ")[-1]
        $nextLine = "${className}.${method}("
        $argObjs = $entry.Split("(")[-1].Split(",")
        $argNames = $argObjs | ForEach-Object { $_.Split(" ")[-1].TrimEnd(");").TrimStart("*") }
        $nextLine += [string]::Join(", ", $argNames)
        $nextLine += ");"

        $result.Value += "        public $($entry.Substring(0, $entry.Length - 1).TrimStart()) => $nextLine"
    }
}

Push-Location $PSScriptRoot
$native = @()
$native_raw = @()
Get-ChildItem $PWD -Filter *_native.cs |
ForEach-Object {
    $InRaw = $false
    foreach($line in Get-Content $_) {
        if($line.Contains("class NativeRaw")) {
            $InRaw = $true
        }

        if($line.Contains("public static")) {
            $convertedLine = $line.Replace("public static ", "").Replace("extern ", "").Replace("[MarshalAs(UnmanagedType.U1)]", "").Replace("[Out]", "")
            if(-Not $convertedLine.EndsWith(";")) {
                $convertedLine += ";"
            }

            if($InRaw) {
                $native_raw += $convertedLine
            } else {
                $native += $convertedLine
            }
        }
    }
}

$native_file = @("using System;`n", "using Couchbase.Lite.Interop;`n", "namespace LiteCore.Interop", "{", "    internal unsafe interface ILiteCore", "    {")
$native_file += $native
$native_file += "    }`n"
$native_file += @("    internal unsafe interface ILiteCoreRaw", "    {")
$native_file += $native_raw
$native_file += "    }"
$native_file += "}"

[string]::Join("`n", $native_file) | Out-File ILiteCore.cs

$implementation = @("using System;`n", "namespace LiteCore.Interop", "{", "    internal sealed unsafe class LiteCoreImpl : ILiteCore", "    {")
Write-Implementation $native "Native" ([ref]$implementation)
$implementation += @("    }`n", "    internal sealed unsafe class LiteCoreRawImpl : ILiteCoreRaw", "    {")
Write-Implementation $native_raw "NativeRaw" ([ref]$implementation)
$implementation += "    }"
$implementation += "}"

[string]::Join("`n", $implementation) | Out-File LiteCore_impl.cs

$shell = @()
$shell_raw = @()
foreach($entry in $implementation) {
    if(-Not $entry.StartsWith("        ")) {
        continue
    }

    if($entry.Contains("NativeRaw")) {
        $shell_raw += $entry.Replace("NativeRaw", "Impl").Replace("public ", "public static ")
    } else {
        $shell += $entry.Replace("Native", "Impl").Replace("public ", "public static ")
    }
}

$shell_file = @("using System;`n", "using LiteCore.Interop;`n", "namespace Couchbase.Lite.Interop", "{", "    internal static unsafe partial class Native", "    {")
$shell_file += $shell
$shell_file += "    }`n"
$shell_file += "    internal static unsafe partial class NativeRaw", "    {"
$shell_file += $shell_raw
$shell_file += "    }"
$shell_file += "}"

[string]::Join("`n", $shell_file) | Out-File LiteCore_shell.cs