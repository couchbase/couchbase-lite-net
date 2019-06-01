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
                    agent "s61114win10_(litecore)"
                    steps {
                        powershell(returnStatus: true, script: 'jenkins\\build_win_tests.ps1')
                    }
                }
                stage("macOS") {
                    steps {
                        build job: "couchbase-lite-net-netcore-macos",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}"), 
                        string(name:"BRANCH", value:"${BRANCH}")
                        ]
                    }
                }
                stage("Linux") {
                    steps {
                        build job: "couchbase-lite-net-netcore-linux",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}"), 
                        string(name:"BRANCH", value:"${BRANCH}")
                        ]
                    }
                }
            }
        }
        stage("Test Mobile") {
            parallel {
                stage("UWP") {
                    steps {
                        build job: "couchbase-lite-net-uwp",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}"), 
                        string(name:"BRANCH", value:"${BRANCH}")
                        ]
                    }
                }
                stage("Xamarin Android") {
                    steps {
                        build job: "couchbase-lite-net-xamarin-android",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}"), 
                        string(name:"BRANCH", value:"${BRANCH}")
                        ]
                    }
                }
                stage("Xamarin iOS") {
                    steps {
                        build job: "couchbase-lite-net-xamarin-ios",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}"), 
                        string(name:"BRANCH", value:"${BRANCH}")
                        ]
                    }
                }
            }
        }
        stage("Promote Build") {
            steps {
                sh '''#!/bin/bash
                 generate_post_data()
                 {
                    echo '{"packageName": "'$1'","version": "'$VERSION'","fromFeed": "CI","toFeed":"Internal","comments": "Promoted by Jenkins","API_Key": "xGBGDlaKWZergqWeYp1ybA=="}'
                 }
                 
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.NetDesktop)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.UWP)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.Android)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Support.iOS)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.NetDesktop)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.UWP)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.Android)" "http://mobile.nuget.couchbase.com/api/promotions/promote"
                 curl -X POST -H "Content-Type: application/json" -d "$(generate_post_data Couchbase.Lite.Enterprise.Support.iOS)" "http://mobile.nuget.couchbase.com/api/promotions/promote"'''
            }   
        }
        stage("Build QE apps") {
            parallel {
                stage("non-iOS") {
                     steps {
                        build job: "couchbase-lite-net-testapp",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}")
                        ],
                        wait: false
                    }
                }
                stage("iOS") {
                     steps {
                        build job: "couchbase-lite-net-testapp-ios",
                        parameters:[
                        string(name:"VERSION", value:"${VERSION}")
                        ],
                        wait: false
                    }
                }
            }
        }
    }
}