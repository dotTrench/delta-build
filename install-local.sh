#!/usr/bin/env bash
set -e

dotnet pack delta-build/delta-build.csproj -o /tmp/delta-build-local -c Release /p:Version=0.0.0-local
dotnet tool uninstall -g delta-build 2>/dev/null || true
dotnet tool install -g delta-build --version 0.0.0-local --add-source /tmp/delta-build-local
