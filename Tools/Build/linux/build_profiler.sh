#!/bin/bash

OCD=$(pwd)

cd "$(dirname "$0")/../../.."
BUILD_ROOT=$(pwd)

if [ -z "$1" ]; then
    BUILD_TARGET="Release"
else
    BUILD_TARGET="$1"
fi

if [ -z "$DEFINES" ]; then
    DEFINES="$2"
fi

if [ -z "$DEFINES" ]; then
    echo "** No defines."
else
    echo "** Defines: $DEFINES"
fi

dotnet restore "$BUILD_ROOT/Carbon.Core" -v:m --nologo || exit 1
dotnet clean "$BUILD_ROOT/Carbon.Core" -v:m --configuration "$BUILD_TARGET" --nologo || exit 1
dotnet build "$BUILD_ROOT/Carbon.Core" -v:m --configuration "$BUILD_TARGET" --no-restore --no-incremental \
    -p:UserConstants="$DEFINES" -p:UserVersion="$VERSION" || exit 1

CARGO_TARGET=release

echo "** Copy operating system specific files"
if [[ "$BUILD_TARGET" == *"Unix"* ]]; then
    cp -f "$BUILD_ROOT/Carbon.Core/Carbon.Native/target/x86_64-unknown-linux-gnu/$CARGO_TARGET/libCarbonNative.so" \
        "$BUILD_ROOT/Release/.tmp/$BUILD_TARGET/profiler/native/libCarbonNative.so"
else
    cp -f "$BUILD_ROOT/Carbon.Core/Carbon.Native/target/x86_64-pc-windows-msvc/$CARGO_TARGET/CarbonNative.dll" \
        "$BUILD_ROOT/Release/.tmp/$BUILD_TARGET/profiler/native/CarbonNative.dll"
fi

echo "** Create the compressed archive 'Carbon.Linux.Profiler.$EXT'"
pwsh -Command "Compress-Archive -Update -Path '$BUILD_ROOT/Release/.tmp/$BUILD_TARGET/profiler/*' -DestinationPath '$BUILD_ROOT/Release/Carbon.Linux.Profiler.tar.gz'"
