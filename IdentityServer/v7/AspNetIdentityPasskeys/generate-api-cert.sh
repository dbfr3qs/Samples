#!/bin/bash

# Generate self-signed certificate for api.dev.internal
# This certificate will be used by the API and trusted by the iPhone

CERT_DIR="./Api/certs"
CERT_NAME="api-dev-cert"

# Create certs directory if it doesn't exist
mkdir -p "$CERT_DIR"

# Create OpenSSL configuration file
cat > "$CERT_DIR/openssl.cnf" << EOF
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = dn
req_extensions = v3_req
x509_extensions = v3_ca

[dn]
C=NZ
ST=Auckland
L=Auckland
O=Development
OU=API
CN=api.dev.internal

[v3_req]
subjectAltName = @alt_names
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth

[v3_ca]
subjectAltName = @alt_names
keyUsage = critical, digitalSignature, keyEncipherment, keyCertSign
extendedKeyUsage = serverAuth
basicConstraints = critical, CA:TRUE
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid:always,issuer

[alt_names]
DNS.1 = api.dev.internal
DNS.2 = *.dev.internal
DNS.3 = localhost
IP.1 = 192.168.178.153
IP.2 = 127.0.0.1
EOF

echo "🔐 Generating private key..."
openssl genrsa -out "$CERT_DIR/$CERT_NAME.key" 2048

echo "🔐 Generating certificate signing request..."
openssl req -new -key "$CERT_DIR/$CERT_NAME.key" -out "$CERT_DIR/$CERT_NAME.csr" -config "$CERT_DIR/openssl.cnf"

echo "🔐 Generating self-signed certificate..."
openssl x509 -req -days 365 -in "$CERT_DIR/$CERT_NAME.csr" \
    -signkey "$CERT_DIR/$CERT_NAME.key" \
    -out "$CERT_DIR/$CERT_NAME.crt" \
    -extensions v3_ca \
    -extfile "$CERT_DIR/openssl.cnf"

echo "🔐 Creating PFX file for .NET..."
openssl pkcs12 -export -out "$CERT_DIR/$CERT_NAME.pfx" \
    -inkey "$CERT_DIR/$CERT_NAME.key" \
    -in "$CERT_DIR/$CERT_NAME.crt" \
    -passout pass:dev-password

echo "🔐 Converting to CER format for iPhone..."
openssl x509 -in "$CERT_DIR/$CERT_NAME.crt" -out "$CERT_DIR/$CERT_NAME.cer" -outform DER

echo ""
echo "✅ Certificate generated successfully!"
echo ""
echo "Files created:"
echo "  - $CERT_DIR/$CERT_NAME.pfx (for .NET API)"
echo "  - $CERT_DIR/$CERT_NAME.cer (for iPhone)"
echo "  - $CERT_DIR/$CERT_NAME.crt (certificate)"
echo "  - $CERT_DIR/$CERT_NAME.key (private key)"
echo ""
echo "Next steps:"
echo "1. Transfer $CERT_DIR/$CERT_NAME.cer to your iPhone"
echo "2. Install and trust the certificate on iPhone"
echo "3. Run the API with: cd Api && dotnet run"
echo ""
