@echo off

echo   ______ _______ ______ ______ _______ _______ 
echo  ^|      ^|   _   ^|   __ \   __ \       ^|    ^|  ^|
echo  ^|   ---^|       ^|      ^<   __ ^<   -   ^|       ^|
echo  ^|______^|___^|___^|___^|__^|______/_______^|__^|____^|
echo                         discord.gg/eXPcNKK4yd
echo.

set BASE=%~dp0

pushd %BASE%..\..\..
set ROOT=%CD%
popd

rem Get the target depot argument
if "%1" EQU "" (
	set TARGET=public
) else (
	set TARGET=%1
)

rem Cleans the exiting files
git clean -fx "%ROOT%\Rust"

FOR %%O IN (windows linux) DO (
	rem Download rust binary libs
	"%ROOT%\Tools\DepotDownloader\DepotDownloader\bin\Release\net6.0\DepotDownloader.exe" ^
		-os %%O -validate -app 258550 -branch %TARGET% -filelist ^
		"%ROOT%\Tools\Helpers\258550_258551_refs.txt" -dir "%ROOT%\Rust\%%O"

	rem Show me all you've got baby
	"%ROOT%\Tools\NStrip\NStrip\bin\Release\net452\NStrip.exe" ^
		--public --include-compiler-generated --keep-resources --no-strip --overwrite ^
		--unity-non-serialized "%ROOT%\Rust\%%O\RustDedicated_Data\Managed\Assembly-CSharp.dll"
)

dotnet restore "%ROOT%\Carbon.Core" --nologo