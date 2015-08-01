
#!/bin/bash

function usage 
{
	echo -e "\nUse: ${0} start|stop|clean\n"
}

PROJECT_DIR="$(pwd)"
SG_DIR="${PROJECT_DIR}/tmp"
SG_URL="http://packages.couchbase.com/releases/couchbase-sync-gateway/1.1.0/couchbase-sync-gateway-enterprise_1.1.0-28_x86_64.tar.gz"
SG_PKG="${SG_DIR}/sync_gateway.tar.gz"
SG_TAR="${SG_DIR}/couchbase-sync-gateway"
SG_BIN="${SG_TAR}/bin/sync_gateway"
SG_PID="${SG_DIR}/pid"
SG_CFG="${PROJECT_DIR}/script/sync-gateway-config.json"

function startSyncGateway
{
	if  [[ ! -e ${SG_BIN} ]] 
		then
		cleanSyncGateway
		mkdir "${SG_DIR}"
		echo "Downloading SyncGateway ..."
		curl -s -o "${SG_PKG}" ${SG_URL}
		tar xf "${SG_PKG}" -C "${SG_DIR}"
		rm -f "${SG_PKG}"
	fi

	stopSyncGateway

	open "http://localhost:4985/_admin/"
	
	"${SG_BIN}" "${SG_CFG}"
	PID=$!
	echo ${PID} > "${SG_PID}"
}

function stopSyncGateway
{
	if  [[ -e "${SG_PID}" ]]
		then
		kill $(cat "${SG_PID}") 2>/dev/null
		rm -f "${SG_PID}"
	fi
}

function cleanSyncGateway
{
	stopSyncGateway
	rm -rf "${SG_DIR}"
}

MODE=${1}
if [[ ${MODE} = "start" ]]
	then 
	echo "Start SyncGateway ..."
	startSyncGateway
elif [[ ${MODE} = "stop" ]]
	then 
	echo "Stop SyncGateway ..."
	stopSyncGateway
elif [[ ${MODE} = "clean" ]]
	then 
	echo "Clean SyncGateway ..."
	cleanSyncGateway
else
	usage
fi
