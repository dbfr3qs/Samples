# API Project

This is a simple HTTP API with JWT Bearer authentication that uses the IdentityServer instance as its OAuth2 Authorization Server.

## Endpoint

### GET /claims

A secured endpoint that returns the claims present in the access token used to authenticate the request.

**Authentication:** Required (JWT Bearer token)

**Response:**
```json
{
  "claims": [
    {
      "type": "claim_type",
      "value": "claim_value"
    }
  ],
  "identity": "client_id",
  "isAuthenticated": true
}
```

## Running the API

1. Start the IdentityServer project first:
   ```bash
   cd IdentityServerAspNetIdentityPasskeys
   dotnet run
   ```

2. In a separate terminal, start the API project:
   ```bash
   cd Api
   dotnet run
   ```

The API will be available at: `https://localhost:6001`

## Testing the API

### 1. Get an Access Token from IdentityServer

Use the client credentials flow to obtain an access token:

```bash
curl -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=api.client" \
  -d "client_secret=api-secret" \
  -d "grant_type=client_credentials" \
  -d "scope=api"
```

Or use the existing m2m.client:

```bash
curl -X POST https://localhost:5001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=m2m.client" \
  -d "client_secret=511536EF-F270-4058-80CA-1C89C192F69A" \
  -d "grant_type=client_credentials" \
  -d "scope=api"
```

### 2. Call the API with the Access Token

```bash
curl -X GET https://localhost:6001/claims \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN_HERE"
```

## Configuration

The API is configured to:
- Use JWT Bearer authentication
- Trust tokens issued by `https://localhost:5001` (the IdentityServer instance)
- Not validate audience (for simplicity in this sample)
- Run on `https://localhost:6001`

## Clients Configured in IdentityServer

Two clients can access this API:

1. **api.client**
   - Client ID: `api.client`
   - Client Secret: `api-secret`
   - Allowed Scopes: `api`

2. **m2m.client** (existing client, updated)
   - Client ID: `m2m.client`
   - Client Secret: `511536EF-F270-4058-80CA-1C89C192F69A`
   - Allowed Scopes: `scope1`, `api`
