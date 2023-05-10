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
                                Get-ChildItem -Force tmp\\* | Move-Item -Destination couchbase-lite-net
                                Remove-Item tmp
                                Push-Location couchbase-lite-net
                                & 'C:\\Program Files\\Git\\bin\\git.exe' submodule update --init --recursive
                                Pop-Location
                                Push-Location jenkins
                                & 'C:\\Program Files\\Git\\bin\\git.exe' clone https://github.com/couchbaselabs/couchbase-lite-net-validation --depth --branch pre-3.2 proj
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
                                mv tmp/* couchbase-lite-net
                                rmdir tmp

                                pushd couchbase-lite-net
                                git submodule update --init --recursive
                                popd

                                pushd jenkins
                                git clone https://github.com/couchbaselabs/couchbase-lite-net-validation --depth 1 proj
                                popd
                                '''
                            }
                        }
                        stage("Maui iOS") {
                            steps {
                                sh 'jenkins/run_net6_ios_tests.sh'
                            }
                        }
                    }
                }
            }
        }
    }   
}