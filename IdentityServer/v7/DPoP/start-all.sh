#!/bin/bash

# Script to start all services for the DPoP attack demo
# This will open 4 terminal tabs and start each service

echo "Starting all services for DPoP Attack Demo..."
echo ""
echo "Services will run on:"
echo "  - IdentityServerHost: https://localhost:5001"
echo "  - Api: https://localhost:5005"
echo "  - WebClient: https://localhost:5010"
echo "  - AttackerApi: https://localhost:7666"
echo ""

# Check if we're on macOS
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS - use osascript to open new Terminal tabs
    
    # Get the current directory
    DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
    
    # Open new Terminal window and run commands in tabs
    osascript <<EOF
tell application "Terminal"
    activate
    
    -- IdentityServerHost
    do script "cd '$DIR/IdentityServerHost' && echo 'Starting IdentityServerHost on port 5001...' && dotnet run"
    
    -- Wait a bit
    delay 1
    
    -- Api
    tell application "System Events" to keystroke "t" using {command down}
    delay 0.5
    do script "cd '$DIR/Api' && echo 'Starting Api on port 5005...' && dotnet run" in front window
    
    -- Wait a bit
    delay 1
    
    -- WebClient
    tell application "System Events" to keystroke "t" using {command down}
    delay 0.5
    do script "cd '$DIR/WebClient' && echo 'Starting WebClient on port 5010...' && dotnet run" in front window
    
    -- Wait a bit
    delay 1
    
    -- AttackerApi
    tell application "System Events" to keystroke "t" using {command down}
    delay 0.5
    do script "cd '$DIR/AttackerApi' && echo 'Starting AttackerApi on port 7666...' && dotnet run" in front window
end tell
EOF

    echo "All services started in new Terminal tabs!"
    echo ""
    echo "To run the attack demo:"
    echo "1. Wait for all services to start (check the terminal tabs)"
    echo "2. Open browser 1 (Attacker): https://localhost:5010/Home/AttackDemo"
    echo "3. Open browser 2 (Victim): https://localhost:5010/Home/AttackVictim"
    echo "4. Follow the instructions in ATTACK_DEMO_README.md"
    
else
    # Linux or other - provide manual instructions
    echo "Please open 4 terminal windows and run these commands:"
    echo ""
    echo "Terminal 1:"
    echo "  cd IdentityServerHost && dotnet run"
    echo ""
    echo "Terminal 2:"
    echo "  cd Api && dotnet run"
    echo ""
    echo "Terminal 3:"
    echo "  cd WebClient && dotnet run"
    echo ""
    echo "Terminal 4:"
    echo "  cd AttackerApi && dotnet run"
fi
