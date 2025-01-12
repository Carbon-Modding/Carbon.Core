::
:: Copyright (c) 2022-2023 Carbon Community 
:: All rights reserved
::
@echo off

pushd %~dp0..\..\..
set UPDATE_ROOT=%CD%
popd

rem Get the target depot argument
if "%1" EQU "" (
	set UPDATE_TARGET=release
) else (
	set UPDATE_TARGET=%1
)

"%UPDATE_ROOT%\Tools\Helpers\CodeGen.exe" ^
	--plugininput "%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Common\src\Carbon\Core" ^
	--pluginoutput "%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Common\src\Carbon\Core\Core.Plugin-Generated.cs"

for /d %%O in (%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Common\src\Carbon\Modules\*) do (
	"%UPDATE_ROOT%\Tools\Helpers\CodeGen.exe" ^
		--plugininput "%%O" ^
		--pluginoutput "%%O\%%~nO-Generated.cs" ^
		--pluginname "%%~nO" ^
		--pluginnamespace "Carbon.Modules" ^
		--basename "module"
)

for /d %%O in (%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Modules\src\*) do (
	"%UPDATE_ROOT%\Tools\Helpers\CodeGen.exe" ^
		--plugininput "%%O" ^
		--pluginoutput "%%O\%%~nO-Generated.cs" ^
		--pluginname "%%~nO" ^
		--pluginnamespace "Carbon.Modules" ^
		--basename "module"
)

set RUNTIME_URL=https://carbonmod.gg/assets/content/runtime-%%O.zip
set RUNTIME_FOLDER=%UPDATE_ROOT%\Tools\Runtime\%%O

FOR %%O IN (windows linux) DO (	
	if not exist %RUNTIME_FOLDER% (
		echo Downloading %%O runtime from %RUNTIME_URL%..
		mkdir %RUNTIME_FOLDER%
	
		call pwsh -Command "(New-Object Net.WebClient).DownloadFile('%RUNTIME_URL%', '%RUNTIME_FOLDER%\runtime_%%O.zip')"
		call pwsh -Command "Expand-Archive '%RUNTIME_FOLDER%\runtime_%%O.zip' -DestinationPath '%RUNTIME_FOLDER%'" -Force
		del "%RUNTIME_FOLDER%\runtime_%%O.zip"
	) else (
		echo Skipped %%O runtime..
	)
		
	echo Downloading %%O Rust files..

	rem Download rust binary libs
	"%UPDATE_ROOT%\Tools\DepotDownloader\DepotDownloader\bin\Release\net8.0\DepotDownloader.exe" ^
		-os %%O -validate -app 258550 -branch %UPDATE_TARGET% -filelist ^
		"%UPDATE_ROOT%\Tools\Helpers\258550_refs.txt" -dir "%UPDATE_ROOT%\Rust\%%O"

	"%UPDATE_ROOT%\Tools\Helpers\Publicizer.exe" ^
		-input "%UPDATE_ROOT%\Rust\%%O\RustDedicated_Data\Managed" -carbon.rustrootdir "%UPDATE_ROOT%\Rust\%%O" -carbon.logdir "%UPDATE_ROOT%\Rust\%%O"
)

dotnet restore "%UPDATE_ROOT%\Carbon.Core" --nologo
