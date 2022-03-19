pipeline {
    agent any
    options {
        timeout(time: 30, unit: 'MINUTES') 
    }
    stages {
	    stage("Mac Node") {
		    agent { label 'dotnet-mobile-mac-mini'  }
			    environment {
				    NEXUS_REPO="http://nexus.build.couchbase.com:8081/nexus/content/repositories/releases/com/couchbase/litecore"
                    KEYCHAIN_PWD = credentials("mackeychain")
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
                            git submodule update --init
                            popd

                            pushd jenkins
                            git clone https://github.com/couchbaselabs/couchbase-lite-net-validation --depth 1 proj
                            popd
                            '''
                        }
                    }
                    stage(".NET Core Mac") {
                        steps {
                            catchError {
                                sh 'jenkins/run_unix_tests.sh'
                            }
								
                            echo currentBuild.result
                        }
                    }
                    stage("Xamarin iOS") {
                        steps {
                            catchError {
                                sh 'jenkins/run_ios_tests.sh'
                            }
								
                            echo currentBuild.result
                        }
                    }
                    stage("Xamarin Android") {
                        steps {
                            catchError {
                                sh 'jenkins/run_android_tests.sh'
                            }
								
                            echo currentBuild.result
                        }
                    }
                }
        }
    }   
}
