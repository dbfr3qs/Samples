# PRF (Pseudo-Random Function) Extension for Passkeys

## Overview

The PRF extension has been added to the passkey implementation in this project. The PRF extension is a WebAuthn extension that allows generating cryptographic keys derived from the passkey credential.

## What is PRF?

The PRF (Pseudo-Random Function) extension enables:
- **Deterministic key derivation**: Generate the same cryptographic output for the same input salt
- **Client-side encryption**: Use the derived keys for encrypting user data locally
- **Password-less key management**: Derive encryption keys without storing them

## Implementation Details

The PRF extension is implemented in `PasskeyEndpointRouteBuilderExtensions.cs` by modifying the JSON options returned by the passkey endpoints.

### Important: Base64url Encoding

WebAuthn requires **base64url** encoding (RFC 4648 Section 5) for all binary data, including PRF salts. This differs from standard base64:

- **Standard base64:** Uses `+` and `/`, includes padding (`=`)
- **Base64url:** Uses `-` and `_`, no padding

The implementation includes a helper method to convert to base64url:

```csharp
private static string ToBase64Url(byte[] bytes)
{
    return Convert.ToBase64String(bytes)
        .TrimEnd('=')           // Remove padding
        .Replace('+', '-')      // Replace + with -
        .Replace('/', '_');     // Replace / with _
}
```

### Registration (Creation)
When creating a new passkey, the PRF extension is added to the creation options:

```json
{
  "extensions": {
    "prf": {
      "eval": {
        "first": "base64-encoded-salt"
      }
    }
  }
}
```

### Authentication (Request)
When authenticating with a passkey, the PRF extension is added to the request options:

```json
{
  "extensions": {
    "prf": {
      "eval": {
        "first": "base64-encoded-salt"
      }
    }
  }
}
```

## Usage

### During Registration
When a user registers a new passkey:
1. The browser receives the creation options with the PRF extension
2. The authenticator evaluates the PRF with the provided salt
3. The PRF output is returned in the credential response

### During Authentication
When a user authenticates with a passkey:
1. The browser receives the request options with the PRF extension
2. The authenticator evaluates the PRF with the provided salt
3. The PRF output is returned in the assertion response

## Customization

### Changing the Salt
The current implementation uses a static salt (`"first-salt"`). For production use, you should:

1. **Use dynamic salts**: Generate unique salts per user or per operation
2. **Store salt metadata**: Keep track of which salts are used for what purpose
3. **Implement salt rotation**: Update salts periodically for security

Example modification in `PasskeyEndpointRouteBuilderExtensions.cs`:

```csharp
// Instead of static salt
["first"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("first-salt"))

// Use dynamic salt based on user ID
var salt = $"user-{userId}-salt";
["first"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(salt))
```

### Adding Second Salt
The PRF extension supports a second salt for additional key derivation:

```csharp
extensions["prf"] = new JsonObject
{
    ["eval"] = new JsonObject
    {
        ["first"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("first-salt")),
        ["second"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("second-salt"))
    }
};
```

## Browser Support

PRF extension support varies by browser and authenticator:
- **Chrome/Edge**: Supported on compatible authenticators
- **Safari**: Supported on macOS/iOS with compatible authenticators
- **Firefox**: Check current support status

## Security Considerations

1. **Salt Management**: Salts should be treated as sensitive data
2. **Output Handling**: PRF outputs are cryptographic keys and must be handled securely
3. **Authenticator Support**: Not all authenticators support PRF - implement fallback mechanisms
4. **Key Derivation**: Use appropriate key derivation functions (KDFs) when using PRF outputs

## Testing PRF

To test if PRF is working:

1. Register a new passkey
2. Check the browser console for the credential response
3. Look for `clientExtensionResults.prf` in the response
4. Verify that `prf.results.first` contains the derived output

Example response:
```json
{
  "clientExtensionResults": {
    "prf": {
      "enabled": true,
      "results": {
        "first": "base64-encoded-prf-output"
      }
    }
  }
}
```

## References

- [WebAuthn PRF Extension Spec](https://w3c.github.io/webauthn/#prf-extension)
- [FIDO Alliance PRF Documentation](https://fidoalliance.org/)
