#!/bin/bash

# Script to install ASP.NET Core development certificate to iOS Simulator

set -e

CERT_PATH="$(dirname "$0")/aspnetcore-dev-cert.cer"
SCRIPT_DIR="$(dirname "$0")"

echo "🔐 iOS Simulator Certificate Installer"
echo "======================================="
echo ""

# Check if certificate exists
if [ ! -f "$CERT_PATH" ]; then
    echo "❌ Certificate not found at: $CERT_PATH"
    echo ""
    echo "Please ensure aspnetcore-dev-cert.cer is in the same directory as this script."
    echo "You can export it from your ASP.NET Core project or from Keychain Access."
    exit 1
fi

echo "✓ Found certificate: $CERT_PATH"
echo ""

# Check if a simulator is booted
BOOTED_DEVICE=$(xcrun simctl list devices | grep "(Booted)" | head -n 1)

if [ -z "$BOOTED_DEVICE" ]; then
    echo "⚠️  No simulator is currently running."
    echo ""
    echo "Available simulators:"
    xcrun simctl list devices | grep "iPhone" | grep -v "unavailable" | head -n 10
    echo ""
    
    # Get first available iPhone simulator
    DEVICE_ID=$(xcrun simctl list devices | grep "iPhone 16" | grep -v "unavailable" | head -n 1 | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/')
    
    if [ -z "$DEVICE_ID" ]; then
        # Fallback to any iPhone
        DEVICE_ID=$(xcrun simctl list devices | grep "iPhone" | grep -v "unavailable" | head -n 1 | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/')
    fi
    
    if [ -z "$DEVICE_ID" ]; then
        echo "❌ No iPhone simulators available."
        exit 1
    fi
    
    echo "📱 Booting simulator: $DEVICE_ID"
    xcrun simctl boot "$DEVICE_ID"
    sleep 2
    open -a Simulator
    sleep 3
else
    echo "✓ Simulator is already running"
    echo "   $BOOTED_DEVICE"
    echo ""
fi

# Install certificate
echo "📦 Installing certificate to simulator..."
if xcrun simctl keychain booted add-root-cert "$CERT_PATH"; then
    echo "✅ Certificate installed successfully!"
    echo ""
    
    # Ask if user wants to restart simulator
    echo "⚠️  The simulator needs to be restarted for the certificate to take effect."
    read -p "Restart simulator now? (y/n) " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "🔄 Restarting simulator..."
        
        # Get the booted device ID
        DEVICE_ID=$(xcrun simctl list devices | grep "(Booted)" | head -n 1 | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/')
        
        xcrun simctl shutdown booted
        sleep 2
        xcrun simctl boot "$DEVICE_ID"
        open -a Simulator
        
        echo "✅ Simulator restarted!"
        echo ""
        echo "🎉 All done! The simulator now trusts your ASP.NET Core certificate."
        echo ""
        echo "You can now:"
        echo "  1. Build and run your app from Xcode (⌘R)"
        echo "  2. The app will connect to https://idp.dev.internal:5001 without SSL errors"
        echo ""
    else
        echo ""
        echo "⚠️  Remember to restart the simulator before testing:"
        echo "   xcrun simctl shutdown booted"
        echo "   # Then relaunch from Xcode"
        echo ""
    fi
else
    echo "❌ Failed to install certificate"
    exit 1
fi

echo "💡 Tip: Run this script again if you create a new simulator or reset the current one."
