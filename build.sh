#!/bin/bash

PROJECT="./src/Api/Api.csproj"

echo "Restoring NuGet packages..."
dotnet restore $PROJECT

echo "Building the project..."
dotnet publish $PROJECT -c Release -r win-x64 -o bin