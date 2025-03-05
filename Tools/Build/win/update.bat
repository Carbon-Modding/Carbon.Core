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

FOR %%O IN (windows linux) DO (			
	echo Downloading %%O Rust files..

	rem Download rust binary libs
	"%UPDATE_ROOT%\Tools\DepotDownloader\DepotDownloader\bin\Release\net8.0\DepotDownloader.exe" ^
		-os %%O -validate -app 258550 -branch %UPDATE_TARGET% -filelist ^
		"%UPDATE_ROOT%\Tools\Helpers\258550_refs.txt" -dir "%UPDATE_ROOT%\Rust\%%O"
)

dotnet restore "%UPDATE_ROOT%\Carbon.Core"
dotnet   clean "%UPDATE_ROOT%\Carbon.Core" --configuration Debug
dotnet   build "%UPDATE_ROOT%\Carbon.Core" --configuration Debug

FOR %%O IN (windows linux) DO (			
	"%UPDATE_ROOT%\Carbon.Core\Carbon.Tools\Carbon.Publicizer\bin\x64\Debug\net8.0\Carbon.Publicizer.exe" ^
		-input "%UPDATE_ROOT%\Rust\%%O\RustDedicated_Data\Managed" -carbon.rustrootdir "%UPDATE_ROOT%\Rust\%%O" -carbon.logdir "%UPDATE_ROOT%\Rust\%%O"
)

dotnet restore "%UPDATE_ROOT%\Carbon.Core"
dotnet   clean "%UPDATE_ROOT%\Carbon.Core" --configuration Debug
dotnet   build "%UPDATE_ROOT%\Carbon.Core" --configuration Debug

"%UPDATE_ROOT%\Carbon.Core\Carbon.Tools\Carbon.Generator\bin\x64\Debug\net8.0\Carbon.Generator.exe" ^
	--plugininput "%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Common\src\Carbon\Core" ^
	--pluginoutput "%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Common\src\Carbon\Core\Core.Plugin-Generated.cs"

for /d %%O in (%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Common\src\Carbon\Modules\*) do (
	"%UPDATE_ROOT%\Carbon.Core\Carbon.Tools\Carbon.Generator\bin\x64\Debug\net8.0\Carbon.Generator.exe" ^
		--plugininput "%%O" ^
		--pluginoutput "%%O\%%~nO-Generated.cs" ^
		--pluginname "%%~nO" ^
		--pluginnamespace "Carbon.Modules" ^
		--basename "module"
)

for /d %%O in (%UPDATE_ROOT%\Carbon.Core\Carbon.Components\Carbon.Modules\src\*) do (
	"%UPDATE_ROOT%\Carbon.Core\Carbon.Tools\Carbon.Generator\bin\x64\Debug\net8.0\Carbon.Generator.exe" ^
		--plugininput "%%O" ^
		--pluginoutput "%%O\%%~nO-Generated.cs" ^
		--pluginname "%%~nO" ^
		--pluginnamespace "Carbon.Modules" ^
		--basename "module"
)