#!/bin/bash
# Copies .NET reference DLLs to wwwroot/lib/ for Roslyn in-browser compilation
set -e

RUNTIME_DIR=$(dotnet --list-runtimes | grep "Microsoft.NETCore.App 8" | head -1 | sed 's/.*\[\(.*\)\]/\1/')
VERSION=$(dotnet --list-runtimes | grep "Microsoft.NETCore.App 8" | head -1 | awk '{print $2}')
RUNTIME_PATH="$RUNTIME_DIR/$VERSION"

DEST="$(dirname "$0")/wwwroot/lib"
mkdir -p "$DEST"

DLLS=(
    "mscorlib.dll"
    "System.dll"
    "System.Console.dll"
    "System.Core.dll"
    "System.Private.CoreLib.dll"
    "System.Runtime.dll"
    "System.Collections.dll"
    "netstandard.dll"
)

echo "Copying from: $RUNTIME_PATH"
for dll in "${DLLS[@]}"; do
    if [ -f "$RUNTIME_PATH/$dll" ]; then
        cp "$RUNTIME_PATH/$dll" "$DEST/$dll"
        echo "  Copied: $dll"
    else
        echo "  WARNING: $dll not found"
    fi
done

echo "Done. DLLs in $DEST:"
ls -la "$DEST"
