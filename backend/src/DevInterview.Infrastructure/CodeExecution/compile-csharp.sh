#!/bin/sh
# Compiles Main.cs with the Roslyn compiler shipped in the .NET SDK image, without NuGet/network access.
set -e
CSC=$(ls -d /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -n 1)
REF=$(ls -d /usr/share/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -n 1)
ARGS=""
for f in "$REF"/*.dll; do ARGS="$ARGS -r:$f"; done
dotnet "$CSC" -nologo -optimize+ -langversion:latest -nullable:disable -out:main.dll $ARGS Main.cs
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"},"configProperties":{"System.GC.Concurrent":false}}}' > main.runtimeconfig.json
