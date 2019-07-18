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
                        powershell(returnStatus: true, script: 'jenkins\\run_win_tests.ps1')
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
        stage("Test Mobile") {
            parallel {
                stage("UWP") {
                    agent { label 's61114win10_(litecore)' }
                    steps {
                        powershell(returnStatus: true, script: 'jenkins\\build_uwp_tests.ps1')
                        powershell(returnStatus: true, script: 'Remove-AppxPackage -Package "eb9a8775-4322-4e36-a34c-eee261241f4e_1.0.0.0_x64__1v3rwxh47wwxj"')
                        bat 'jenkins\\run_uwp_tests_debug.bat'
                        powershell(returnStatus: true, script: 'Remove-AppxPackage -Package "eb9a8775-4322-4e36-a34c-eee261241f4e_1.0.0.0_x64__1v3rwxh47wwxj"')
                        bat 'jenkins\\run_uwp_tests_release.bat'
                    }
                }
                stage("Xamarin Android") {
                    agent { label 'mobile-mac-mini' }
                    steps {
                        sh 'jenkins/run_android_tests.sh'
                    }
                }
                stage("Xamarin iOS") {
                    agent { label 'mobile-mac-mini' }
                    steps {
                        sh 'jenkins/run_ios_tests.sh'
                    }
                }
            }
        }
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
