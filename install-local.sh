#!/usr/bin/env bash
set -e

VERSION=${1:-0.0.0-local}

dotnet pack delta-build/delta-build.csproj -o /tmp/delta-build-local -c Release /p:Version=$VERSION
dotnet tool uninstall -g delta-build 2>/dev/null || true
dotnet tool install -g delta-build --version $VERSION --add-source /tmp/delta-build-local
