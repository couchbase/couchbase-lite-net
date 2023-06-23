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
                                & 'C:\\Program Files\\Git\\bin\\git.exe' clone https://github.com/couchbaselabs/couchbase-lite-net-validation --depth 1 proj
                                Pop-Location
                                '''
                            }
                        }
                        stage(".NET 6 Windows") {
                            steps {
                                catchError {
                                    powershell 'jenkins\\run_net6_tests.ps1'
                                }
								
                                echo currentBuild.result
                            }
                        }
                        stage("WinUI") {
                            steps {
                                powershell 'jenkins\\run_winui_tests.ps1'
                            }
                        }
                    }
                }
	            stage("Mac Node") {
		            agent { label 'dotnet-mobile-mac-mini'  }
			        environment {
				        KEYCHAIN_PWD = credentials("mobile-mac-mini-keychain")
                    }
				    stages {
				        stage("Checkout") {
					        steps {
						        sh '''#!/bin/bash
                                set -e
                                shopt -s extglob dotglob
                                mkdir tmp
                                mv !(tmp) tmp
                                git clone git@github.com:couchbaselabs/couchbase-lite-net-ee --branch $BRANCH_NAME --depth 1 couchbase-lite-net-ee || \
                                    git clone git@github.com:couchbaselabs/couchbase-lite-net-ee --branch $CHANGE_TARGET --depth 1 couchbase-lite-net-ee
                                mv couchbase-lite-net-ee/* .
                                mkdir couchbase-lite-net
                                mv tmp/* couchbase-lite-net
                                rmdir tmp couchbase-lite-net-ee

                                # Make sure the latest tools are checked out
                                git submodule update --init

                                pushd jenkins
                                git clone https://github.com/couchbaselabs/couchbase-lite-net-validation --depth 1 proj
                                popd
                                '''
                            }
                        }
                        stage("Maui iOS") {
                            steps {
                                sh 'jenkins/run_net_ios_tests.sh'
                            }
                        }
                    }
                }
            }
        }
    }   
}