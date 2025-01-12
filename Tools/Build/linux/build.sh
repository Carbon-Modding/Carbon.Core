#!/usr/bin/env bash

###
### Copyright (c) 2022-2023 Carbon Community 
### All rights reserved
###
set -e

echo "** Get the base path of the script"
BASE="$(cd -- "$(dirname "$0")" >/dev/null 2>&1; pwd -P)"
ROOT="$(realpath "${BASE}/../../../")"

"${BASE}/publish_git.sh" ${3}

TARGET=${1:-Debug}
echo "** Set the build target config to ${TARGET}"

echo "** Cleanup the release folder"
rm -rf "${ROOT}/Release/.tmp/${TARGET}" "${ROOT}/Release/Carbon.${TARGET}.tar.gz" || exit 0

if [[ "${DEFINES}" == "" ]]; then
	DEFINES=${2}
fi

if [[ "${DEFINES}" == "" ]]; then
	echo "** No defines."
else
	echo "** Defines: ${DEFINES}"
fi

if [[ "${TARGET}" == "Debug" || "${TARGET}" == "DebugUnix" || "${TARGET}" == "Minimal" || "${TARGET}" == "MinimalUnix" ]]; then
	CARGO_TARGET="release"
else
	CARGO_TARGET="prod"
fi

echo "** Build the solution"
dotnet restore "${ROOT}/Carbon.Core" -v:m --nologo
dotnet   clean "${ROOT}/Carbon.Core" -v:m --configuration ${TARGET} --nologo
dotnet   build "${ROOT}/Carbon.Core" -v:m --configuration ${TARGET} --no-restore --no-incremental \
	/p:UserConstants="${DEFINES}" /p:UserVersion="${VERSION}"

echo "** Copy operating system specific files"
if [[ "${TARGET}" == *"Unix"* ]]; then
	cp "${ROOT}/Tools/Helpers/Carbon.targets"                      												"${ROOT}/Release/.tmp/${TARGET}/Carbon.targets"
	cp "${ROOT}/Tools/Helpers/carbon.sh"                      													"${ROOT}/Release/.tmp/${TARGET}/carbon.sh"
	cp "${ROOT}/Tools/Helpers/environment.sh"                 													"${ROOT}/Release/.tmp/${TARGET}/carbon/tools"
	cp "${ROOT}/Tools/UnityDoorstop/linux/x64/libdoorstop.so" 													"${ROOT}/Release/.tmp/${TARGET}/libdoorstop.so"
	cp "${ROOT}/Carbon.Core/Carbon.Native/target/x86_64-unknown-linux-gnu/${CARGO_TARGET}/libCarbonNative.so" 	"${ROOT}/Release/.tmp/${TARGET}/carbon/native/libCarbonNative.so"
else
	cp "${ROOT}/Tools/Helpers/Carbon.targets"            														"${ROOT}/Release/.tmp/${TARGET}"
	cp "${ROOT}/Tools/Helpers/doorstop_config.ini"            													"${ROOT}/Release/.tmp/${TARGET}"
	cp "${ROOT}/Tools/UnityDoorstop/windows/x64/doorstop.dll" 													"${ROOT}/Release/.tmp/${TARGET}/winhttp.dll"
	cp "${ROOT}/Carbon.Core/Carbon.Native/target/x86_64-pc-windows-msvc/${CARGO_TARGET}/CarbonNative.dll" 		"${ROOT}/Release/.tmp/${TARGET}/carbon/native/CarbonNative.dll"
fi


TAG="$1"

if [[ "${TAG}" == "" ]]; then
	TAG="Debug"
fi

TAG="${TAG//Unix/}"

if [[ "${TARGET}" == *"Unix" ]]; then
	if [[ "${TARGET}" == "Debug"* ]]; then
		TOS=Linux
	else
		TOS=Linux
	fi
else
	if [[ "${TARGET}" == "Debug"* ]]; then
		TOS=Windows
	else
		TOS=Windows
	fi
fi

if [ "${2}" != "--no-archive" ]; then
	echo "** Create the compressed archive"
	tar -zcvf "${ROOT}/Release/Carbon.${TOS}.${TAG}.tar.gz" -C "${ROOT}/Release/.tmp/${TARGET}" $(ls -A ${ROOT}/Release/.tmp/${TARGET})
fi
