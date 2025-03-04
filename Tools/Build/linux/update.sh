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

for OS in windows linux; do
	# Download rust binary libs
	"${ROOT}/Tools/DepotDownloader/DepotDownloader/bin/Release/net8.0/DepotDownloader" \
		-os ${OS} -validate -app 258550 -branch ${TARGET} -filelist \
		"${ROOT}/Tools/Helpers/258550_refs.txt" -dir "${ROOT}/Rust/${OS}"
done

dotnet restore "${ROOT}/Carbon.Core"
dotnet clean   "${ROOT}/Carbon.Core" --configuration Debug
dotnet build   "${ROOT}/Carbon.Core" --configuration Debug

for OS in windows linux; do
	"${ROOT}/Carbon.Core/Carbon.Tools/Carbon.Publicizer/bin/Debug/net8.0/Carbon.Publicizer.exe" \
		-input "${ROOT}/Rust/${OS}/RustDedicated_Data/Managed" -carbon.rustrootdir "${ROOT}/Rust/${OS}" -carbon.logdir "${ROOT}/Rust/${OS}"
done

dotnet restore "${ROOT}/Carbon.Core"
dotnet clean   "${ROOT}/Carbon.Core" --configuration Debug
dotnet build   "${ROOT}/Carbon.Core" --configuration Debug

"${ROOT}/Carbon.Core/Carbon.Tools/Carbon.Generator/bin/Debug/Carbon.Generator.exe" \
	--plugininput "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Common/src/Carbon/Core" \
	--pluginoutput "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Common/src/Carbon/Core/Core.Plugin-Generated.cs"

for MODULE in "${ROOT}/Carbon.Core/Carbon.Components/Carbon.Common/src/Carbon/Modules/"*; do
	if [ -d "${MODULE}" ] 
	then
	"${ROOT}/Carbon.Core/Carbon.Tools/Carbon.Generator/bin/Debug/Carbon.Generator.exe" \
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
	"${ROOT}/Carbon.Core/Carbon.Tools/Carbon.Generator/bin/Debug/Carbon.Generator.exe" \
		--plugininput "${MODULE}" \
		--pluginoutput "${MODULE}/$(basename "${MODULE}")-Generated.cs" \
		--pluginname "$(basename "${MODULE}")" \
		--pluginnamespace "Carbon.Modules" \
		--basename "module"
	fi
done

dotnet restore "${ROOT}/Carbon.Core" --nologo
