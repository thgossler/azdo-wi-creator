#!/bin/bash

# Script to publish azdo-wi-creator for all platforms and create compressed archives
# This script calls publish-all.sh and then creates zip/tar.gz archives for each platform

set -e  # Exit on error

OUTPUT_BASE="publish"
CSPROJ_FILE="azdo-wi-creator/azdo-wi-creator.csproj"

echo "=========================================="
echo "Publishing and compressing binaries"
echo "=========================================="

# Extract version from csproj file
echo ""
echo "Extracting version from $CSPROJ_FILE..."
VERSION=$(grep -oP '<Version>\K[^<]+' "$CSPROJ_FILE" 2>/dev/null || grep -o '<Version>[^<]*</Version>' "$CSPROJ_FILE" | sed 's/<Version>//;s/<\/Version>//')
if [ -z "$VERSION" ]; then
    echo "Warning: Could not extract version from $CSPROJ_FILE, using default '1.0.0'"
    VERSION="1.0.0"
fi
echo "Version: $VERSION"

# Step 1: Run publish-all.sh
echo ""
echo "Step 1: Running publish-all.sh..."
./publish-all.sh

# Step 2: Create archives
echo ""
echo "=========================================="
echo "Step 2: Creating compressed archives..."
echo "=========================================="

cd "$OUTPUT_BASE"

# Windows x64 (zip)
echo ""
echo "Creating archive for Windows x64..."
zip -r azdo-wi-creator-win-x64-v${VERSION}.zip win-x64/azdo-wi-creator.exe
echo "✓ Created azdo-wi-creator-win-x64-v${VERSION}.zip"

# Windows ARM64 (zip)
echo ""
echo "Creating archive for Windows ARM64..."
zip -r azdo-wi-creator-win-arm64-v${VERSION}.zip win-arm64/azdo-wi-creator.exe
echo "✓ Created azdo-wi-creator-win-arm64-v${VERSION}.zip"

# macOS x64 (tar.gz)
echo ""
echo "Creating archive for macOS x64..."
tar -czf azdo-wi-creator-osx-x64-v${VERSION}.tar.gz -C osx-x64 azdo-wi-creator
echo "✓ Created azdo-wi-creator-osx-x64-v${VERSION}.tar.gz"

# macOS ARM64 (tar.gz)
echo ""
echo "Creating archive for macOS ARM64..."
tar -czf azdo-wi-creator-osx-arm64-v${VERSION}.tar.gz -C osx-arm64 azdo-wi-creator
echo "✓ Created azdo-wi-creator-osx-arm64-v${VERSION}.tar.gz"

# Linux x64 (tar.gz)
echo ""
echo "Creating archive for Linux x64..."
tar -czf azdo-wi-creator-linux-x64-v${VERSION}.tar.gz -C linux-x64 azdo-wi-creator
echo "✓ Created azdo-wi-creator-linux-x64-v${VERSION}.tar.gz"

# Linux ARM64 (tar.gz)
echo ""
echo "Creating archive for Linux ARM64..."
tar -czf azdo-wi-creator-linux-arm64-v${VERSION}.tar.gz -C linux-arm64 azdo-wi-creator
echo "✓ Created azdo-wi-creator-linux-arm64-v${VERSION}.tar.gz"

cd ..

echo ""
echo "=========================================="
echo "✓ All archives created successfully!"
echo "=========================================="
echo ""
echo "Archives are located in the $OUTPUT_BASE/ directory:"
echo "  - $OUTPUT_BASE/azdo-wi-creator-win-x64-v${VERSION}.zip"
echo "  - $OUTPUT_BASE/azdo-wi-creator-win-arm64-v${VERSION}.zip"
echo "  - $OUTPUT_BASE/azdo-wi-creator-osx-x64-v${VERSION}.tar.gz"
echo "  - $OUTPUT_BASE/azdo-wi-creator-osx-arm64-v${VERSION}.tar.gz"
echo "  - $OUTPUT_BASE/azdo-wi-creator-linux-x64-v${VERSION}.tar.gz"
echo "  - $OUTPUT_BASE/azdo-wi-creator-linux-arm64-v${VERSION}.tar.gz"
echo ""
echo "These archives are ready to be uploaded to a GitHub release."
echo ""
