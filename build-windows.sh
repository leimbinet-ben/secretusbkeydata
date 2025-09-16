#!/bin/bash
echo "Building Windows executable from macOS/Linux..."
echo "This will create a .exe file that requires .NET 9 to be installed on the target Windows machine"
dotnet publish -c Release -r win-x64 -o dist/win-framework

echo ""
echo "Building self-contained Windows executable (larger file, no .NET required)..."
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o dist/win-standalone

echo ""
echo "Build complete! Check the following directories:"
echo "- dist/win-framework/secretusbkeydata.exe (requires .NET 9)"
echo "- dist/win-standalone/secretusbkeydata.exe (standalone)"