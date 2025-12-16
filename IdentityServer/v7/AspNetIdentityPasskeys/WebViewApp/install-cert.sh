#!/bin/bash

# Install WebView certificate to iOS Simulator
# Run this script when you have a simulator running

echo "Installing WebView certificate to iOS Simulator..."

# Get the first booted simulator
SIMULATOR_ID=$(xcrun simctl list devices | grep Booted | head -n 1 | awk -F'[()]' '{print $2}')

if [ -z "$SIMULATOR_ID" ]; then
    echo "❌ No booted simulator found. Please start a simulator first."
    exit 1
fi

echo "📱 Found simulator: $SIMULATOR_ID"

# Install the certificate
xcrun simctl keychain "$SIMULATOR_ID" add-root-cert certs/webview-dev-cert.cer

echo "✅ Certificate installed!"
echo ""
echo "⚠️  IMPORTANT: You must manually enable trust in the simulator:"
echo "   1. Open Settings app in simulator"
echo "   2. Go to General > About > Certificate Trust Settings"
echo "   3. Enable full trust for 'localhost'"
echo ""
