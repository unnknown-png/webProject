#!/bin/bash

# Start Server 5002
# Local servers for Nginx balancing testing
cd "$(dirname "$0")/webProject"
echo "Starting Server 5002..."
dotnet run --urls "http://localhost:5002" --environment "Production" --launch-profile "" -- --ServerInfo:ServerName=SERVER-5002 --ServerInfo:Port=5002