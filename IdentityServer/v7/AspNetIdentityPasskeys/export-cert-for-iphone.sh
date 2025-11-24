#!/bin/bash

# Export ASP.NET Core Development Certificate for iPhone
# This script exports the development certificate in a format that can be installed on iOS devices

set -e

echo "🔐 Exporting ASP.NET Core Development Certificate for iPhone"
echo ""

# Set password for the certificate
CERT_PASSWORD="DevCertPassword123"
OUTPUT_DIR="$HOME/Desktop"

# Export as PFX
echo "📦 Exporting certificate as PFX..."
dotnet dev-certs https -ep "$OUTPUT_DIR/aspnetcore-dev-cert.pfx" -p "$CERT_PASSWORD"

# Convert to CER format for iOS (include CA certificates)
echo "📱 Converting to CER format for iOS..."
openssl pkcs12 -in "$OUTPUT_DIR/aspnetcore-dev-cert.pfx" \
    -nokeys \
    -out "$OUTPUT_DIR/aspnetcore-dev-cert.cer" \
    -passin pass:"$CERT_PASSWORD"

# Clean up PFX file
rm "$OUTPUT_DIR/aspnetcore-dev-cert.pfx"

echo ""
echo "✅ Certificate exported successfully!"
echo ""
echo "📍 Location: $OUTPUT_DIR/aspnetcore-dev-cert.cer"
echo ""
echo "📲 Next steps:"
echo "1. Transfer aspnetcore-dev-cert.cer to your iPhone via:"
echo "   - AirDrop (easiest)"
echo "   - Email"
echo "   - iCloud Drive"
echo ""
echo "2. On iPhone, tap the certificate file"
echo "3. Go to Settings → General → VPN & Device Management"
echo "4. Install the profile"
echo "5. Go to Settings → General → About → Certificate Trust Settings"
echo "6. Enable full trust for the certificate"
echo ""
echo "📖 Full instructions: See IPHONE_SETUP.md"
