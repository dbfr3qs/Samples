import Foundation
import CryptoKit

@available(iOS 15.0, macOS 12.0, *)
public final class SessionAssertionGenerator: @unchecked Sendable {
    
    public init() {}
    
    public func generateSessionAssertion(
        sessionId: String,
        userId: String,
        privateKey: P256.Signing.PrivateKey,
        jwk: DPoPJWK,
        idpUrl: String
    ) throws -> String {
        print("🔐 [SessionAssertion] Generating session assertion...")
        print("   Session ID (sid): \(sessionId)")
        print("   User ID (sub): \(userId)")
        print("   Audience (aud): \(idpUrl)")
        
        let header: [String: Any] = [
            "typ": "dpop+jwt",
            "alg": "ES256",
            "jwk": [
                "kty": jwk.kty,
                "crv": jwk.crv,
                "x": jwk.x,
                "y": jwk.y
            ]
        ]
        
        let now = Int(Date().timeIntervalSince1970)
        let jti = UUID().uuidString
        
        let payload: [String: Any] = [
            "iss": "mobile-client",
            "sub": userId,
            "aud": idpUrl,
            "sid": sessionId,
            "jti": jti,
            "iat": now,
            "exp": now + 60,
            "htm": "POST",
            "htu": "\(idpUrl)/connect/session-exchange"
        ]
        
        print("   JTI: \(jti)")
        print("   Expires: \(now + 60)")
        
        let headerData = try JSONSerialization.data(withJSONObject: header)
        let payloadData = try JSONSerialization.data(withJSONObject: payload)
        
        let headerB64 = headerData.base64URLEncodedString()
        let payloadB64 = payloadData.base64URLEncodedString()
        
        let signingInput = "\(headerB64).\(payloadB64)"
        let signingData = Data(signingInput.utf8)
        
        let signature = try privateKey.signature(for: signingData)
        let signatureB64 = signature.rawRepresentation.base64URLEncodedString()
        
        let jwt = "\(signingInput).\(signatureB64)"
        
        print("✅ [SessionAssertion] Generated assertion (length: \(jwt.count))")
        
        return jwt
    }
}
