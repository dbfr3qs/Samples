# PRF Demo Page

## Overview

A new interactive demo page has been added to the IdentityServer project to showcase the PRF (Pseudo-Random Function) extension for passkeys.

## Accessing the Page

**URL:** `https://localhost:5001/account/prfdemo`

Or click the link from the home page: "Click here to test the PRF (Pseudo-Random Function) extension with your passkey."

## Features

### Interactive Authentication
- Click a button to authenticate with your passkey
- The page automatically handles the WebAuthn flow
- PRF extension is included in the authentication request

### PRF Output Display
After successful authentication, the page displays:
- **PRF Output:** The base64-encoded cryptographic output derived from your passkey
- **Technical Information:** Explanation of what the PRF extension does
- **Use Cases:** Practical applications of PRF-derived keys

### Error Handling
The page handles various scenarios:
- Authenticators that don't support PRF
- Authentication failures
- Network errors

## How It Works

### Client-Side Flow

1. **User clicks "Authenticate with Passkey"**
   - JavaScript fetches authentication options from `/Identity/Account/PasskeyRequestOptions`
   - Options include the PRF extension with a salt value

2. **Browser initiates WebAuthn authentication**
   - Calls `navigator.credentials.get()` with the options
   - Authenticator evaluates the PRF with the provided salt

3. **PRF output is extracted**
   - JavaScript checks `credential.getClientExtensionResults().prf`
   - Extracts the `results.first` ArrayBuffer
   - Converts to base64 string

4. **Form submission**
   - PRF output is submitted to the server via hidden form field
   - Page reloads with the PRF output displayed

### Server-Side Processing

The page model (`PrfDemo.cshtml.cs`) handles:
- **GET:** Initializes the page and generates anti-forgery token
- **POST:** Receives PRF output and displays it to the user

## Technical Details

### Files Created

1. **`Pages/Account/PrfDemo.cshtml`**
   - Razor page with interactive UI
   - JavaScript for WebAuthn authentication
   - Styling for a clean, modern interface

2. **`Pages/Account/PrfDemo.cshtml.cs`**
   - Page model with GET and POST handlers
   - Anti-forgery token management
   - PRF output validation

3. **`Pages/Index.cshtml`** (modified)
   - Added link to PRF demo page

### JavaScript Implementation

The page uses vanilla JavaScript to:
- Fetch authentication options
- Convert base64url to Uint8Array for WebAuthn
- Handle the WebAuthn credential request
- Extract PRF extension results
- Convert ArrayBuffer to base64 for display

### Security Features

- **Anti-forgery tokens:** Protects against CSRF attacks
- **HTTPS required:** PRF extension requires secure context
- **No server-side storage:** PRF output is only displayed, not stored

## Testing the PRF Extension

### Prerequisites

1. **Compatible authenticator:** Your passkey authenticator must support PRF
   - Most modern platform authenticators (Windows Hello, Touch ID, etc.) support PRF
   - Some security keys may not support PRF

2. **Registered passkey:** You must have a passkey registered with the IdentityServer

### Steps to Test

1. Start the IdentityServer:
   ```bash
   cd IdentityServerAspNetIdentityPasskeys
   dotnet run
   ```

2. Navigate to `https://localhost:5001/account/prfdemo`

3. Click "Authenticate with Passkey"

4. Complete the authentication with your passkey

5. View the PRF output displayed on the page

### Expected Results

**If PRF is supported:**
- Green success alert with PRF output
- Base64-encoded string (typically 32-44 characters)
- Technical details about the output

**If PRF is not supported:**
- Warning message indicating authenticator doesn't support PRF
- Authentication may still succeed, but no PRF output

## Use Cases Demonstrated

### Client-Side Encryption
The PRF output can be used as an encryption key:
```javascript
const prfOutput = new Uint8Array(prfResults.results.first);
const encryptionKey = await crypto.subtle.importKey(
    'raw',
    prfOutput,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt', 'decrypt']
);
```

### Deterministic Key Derivation
Same salt always produces the same output:
- Useful for encrypting data that needs to be decrypted later
- No need to store encryption keys
- Keys are derived on-demand from the passkey

### Multiple Keys from One Passkey
Use different salts to derive different keys:
- One key for encrypting files
- Another key for encrypting messages
- Each key is independent but derived from the same passkey

## Customization

### Changing the Salt

The salt is configured in `PasskeyEndpointRouteBuilderExtensions.cs`:

```csharp
["first"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("first-salt"))
```

You can modify this to use dynamic salts based on:
- User ID
- Session ID
- Purpose (e.g., "encryption", "signing")
- Timestamp

### Styling

The page includes embedded CSS that can be customized:
- Colors and branding
- Layout and spacing
- Icons and graphics

### Additional Information

You can extend the page to show:
- Multiple PRF outputs with different salts
- Comparison of PRF outputs across authentications
- Performance metrics
- Browser/authenticator compatibility information

## Browser Compatibility

The PRF extension is supported in:
- **Chrome/Edge:** Version 108+
- **Safari:** Version 17+
- **Firefox:** Check current status

Note: Support also depends on the authenticator (hardware/platform).

## Troubleshooting

### "PRF extension was not supported"
- Your authenticator doesn't support PRF
- Try a different authenticator (e.g., platform authenticator vs security key)
- Update your browser to the latest version

### "Failed to get authentication options"
- Check that IdentityServer is running
- Verify the anti-forgery token is valid
- Check browser console for errors

### Authentication works but no PRF output
- PRF extension may not be enabled on the authenticator
- Check `credential.getClientExtensionResults()` in browser console
- Verify the salt is properly formatted in the server response

## Future Enhancements

Potential improvements:
- Store PRF outputs in session for demonstration
- Show multiple PRF evaluations with different salts
- Implement actual encryption/decryption demo
- Add PRF output comparison tool
- Display raw bytes in addition to base64
