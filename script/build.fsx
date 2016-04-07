// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open System.IO
open System

let buildMode = "Release"
let setParams defaults =
        { defaults with
            Verbosity = Some(Quiet)
            Targets = [
                        "Clean"
                        "Rebuild"
                      ]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", buildMode
                ]
         }

// solution and project paths
let net45Solution = "src"</>"Couchbase.Lite.Net45.sln"
let net45Project = "src"</>"Couchbase.Lite.Net45"</>"Couchbase.Lite.Net45.csproj"
let androidProject = "src"</>"Couchbase.Lite.Android"</>"Couchbase.Lite.Android.csproj"
let cbForestProject = "src"</>"Couchbase.Lite.Shared"</>"vendor"</>"cbforest"</>"CSharp"</>"cbforest-sharp.Net45"</>"cbforest-sharp.Net45.csproj"

// nuget related paths
let nugetPath = Path.GetFullPath(@"./src/.nuget/Nuget.Exe")
let nuspecPath = Path.GetFullPath(@"./packaging/nuget/couchbase-lite-apx.nuspec")
let artifactsNuGetDir = Path.GetFullPath(@"./artifacts/nuget/")
let artifactsBuildDir = Path.GetFullPath(@"./artifacts/build/")

Target "Build" (fun _ ->
    [
        cbForestProject
        net45Project
        androidProject 
    ] 
    |> List.iter(fun proj ->  build setParams proj |> DoNothing) 

)

Target "Package" (fun _ ->
    ensureDirectory artifactsNuGetDir
    ensureDirectory artifactsBuildDir
    
    // create and execute the command ourselves, since the NuGet helper doesn't allow us to set the BasePath command argument which we need
    let commandArgs = sprintf @"pack -BasePath . -Verbosity detailed -OutputDirectory %s %s" artifactsNuGetDir nuspecPath
    let result = Shell.Exec(nugetPath,  commandArgs)
    
    if result <> 0 then failwithf "%s exited with error %d" "build.bat" result
)

Target "TeamCity"(fun _ ->
    directoryInfo artifactsNuGetDir 
        |> filesInDir 
        |> Array.iter(fun file -> PublishArtifact file.FullName)
)

// Dependencies
"Build"
     ==> "Package"
     ==> "TeamCity"

// start build
RunTargetOrDefault "Package"