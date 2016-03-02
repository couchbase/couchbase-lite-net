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
let androidSolution = "src"</>"Couchbase.Lite.Android.sln"
let net45Project = "src"</>"Couchbase.Lite.Net45"</>"Couchbase.Lite.Net45.csproj"
let androidProject = "src"</>"Couchbase.Lite.Android"</>"Couchbase.Lite.Android.csproj"
let cbForestProject = "src"</>"Couchbase.Lite.Shared"</>"vendor"</>"cbforest"</>"CSharp"</>"cbforest-sharp.Net45"</>"cbforest-sharp.Net45.csproj"

// nuget related paths
let nugetPath = Path.GetFullPath(@"./src/.nuget/Nuget.Exe")
let nuspecPath = Path.GetFullPath(@"./packaging/nuget/couchbase-lite-apx.nuspec")
let artifactsNuGetDir = Path.GetFullPath(@"./artifacts/nuget/")
let artifactsBuildDir = Path.GetFullPath(@"./artifacts/build/")

Target "Bootstrap"(fun _ ->
    CopyDir @"src\Couchbase.Lite.Shared\vendor\cbforest\CSharp\prebuilt" @"script\paket-files\github.com\" ( fun _-> true)

)

Target "Build" (fun _ ->
    let mutable restoreFailureHandled = false;
    
    // iterate through the solutions and restore their packages
    [
        net45Solution
        androidSolution
    ]
    |> List.iter(fun solution -> 
        try
            // restore packages for the current solution
            RestoreMSSolutionPackages(fun p ->
                {p with
                    OutputPath = "src/packages"
                })
                solution
        with
            | Failure msg -> 
                // there is a failure restoring packages for one of the samples
                // Handling it here allows us to proceed with the build
                log msg
                restoreFailureHandled <- true
        )
    
  
    // build the projects
    [
        cbForestProject
        net45Project
        androidProject 
    ] 
    |> List.iter(fun proj ->  build setParams proj |> DoNothing) 
    
    //// Workaround for https://github.com/fsharp/FAKE/issues/1079
    if restoreFailureHandled then traceEndTask "Build" "Error restoring packages was handled"
)

Target "Package" (fun _ ->
    ensureDirectory artifactsNuGetDir
    ensureDirectory artifactsBuildDir
    
    let nuspecProps = getNuspecProperties(File.ReadAllText(nuspecPath))
        
    let tcBuildValue = match TeamCityBuildNumber with
                       | Some(num) -> num
                       | None -> ""

    let version = sprintf "%s%s-apx" nuspecProps.Version tcBuildValue
    
    let basePath =  Path.GetFullPath "."
    // create and execute the command ourselves, since the NuGet helper doesn't allow us to set the BasePath command argument which we need
    let commandArgs = sprintf @"pack -Verbosity detailed -BasePath %s -Version %s -OutputDirectory %s %s" basePath version artifactsNuGetDir nuspecPath
    let result = Shell.Exec(nugetPath,  commandArgs.ToString())

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