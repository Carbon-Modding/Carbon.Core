::
:: Copyright (c) 2022-2023 Carbon Community 
:: All rights reserved
::
@echo off

echo ** Get the base path of the script
pushd %~dp0..\..\..
set BUILD_ROOT=%CD%
popd

if "%1" EQU "" (
	set BUILD_TARGET=Debug
) else (
	set BUILD_TARGET=%1
)

call "%~dp0publish_git.bat" %3

echo ** Set the build target config to %BUILD_TARGET%

echo ** Cleanup the release folder
rmdir /s /q "%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%" 2>NUL
del /q "%BUILD_ROOT%\Release\Carbon.%BUILD_TARGET%.zip" 2>NUL

if "%DEFINES%" EQU "" (
	set DEFINES=%2
)

if "%DEFINES%" EQU "" (
	echo ** No defines.
) else (
	echo ** Defines: %DEFINES%
)

if "%BUILD_TARGET%" EQU "Debug" (
	set CARGO_TARGET=release
) else if "%BUILD_TARGET%" EQU "DebugUnix" (
	set CARGO_TARGET=release
) else if "%BUILD_TARGET%" EQU "Minimal" (
	set CARGO_TARGET=release
) else if "%BUILD_TARGET%" EQU "MinimalUnix" (
	set CARGO_TARGET=release
) else (
	set CARGO_TARGET=prod
)

echo ** Build the solution
dotnet restore "%BUILD_ROOT%\Carbon.Core" -v:m --nologo || exit /b
dotnet   clean "%BUILD_ROOT%\Carbon.Core" -v:m --configuration %BUILD_TARGET% --nologo || exit /b
dotnet   build "%BUILD_ROOT%\Carbon.Core" -v:m --configuration %BUILD_TARGET% --no-restore --no-incremental ^
	/p:UserConstants=\"%2\" /p:UserVersion="%VERSION%" || exit /b

echo ** Copy operating system specific files
echo "%BUILD_TARGET%" | findstr /C:"Unix" >NUL && (
	copy /y "%BUILD_ROOT%\Tools\Helpers\Carbon.targets"            														"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%"                                
	copy /y "%BUILD_ROOT%\Tools\Helpers\carbon.sh"                      												"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\carbon.sh"
	copy /y "%BUILD_ROOT%\Tools\Helpers\environment.sh"                 												"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\carbon\tools\"
	copy /y "%BUILD_ROOT%\Tools\UnityDoorstop\linux\x64\libdoorstop.so" 												"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\libdoorstop.so"
	copy /y "%BUILD_ROOT%\Carbon.Core\Carbon.Native\target\x86_64-unknown-linux-gnu\%CARGO_TARGET%\libCarbonNative.so"	"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\carbon\native\libCarbonNative.so"
	(CALL )				
) || (                                                                  				                                                        
	copy /y "%BUILD_ROOT%\Tools\Helpers\Carbon.targets"            														"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%"                                
	copy /y "%BUILD_ROOT%\Tools\Helpers\doorstop_config.ini"            												"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%"                                
	copy /y "%BUILD_ROOT%\Tools\UnityDoorstop\windows\x64\doorstop.dll" 												"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\winhttp.dll"    
	copy /y "%BUILD_ROOT%\Carbon.Core\Carbon.Native\target\x86_64-pc-windows-msvc\%CARGO_TARGET%\CarbonNative.dll"		"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\carbon\native\CarbonNative.dll"
	(CALL )
)


set TAG=%1

if "%TAG%" EQU "" (
	set TAG=Debug
)

set TAG=%TAG:Unix=%

echo "%BUILD_TARGET%" | findstr /C:"Unix" >NUL && (
	echo "%BUILD_TARGET%" | findstr /C:"Debug" >NUL && (
		set TOS=Linux
		(CALL )
	) || (                                                                                                                          
		set TOS=Linux
		(CALL )
	)
	(CALL )
) || (                                                                                                                          
	echo "%BUILD_TARGET%" | findstr /C:"Debug" >NUL && (
		set TOS=Windows
		(CALL )
	) || (                                                                                                                          
		set TOS=Windows
		(CALL )
	)
	(CALL )
)

if "%2" NEQ "--no-archive" (
	echo ** Create the compressed archive 'Carbon.%TOS%.%TAG%.zip'
	pwsh -Command "Compress-Archive -Update -Path '%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\*' -DestinationPath '%BUILD_ROOT%\Release\Carbon.%TOS%.%TAG%.zip'"
)
