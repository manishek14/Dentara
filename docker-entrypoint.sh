#!/bin/bash
# Dentara — Docker entrypoint script
# Runs EF Core migrations on startup, then launches the API.
set -e

echo "🔧 Dentara API starting..."
echo "   ConnectionString: ${ConnectionStrings__DentaraDb:-(not set)}"
echo "   Environment: ${ASPNETCORE_ENVIRONMENT}"

# Wait for SQL Server to be ready (max 60 seconds)
if [ -n "$ConnectionStrings__DentaraDb" ]; then
    echo "⏳ Waiting for SQL Server..."
    for i in $(seq 1 60); do
        if dotnet Dentara.Api.dll --migrate-only 2>/dev/null; then
            break
        fi
        echo "   Attempt $i/60 — SQL Server not ready yet..."
        sleep 2
    done
fi

echo "🚀 Starting Dentara API..."
exec dotnet Dentara.Api.dll
