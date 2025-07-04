pipeline {
    agent none
    options {
        timeout(time: 60, unit: 'MINUTES') 
    }
    stages {
	    stage("Entry") {
            parallel {
                stage("Windows Node") {
                    agent { label 'couchbase-lite-net-validation' }
                    stages {
                        stage("Checkout") {
                            steps {
                                powershell '''
                                New-Item -Type Directory tmp
                                Get-ChildItem -Path $pwd -Exclude "tmp" -Force | Move-Item -Destination "tmp"
                                # Sometimes a PR depends on a PR in the EE repo as well.  This needs to be convention based, so if there is a branch with the name PR-###
                                # (with the GH PR number) in the EE repo then use that, otherwise use the name of the target branch (master, release/XXX etc)  
                                & 'C:\\Program Files\\Git\\bin\\git.exe' clone git@github.com:couchbaselabs/couchbase-lite-net-ee --branch $env:BRANCH_NAME --depth 1 couchbase-lite-net-ee
                                if($LASTEXITCODE -ne 0) {
                                    & 'C:\\Program Files\\Git\\bin\\git.exe' clone git@github.com:couchbaselabs/couchbase-lite-net-ee --branch $env:CHANGE_TARGET --depth 1 couchbase-lite-net-ee
                                }
								
                                Get-ChildItem couchbase-lite-net-ee\\* -Force | Move-Item -Destination .
                                New-Item -Type Directory couchbase-lite-net
                                Get-ChildItem -Force tmp\\* | Move-Item -Destination couchbase-lite-net
                                Remove-Item tmp
                                Remove-Item couchbase-lite-net-ee

                                # Make sure the latest tools are checked out
                                & 'C:\\Program Files\\Git\\bin\\git.exe' submodule update --init

                                Push-Location jenkins
                                & 'C:\\Program Files\\Git\\bin\\git.exe' clone https://github.com/couchbaselabs/couchbase-lite-net-validation --depth 1 --branch pre-4.0 proj
                                Pop-Location
                                '''
                            }
                        }
                        stage("Compile CE") {
                            steps {
                                powershell 'jenkins\\compile_ce.ps1'
                            }
                        }
                        stage("Compile EE") {
                            steps {
                                powershell 'jenkins\\compile_ee.ps1'
                            }
                        }
                        stage(".NET Windows") {
                            steps {
                                powershell 'jenkins\\run_net_console_tests.ps1'
                            }
                        }
                    }
                }
            }
        }
    }   
}