#!/bin/bash

# Start Server 5001
# Local servers for Nginx balancing testing
cd "$(dirname "$0")/webProject"
echo "Starting Server 5001..."
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --urls "http://localhost:5001" --no-launch-profile -- --ServerInfo:ServerName=SERVER-5001 --ServerInfo:Port=5001
