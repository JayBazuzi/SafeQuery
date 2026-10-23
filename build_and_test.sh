#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
