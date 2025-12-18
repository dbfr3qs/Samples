#!/bin/bash

# Export WebView app certificate for iPhone installation
# This script copies the certificate to a convenient location for transfer

CERT_FILE="./WebViewApp/certs/web-dev-cert.cer"
DEST_DIR="$HOME/Desktop"
DEST_FILE="$DEST_DIR/web-dev-cert.cer"

if [ ! -f "$CERT_FILE" ]; then
    echo "❌ Certificate not found at $CERT_FILE"
    echo "Run ./generate-web-cert.sh first"
    exit 1
fi

echo "📋 Copying certificate to Desktop..."
cp "$CERT_FILE" "$DEST_FILE"

echo ""
echo "✅ Certificate copied to: $DEST_FILE"
echo ""
echo "📱 Transfer to iPhone using one of these methods:"
echo ""
echo "Option 1: AirDrop"
echo "  1. Right-click web-dev-cert.cer on your Desktop"
echo "  2. Select 'Share' → 'AirDrop'"
echo "  3. Send to your iPhone"
echo ""
echo "Option 2: Email"
echo "  1. Email the web-dev-cert.cer file to yourself"
echo "  2. Open the email on your iPhone"
echo "  3. Tap the attachment"
echo ""
echo "Option 3: iCloud Drive"
echo "  1. Copy web-dev-cert.cer to iCloud Drive"
echo "  2. Open Files app on iPhone"
echo "  3. Navigate to the certificate and tap it"
echo ""
echo "📱 Install on iPhone:"
echo "  1. After receiving the certificate, tap it"
echo "  2. Go to Settings → General → VPN & Device Management"
echo "  3. Under 'Downloaded Profile', tap the certificate"
echo "  4. Tap Install (top right)"
echo "  5. Enter your passcode"
echo "  6. Tap Install again (warning will appear)"
echo "  7. Tap Install one more time"
echo "  8. Tap Done"
echo ""
echo "🔒 Trust the Certificate:"
echo "  1. Go to Settings → General → About → Certificate Trust Settings"
echo "  2. Under 'Enable Full Trust for Root Certificates'"
echo "  3. Toggle ON the switch for 'web.dev.internal'"
echo "  4. Tap Continue on the warning"
echo ""
echo "✅ Once installed, the iPhone will trust https://web.dev.internal:5003"
echo ""
