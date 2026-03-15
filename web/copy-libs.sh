#!/bin/bash
# Copies .NET reference assemblies to wwwroot/lib/ for Roslyn in-browser compilation.
# Uses the ref (targeting) pack instead of runtime DLLs so compiled code resolves
# methods correctly in both desktop and WASM runtimes.
set -e

DOTNET_ROOT="$(dotnet --list-sdks | head -1 | sed 's/.*\[\(.*\)\]/\1/')/.."
VERSION=$(ls "$DOTNET_ROOT/packs/Microsoft.NETCore.App.Ref/" | sort -V | tail -1)
REF_PATH="$DOTNET_ROOT/packs/Microsoft.NETCore.App.Ref/$VERSION/ref/net8.0"

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

echo "Copying from: $REF_PATH"
for dll in "${DLLS[@]}"; do
    if [ -f "$REF_PATH/$dll" ]; then
        cp "$REF_PATH/$dll" "$DEST/$dll"
        echo "  Copied: $dll"
    else
        echo "  WARNING: $dll not found"
    fi
done

# Remove old runtime-specific DLL that isn't in the ref pack
rm -f "$DEST/System.Private.CoreLib.dll"

echo "Done. DLLs in $DEST:"
ls -la "$DEST"
