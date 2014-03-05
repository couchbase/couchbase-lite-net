#/bin/sh
Tools/sync_gateway -pretty -verbose=true Couchbase.Lite/Couchbase.Lite.Tests/Assets/GatewayConfig.json &
sleep 3
curl -X PUT 127.0.0.1:4985/db/_user/GUEST --data '{"disabled":false, "admin_channels":["public"]}'
