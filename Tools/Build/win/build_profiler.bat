@echo off

set OCD=%cd%

pushd %~dp0..\..\..
set BUILD_ROOT=%CD%

if "%1" EQU "" (
	set BUILD_TARGET=Release
) else (
	set BUILD_TARGET=%1
)

if "%DEFINES%" EQU "" (
	set DEFINES=%2
)

if "%DEFINES%" EQU "" (
	echo ** No defines.
) else (
	echo ** Defines: %DEFINES%
)

dotnet restore "%BUILD_ROOT%\Carbon.Core" -v:m --nologo || exit /b
dotnet   clean "%BUILD_ROOT%\Carbon.Core" -v:m --configuration %BUILD_TARGET% --nologo || exit /b
dotnet   build "%BUILD_ROOT%\Carbon.Core" -v:m --configuration %BUILD_TARGET% --no-restore --no-incremental ^
	/p:UserConstants=\"%DEFINES%\" /p:UserVersion="%VERSION%" || exit /b

set CARGO_TARGET=release

echo ** Copy operating system specific files
echo "%BUILD_TARGET%" | findstr /C:"Unix" >NUL && (
	copy /y "%BUILD_ROOT%\Carbon.Core\Carbon.Native\target\x86_64-unknown-linux-gnu\%CARGO_TARGET%\libCarbonNative.so"	"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\profiler\native\libCarbonNative.so"
	(CALL )				
) || (                                                                  				                                                        
	copy /y "%BUILD_ROOT%\Carbon.Core\Carbon.Native\target\x86_64-pc-windows-msvc\%CARGO_TARGET%\CarbonNative.dll"		"%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\profiler\native\CarbonNative.dll"
	(CALL )
)

echo ** Create the compressed archive 'Carbon.Windows.Profiler.zip'
pwsh -Command "Compress-Archive -Update -Path '%BUILD_ROOT%\Release\.tmp\%BUILD_TARGET%\profiler\*' -DestinationPath '%BUILD_ROOT%\Release\Carbon.Windows.Profiler.zip'"

cd %OCD%