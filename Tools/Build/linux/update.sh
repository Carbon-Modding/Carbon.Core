#!/usr/bin/env bash

###
### Copyright (c) 2022-2023 Carbon Community 
### All rights reserved
###
set -e

BASE="$(cd -- "$(dirname "$0")" >/dev/null 2>&1; pwd -P)"
ROOT="$(realpath "${BASE}/../../../")"

# Get the target depot argument
TARGET=${1:-release}

mono "${ROOT}/Tools/Helpers/CodeGen.exe" \
	--plugininput "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Common/src/Carbon/Core" \
	--pluginoutput "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Common/src/Carbon/Core/Core.Plugin-Generated.cs"

for MODULE in "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Common/src/Carbon/Modules/"*; do
	if [ -d "${MODULE}" ] 
	then
	mono "${ROOT}/Tools/Helpers/CodeGen.exe" \
		--plugininput "${MODULE}" \
		--pluginoutput "${MODULE}/$(basename "${MODULE}")-Generated.cs" \
		--pluginname "$(basename "${MODULE}")" \
		--pluginnamespace "Carbon.Modules" \
		--basename "module"
	fi
done

for MODULE in "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Modules/src/"*; do
	if [ -d "${MODULE}" ] 
	then
	mono "${ROOT}/Tools/Helpers/CodeGen.exe" \
		--plugininput "${MODULE}" \
		--pluginoutput "${MODULE}/$(basename "${MODULE}")-Generated.cs" \
		--pluginname "$(basename "${MODULE}")" \
		--pluginnamespace "Carbon.Modules" \
		--basename "module"
	fi
done

for OS in windows linux; do
    RUNTIME_URL=https://carbonmod.gg/assets/content/runtime-${OS}.zip
    RUNTIME_FOLDER=${ROOT}/Tools/Runtime/${OS}

    if [ -d "${RUNTIME_FOLDER}" ]; then
        echo Skipped ${OS} runtime..
    else
        echo Downloading ${OS} runtime from ${RUNTIME_URL}..
        mkdir -p "${RUNTIME_FOLDER}"
        curl --silent --fail --location --output /tmp/runtime_${OS}.zip ${RUNTIME_URL}
        unzip /tmp/runtime_${OS}.zip -d "${RUNTIME_FOLDER}"
        rm /tmp/runtime_${OS}.zip
    fi

	# Download rust binary libs
	"${ROOT}/Tools/DepotDownloader/DepotDownloader/bin/Release/net8.0/DepotDownloader" \
		-os ${OS} -validate -app 258550 -branch ${TARGET} -filelist \
		"${ROOT}/Tools/Helpers/258550_refs.txt" -dir "${ROOT}/Rust/${OS}"

	mono "${ROOT}/Tools/Helpers/Publicizer.exe" \
		-input "${ROOT}/Rust/${OS}/RustDedicated_Data/Managed" -carbon.rustrootdir "${ROOT}/Rust/${OS}"
done

dotnet restore "${ROOT}/Carbon.Core" --nologo
