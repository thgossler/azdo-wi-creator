#!/bin/bash

# Script to publish azdo-wi-creator for all platforms as single-file executables
# Output will be in the publish/ directory

set -e  # Exit on error

PROJECT_PATH="azdo-wi-creator/azdo-wi-creator.csproj"
OUTPUT_BASE="publish"

echo "=========================================="
echo "Publishing azdo-wi-creator for all platforms"
echo "=========================================="

# Clean previous publish directory
if [ -d "$OUTPUT_BASE" ]; then
    echo "Cleaning previous publish directory..."
    rm -rf "$OUTPUT_BASE"
fi

# Create output directory
mkdir -p "$OUTPUT_BASE"

# Function to publish for a specific runtime
publish_runtime() {
    local runtime=$1
    local platform=$2
    
    echo ""
    echo "Publishing for $platform ($runtime)..."
    
    dotnet publish "$PROJECT_PATH" \
        --configuration Release \
        --runtime "$runtime" \
        --self-contained true \
        --output "$OUTPUT_BASE/$platform" \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=false \
        /p:IncludeNativeLibrariesForSelfExtract=true
    
    echo "✓ Published $platform to $OUTPUT_BASE/$platform"
}

# Windows x64
publish_runtime "win-x64" "windows-x64"

# Windows ARM64
publish_runtime "win-arm64" "windows-arm64"

# macOS x64
publish_runtime "osx-x64" "macos-x64"

# macOS ARM64 (Apple Silicon)
publish_runtime "osx-arm64" "macos-arm64"

# Linux x64
publish_runtime "linux-x64" "linux-x64"

# Linux ARM64
publish_runtime "linux-arm64" "linux-arm64"

echo ""
echo "=========================================="
echo "✓ All platforms published successfully!"
echo "=========================================="
echo ""
echo "Executables are located in the following directories:"
echo "  - $OUTPUT_BASE/windows-x64/azdo-wi-creator.exe"
echo "  - $OUTPUT_BASE/windows-arm64/azdo-wi-creator.exe"
echo "  - $OUTPUT_BASE/macos-x64/azdo-wi-creator"
echo "  - $OUTPUT_BASE/macos-arm64/azdo-wi-creator"
echo "  - $OUTPUT_BASE/linux-x64/azdo-wi-creator"
echo "  - $OUTPUT_BASE/linux-arm64/azdo-wi-creator"
echo ""
