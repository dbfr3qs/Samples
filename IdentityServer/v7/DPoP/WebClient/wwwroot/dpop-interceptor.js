/**
 * DPoP Interceptor for Attack Demo
 * This script intercepts the authorize redirect and adds a DPoP proof that can be stolen
 */

(function() {
    'use strict';

    // Check if we're in attack mode
    const isAttackMode = sessionStorage.getItem('attack_mode') === 'attacker';
    
    if (!isAttackMode) {
        return; // Not in attack mode, don't intercept
    }

    console.log('[DPoP Interceptor] Attack mode active - intercepting authorize redirect');

    // Generate a simple DPoP proof (for demo purposes)
    async function generateDPoPProof(url, method) {
        try {
            // Generate RSA key pair
            const keyPair = await window.crypto.subtle.generateKey(
                {
                    name: "RSASSA-PKCS1-v1_5",
                    modulusLength: 2048,
                    publicExponent: new Uint8Array([1, 0, 1]),
                    hash: "SHA-256"
                },
                true,
                ["sign", "verify"]
            );

            // Export public key as JWK
            const publicKeyJwk = await window.crypto.subtle.exportKey("jwk", keyPair.publicKey);
            
            // Create DPoP proof header
            const header = {
                typ: "dpop+jwt",
                alg: "RS256",
                jwk: {
                    kty: publicKeyJwk.kty,
                    e: publicKeyJwk.e,
                    n: publicKeyJwk.n
                }
            };

            // Create DPoP proof payload
            const payload = {
                htm: method,
                htu: url,
                jti: generateJti(),
                iat: Math.floor(Date.now() / 1000)
            };

            // Encode header and payload
            const encodedHeader = base64UrlEncode(JSON.stringify(header));
            const encodedPayload = base64UrlEncode(JSON.stringify(payload));
            const dataToSign = encodedHeader + '.' + encodedPayload;

            // Sign the data
            const encoder = new TextEncoder();
            const signature = await window.crypto.subtle.sign(
                "RSASSA-PKCS1-v1_5",
                keyPair.privateKey,
                encoder.encode(dataToSign)
            );

            // Create the final JWT
            const encodedSignature = base64UrlEncode(signature);
            const dpopProof = dataToSign + '.' + encodedSignature;

            console.log('[DPoP Interceptor] Generated DPoP proof:', dpopProof.substring(0, 50) + '...');
            
            return dpopProof;
        } catch (error) {
            console.error('[DPoP Interceptor] Failed to generate DPoP proof:', error);
            return null;
        }
    }

    function base64UrlEncode(data) {
        let base64;
        if (typeof data === 'string') {
            base64 = btoa(data);
        } else if (data instanceof ArrayBuffer) {
            base64 = btoa(String.fromCharCode(...new Uint8Array(data)));
        } else {
            base64 = btoa(String.fromCharCode(...data));
        }
        return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
    }

    function generateJti() {
        return 'jti_' + Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
    }

    // Store the DPoP proof generation function globally for the attack script to use
    window.generateDPoPProofForAttack = generateDPoPProof;

    console.log('[DPoP Interceptor] DPoP proof generator ready');
})();
