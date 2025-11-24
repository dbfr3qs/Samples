#!/bin/bash

# Test script for the API
# This script will:
# 1. Request an access token from IdentityServer
# 2. Call the /claims endpoint with the token

echo "=== Testing API with IdentityServer ==="
echo ""

# Request access token
echo "1. Requesting access token from IdentityServer..."
TOKEN_RESPONSE=$(curl -s -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=api.client" \
  -d "client_secret=api-secret" \
  -d "grant_type=client_credentials" \
  -d "scope=api" \
  --insecure)

# Extract access token
ACCESS_TOKEN=$(echo $TOKEN_RESPONSE | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
  echo "❌ Failed to get access token"
  echo "Response: $TOKEN_RESPONSE"
  exit 1
fi

echo "✅ Access token received"
echo ""

# Call the API
echo "2. Calling /claims endpoint..."
echo ""
curl -X GET https://localhost:6001/claims \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  --insecure \
  | jq '.'

echo ""
echo "=== Test complete ==="
