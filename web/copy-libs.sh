#!/bin/bash
# Copies .NET WASM runtime DLLs to wwwroot/lib/ for Roslyn in-browser compilation.
# Uses the browser-wasm Mono runtime DLLs from the NuGet cache so compiled code
# resolves methods against the same assemblies that run in the browser.
set -e

WASM_PKG="$HOME/.nuget/packages/microsoft.netcore.app.runtime.mono.browser-wasm"
VERSION=$(ls "$WASM_PKG" | sort -V | tail -1)
WASM_PATH="$WASM_PKG/$VERSION/runtimes/browser-wasm/lib/net8.0"

DEST="$(dirname "$0")/wwwroot/lib"
mkdir -p "$DEST"

DLLS=(
    "mscorlib.dll"
    "System.dll"
    "System.Console.dll"
    "System.Core.dll"
    "System.Runtime.dll"
    "System.Collections.dll"
    "netstandard.dll"
)

echo "Copying from: $WASM_PATH"
for dll in "${DLLS[@]}"; do
    if [ -f "$WASM_PATH/$dll" ]; then
        cp "$WASM_PATH/$dll" "$DEST/$dll"
        echo "  Copied: $dll"
    else
        echo "  WARNING: $dll not found"
    fi
done

# System.Private.CoreLib lives in the native/ folder for Mono WASM
NATIVE_PATH="$WASM_PKG/$VERSION/runtimes/browser-wasm/native"
if [ -f "$NATIVE_PATH/System.Private.CoreLib.dll" ]; then
    cp "$NATIVE_PATH/System.Private.CoreLib.dll" "$DEST/System.Private.CoreLib.dll"
    echo "  Copied: System.Private.CoreLib.dll (from native/)"
fi

echo "Done. DLLs in $DEST:"
ls -la "$DEST"
