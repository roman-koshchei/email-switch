#!/bin/bash

echo "Building the project..."

dotnet publish ./src/Api/Api.csproj \
  -c Release -r win-x64 -o out \
  -p:DebugSymbols=false -p:DebugType=none \