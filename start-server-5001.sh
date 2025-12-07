#!/bin/bash

# Start Server 5001
cd "$(dirname "$0")/webProject"
echo "Starting Server 5001..."
dotnet run --urls "http://localhost:5001" --environment "Production" --launch-profile "" -- --ServerInfo:ServerName=SERVER-5001 --ServerInfo:Port=5001

