$ErrorActionPreference = 'Stop'

$out = Join-Path $env:TEMP 'delta-build-local'

dotnet pack delta-build/delta-build.csproj -o $out -c Release /p:Version=0.0.0-local
dotnet tool uninstall -g delta-build 2>$null
dotnet tool install -g delta-build --version 0.0.0-local --add-source $out
