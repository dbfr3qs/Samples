// PRF Demo Page JavaScript
// Handles passkey authentication with PRF extension

document.addEventListener('DOMContentLoaded', function() {
    const authenticateBtn = document.getElementById('authenticateBtn');
    if (!authenticateBtn) return;

    authenticateBtn.addEventListener('click', async function() {
        const statusDiv = document.getElementById('statusMessage');
        const btn = this;
        
        try {
            btn.disabled = true;
            statusDiv.innerHTML = '<div class="alert alert-info">Requesting authentication options...</div>';
            
            // Get authentication options from server
            const optionsResponse = await fetch('/Identity/Account/PasskeyRequestOptions', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            });
            
            if (!optionsResponse.ok) {
                throw new Error('Failed to get authentication options');
            }
            
            const options = await optionsResponse.json();
            
            statusDiv.innerHTML = '<div class="alert alert-info">Waiting for passkey authentication...</div>';
            
            // Convert base64url to Uint8Array
            const base64urlToUint8Array = (base64url) => {
                const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
                const binary = atob(base64);
                const bytes = new Uint8Array(binary.length);
                for (let i = 0; i < binary.length; i++) {
                    bytes[i] = binary.charCodeAt(i);
                }
                return bytes;
            };
            
            // Prepare credential request options
            const publicKeyOptions = {
                challenge: base64urlToUint8Array(options.challenge),
                timeout: options.timeout,
                rpId: options.rpId,
                allowCredentials: options.allowCredentials?.map(cred => ({
                    type: cred.type,
                    id: base64urlToUint8Array(cred.id)
                })),
                userVerification: options.userVerification
            };
            
            // Convert PRF extension salts from base64url to ArrayBuffer
            if (options.extensions && options.extensions.prf) {
                publicKeyOptions.extensions = {
                    prf: {
                        eval: {
                            first: base64urlToUint8Array(options.extensions.prf.eval.first).buffer
                        }
                    }
                };
                
                // Handle second salt if present
                if (options.extensions.prf.eval.second) {
                    publicKeyOptions.extensions.prf.eval.second = base64urlToUint8Array(options.extensions.prf.eval.second).buffer;
                }
            }
            
            // Get credential
            const credential = await navigator.credentials.get({
                publicKey: publicKeyOptions
            });
            
            // Check if PRF extension was successful
            const prfResults = credential.getClientExtensionResults().prf;
            
            if (prfResults && prfResults.results && prfResults.results.first) {
                // Convert ArrayBuffer to base64
                const prfOutput = btoa(String.fromCharCode(...new Uint8Array(prfResults.results.first)));
                
                statusDiv.innerHTML = '<div class="alert alert-success">PRF output received! Redirecting...</div>';
                
                // Use URL parameters to pass the PRF output to avoid session issues
                const params = new URLSearchParams();
                params.append('prfOutput', prfOutput);
                params.append('prfEnabled', 'true');
                
                // Redirect to the same page with the PRF output as query parameters
                window.location.href = '/account/prfdemo?' + params.toString();
            } else {
                statusDiv.innerHTML = '<div class="alert alert-warning">Authentication successful, but PRF extension was not supported by your authenticator.</div>';
                btn.disabled = false;
            }
            
        } catch (error) {
            console.error('Authentication error:', error);
            statusDiv.innerHTML = '<div class="alert alert-danger">Error: ' + error.message + '</div>';
            btn.disabled = false;
        }
    });
});
