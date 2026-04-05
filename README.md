# delta-build

Inspired by [dotnet-affected](https://github.com/leonardochaia/dotnet-affected)

## How it works

delta-build works by taking snapshots of your build graph at different points in time, then comparing them to determine
which projects are affected by changes. It uses MSBuild's project graph and git blob hashes to precisely identify what
has changed.

A snapshot captures:

- All projects in the build graph and their input files
- Git blob hashes for each input file
- Project reference relationships