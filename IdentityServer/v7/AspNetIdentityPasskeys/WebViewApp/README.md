# WebView App

Simple ASP.NET Razor Pages app for testing WebView integration with the iOS mobile app.

## Setup

### 1. Certificate is already created

The self-signed certificate is in `certs/webview-dev-cert.pfx` and is automatically loaded by the app.

### 2. Install Certificate on iOS Simulator

To trust the certificate in the iOS Simulator:

```bash
# Copy certificate to simulator
cp certs/webview-dev-cert.cer ~/Desktop/

# Then in iOS Simulator:
# 1. Drag the .cer file onto the simulator
# 2. Go to Settings > General > VPN & Device Management
# 3. Tap on the certificate and tap "Install"
# 4. Go to Settings > General > About > Certificate Trust Settings
# 5. Enable full trust for the certificate
```

Or use the install script:
```bash
# Install to all running simulators
xcrun simctl list devices | grep Booted | awk -F'[()]' '{print $2}' | xargs -I {} xcrun simctl keychain {} add-root-cert certs/webview-dev-cert.cer
```

## Running

```bash
dotnet run
```

The app will be available at `https://localhost:5003`

## Features

- Minimal layout (no navigation, header, or footer)
- Simple content page with current time
- Interactive button for testing JavaScript
