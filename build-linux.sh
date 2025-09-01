#!/bin/bash

# DS Pokemon ROM Editor - Linux Build Script
# This script builds the Linux-compatible BatchExport tool

set -e

echo "DS Pokemon ROM Editor - Linux Build Script"
echo "=========================================="

# Check for .NET
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET runtime not found."
    echo "Please install .NET 8.0:"
    echo "  sudo apt update && sudo apt install dotnet-runtime-8.0"
    exit 1
fi

echo "✓ .NET runtime found: $(dotnet --version)"

# Check for ndstool
if ! command -v ndstool &> /dev/null; then
    echo "Warning: ndstool not found in PATH."
    echo "To extract ROM files, install ndstool:"
    echo "  sudo apt install devkitpro-tools"
    echo "  # or build from source: https://github.com/devkitPro/ndstool"
    echo ""
fi

# Build the Linux version
echo "Building BatchExport.Linux..."
cd BatchExport.Linux
dotnet build --configuration Release

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Build successful!"
    echo ""
    echo "Usage:"
    echo "  cd BatchExport.Linux"
    echo "  dotnet run -- <rom.nds> <output_dir> [--extract-only]"
    echo ""
    echo "Or run the compiled binary:"
    echo "  dotnet bin/Release/net8.0/DSPRE.BatchExport.Linux.dll <rom.nds> <output_dir>"
else
    echo "✗ Build failed!"
    exit 1
fi