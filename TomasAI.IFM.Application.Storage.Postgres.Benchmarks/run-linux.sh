#!/bin/sh
set -eu
mkdir /work
cp /source/*.cs /source/*.csproj /work/
cd /work
dotnet run -c Release -- --filter '*PostgresTuningBenchmarks*' --artifacts /results --exporters json csv
