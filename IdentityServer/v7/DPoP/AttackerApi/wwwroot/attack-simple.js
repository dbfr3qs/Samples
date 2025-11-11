/**
 * Simplified OAuth DPoP Attack Script
 * This version is designed to work with the standalone AttackerApi UI
 * No longer needs WebClient-specific logic
 */

const ATTACKER_API = 'https://localhost:7666';

class OAuthAttack {
    constructor() {
        this.sessionId = this.getOrCreateSessionId();
    }

    getOrCreateSessionId() {
        let sessionId = sessionStorage.getItem('attack_session_id');
        if (!sessionId) {
            sessionId = 'session_' + Math.random().toString(36).substring(2, 15);
            sessionStorage.setItem('attack_session_id', sessionId);
        }
        return sessionId;
    }

    /**
     * Initialize as ATTACKER mode
     * For the standalone UI - just sets up session
     */
    async initAttacker() {
        console.log('[ATTACKER] Session ID:', this.sessionId);
        const sessionIdElement = document.getElementById('session-id');
        if (sessionIdElement) {
            sessionIdElement.textContent = this.sessionId;
        }
    }
}

// Export for use
window.OAuthAttack = OAuthAttack;
