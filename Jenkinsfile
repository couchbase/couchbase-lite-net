pipeline {
    agent any
    stages {
        stage("Prepare Test") {
            steps {
                script {
                    currentBuild.displayName = "${VERSION}"
                }
            }
        }
        stage("Test Desktop") {
            parallel {
                stage("Windows") {
                    agent { label 's61114win10_(litecore)' }
                    steps {
                        def result = powershell(returnStatus: true, script: 'jenkins\\run_win_tests.ps1')
                        if(result != 0) {
                            error("Win testing failed")
                        }
                    }
                    post {
                        always {
                            step([$class: 'MSTestPublisher', testResultsFile:"**/unit_tests.xml", failOnError: true, keepLongStdio: true])
                        }
                    }
                }
                stage("macOS") {
                    agent { label 'mobile-mac-mini' }
                    steps {
                        sh 'jenkins/run_mac_tests.sh'
                    }
                    post {
                        always {
                            step([$class: 'MSTestPublisher', testResultsFile:"**/unit_tests.xml", failOnError: true, keepLongStdio: true])
                        }
                    }
                }
                stage("Linux") {
                    agent { label 's61113u16 (litecore)' }
                    steps {
                        sh 'jenkins/run_linux_tests.sh'
                    }
                    post {
                        always {
                            step([$class: 'MSTestPublisher', testResultsFile:"**/unit_tests.xml", failOnError: true, keepLongStdio: true])
                        }
                    }
                }
            }
        }
        // stage("Test Mobile") {
        //     parallel {
        //         stage("UWP") {
        //             steps {
        //                 build job: "couchbase-lite-net-uwp",
        //                 parameters:[
        //                 string(name:"VERSION", value:"${VERSION}"), 
        //                 string(name:"BRANCH", value:"${BRANCH}")
        //                 ]
        //             }
        //         }
        //         stage("Xamarin Android") {
        //             steps {
        //                 build job: "couchbase-lite-net-xamarin-android",
        //                 parameters:[
        //                 string(name:"VERSION", value:"${VERSION}"), 
        //                 string(name:"BRANCH", value:"${BRANCH}")
        //                 ]
        //             }
        //         }
        //         stage("Xamarin iOS") {
        //             steps {
        //                 build job: "couchbase-lite-net-xamarin-ios",
        //                 parameters:[
        //                 string(name:"VERSION", value:"${VERSION}"), 
        //                 string(name:"BRANCH", value:"${BRANCH}")
        //                 ]
        //             }
        //         }
        //     }
        // }
        // stage("Promote Build") {
        //     steps {
        //         sh '''#!/bin/bash
        //          generate_post_data()
        //          {
        //             echo '{"packageName": "'$1'","version": "'$VERSION'","fromFeed": "CI","toFeed":"Internal","comments": "Promoted by Jenkins","API_Key": "xGBGDlaKWZergqWeYp1ybA=="}'
        //          }
                 
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.NetDesktop)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.UWP)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.Android)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.iOS)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.NetDesktop)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.UWP)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.Android)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
        //          curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.iOS)" "http://mobile.nuget.couchbase.com/api/promotions/promote"'''
        //     }   
        // }
        // stage("Build QE apps") {
        //     parallel {
        //         stage("non-iOS") {
        //              steps {
        //                 build job: "couchbase-lite-net-testapp",
        //                 parameters:[
        //                 string(name:"VERSION", value:"${VERSION}")
        //                 ],
        //                 wait: false
        //             }
        //         }
        //         stage("iOS") {
        //              steps {
        //                 build job: "couchbase-lite-net-testapp-ios",
        //                 parameters:[
        //                 string(name:"VERSION", value:"${VERSION}")
        //                 ],
        //                 wait: false
        //             }
        //         }
        //     }
        // }
    }
}