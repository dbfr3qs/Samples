#!/bin/bash

# Setup script for IdpMobileClient Xcode project

set -e

echo "🚀 Setting up IdpMobileClient Xcode project..."

# Check if xcodegen is installed
if ! command -v xcodegen &> /dev/null; then
    echo "❌ xcodegen not found. Installing via Homebrew..."
    if command -v brew &> /dev/null; then
        brew install xcodegen
    else
        echo "❌ Homebrew not found. Please install xcodegen manually:"
        echo "   brew install xcodegen"
        echo "   or visit: https://github.com/yonaskolb/XcodeGen"
        exit 1
    fi
fi

# Generate Xcode project
echo "📦 Generating Xcode project..."
xcodegen generate

# Check if project was created
if [ -f "IdpMobileClient.xcodeproj/project.pbxproj" ]; then
    echo "✅ Xcode project created successfully!"
    echo ""
    echo "📝 Next steps:"
    echo "   1. Open IdpMobileClient.xcodeproj in Xcode"
    echo "   2. Select the IdpMobileDemoApp target"
    echo "   3. Update DEVELOPMENT_TEAM in project settings"
    echo "   4. Build and run on iOS 18 simulator or device"
    echo ""
    echo "🔧 Configuration:"
    echo "   - IdP: https://idp.dev.internal:5000"
    echo "   - API: https://api.dev.internal:5002"
    echo "   - Client ID: mobile-client"
    echo "   - Redirect URI: com.idp.mobile://callback"
    echo ""
    
    # Ask if user wants to open the project
    read -p "Open project in Xcode now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        open IdpMobileClient.xcodeproj
    fi
else
    echo "❌ Failed to create Xcode project"
    exit 1
fi
