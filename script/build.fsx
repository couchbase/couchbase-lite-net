// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile

let buildMode = "Release"
let setParams defaults =
        { defaults with
            Verbosity = Some(Quiet)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", buildMode
                ]
         }
let net45Solution = "src"</>"Couchbase.Lite.Net45.sln"
let net45Project = "src"</>"Couchbase.Lite.Net45"</>"Couchbase.Lite.Net45.csproj"
let cbForestProject = "src"</>"Couchbase.Lite.Shared"</>"vendor"</>"cbforest"</>"CSharp"</>"cbforest-sharp.Net45"</>"cbforest-sharp.Net45.csproj"

Target "Build" (fun _ ->
    build setParams cbForestProject|> DoNothing    
    build setParams net45Project |> DoNothing
)


// start build
RunTargetOrDefault "Build"