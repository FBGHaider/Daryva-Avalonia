#!/bin/bash
# Simple build verification script

echo "=========================================="
echo "Building Daryva Solution"
echo "=========================================="

cd "$(dirname "$0")"

# Build API
echo ""
echo "Building API..."
dotnet build src/Daryva.Api/Daryva.Api.csproj -v quiet

if [ $? -eq 0 ]; then
    echo "✓ API build successful"
else
    echo "✗ API build failed"
    exit 1
fi

# Build UI
echo ""
echo "Building UI..."
dotnet build src/Daryva.UI/Daryva.csproj -v quiet

if [ $? -eq 0 ]; then
    echo "✓ UI build successful"
else
    echo "✗ UI build failed"
    exit 1
fi

echo ""
echo "=========================================="
echo "All builds successful!"
echo "=========================================="
