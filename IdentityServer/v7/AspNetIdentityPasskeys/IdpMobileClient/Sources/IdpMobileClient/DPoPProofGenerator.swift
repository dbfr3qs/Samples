import Foundation
import CryptoKit

/// Generates RFC 9449 compliant DPoP proofs
@available(iOS 15.0, macOS 12.0, *)
public final class DPoPProofGenerator: @unchecked Sendable {
    
    public init() {}
    
    // MARK: - Proof Generation
    
    /// Generate DPoP proof for token endpoint
    public func generateProof(
        privateKey: P256.Signing.PrivateKey,
        jwk: DPoPJWK,
        httpMethod: String,
        httpUri: String,
        accessToken: String? = nil
    ) throws -> String {
        print("🔐 [DPoPProof] Generating proof for \(httpMethod) \(httpUri)")
        
        // Generate unique jti (UUID)
        let jti = UUID().uuidString
        
        // Current timestamp
        let iat = Int(Date().timeIntervalSince1970)
        
        // Build header
        let header = DPoPHeader(
            typ: "dpop+jwt",
            alg: "ES256",
            jwk: jwk
        )
        
        // Build payload
        var payload = DPoPPayload(
            jti: jti,
            htm: httpMethod,
            htu: httpUri,
            iat: iat
        )
        
        // Add ath (access token hash) if present
        if let accessToken = accessToken {
            payload.ath = try calculateAccessTokenHash(accessToken)
        }
        
        // Encode header and payload
        let headerData = try JSONEncoder().encode(header)
        let payloadData = try JSONEncoder().encode(payload)
        
        let headerB64 = headerData.base64URLEncodedString()
        let payloadB64 = payloadData.base64URLEncodedString()
        
        // Create signing input
        let signingInput = "\(headerB64).\(payloadB64)"
        guard let signingData = signingInput.data(using: .utf8) else {
            throw DPoPProofError.encodingFailed
        }
        
        // Sign with ES256 (ECDSA using P-256 and SHA-256)
        let signature = try privateKey.signature(for: signingData)
        
        // Convert DER signature to raw format (r || s)
        let rawSignature = try convertDERToRaw(signature.derRepresentation)
        
        // Build JWT
        let signatureB64 = rawSignature.base64URLEncodedString()
        let jwt = "\(signingInput).\(signatureB64)"
        
        print("✅ [DPoPProof] Generated proof with jti: \(jti.prefix(20))...")
        
        return jwt
    }
    
    // MARK: - Helpers
    
    /// Calculate SHA-256 hash of access token for ath claim
    private func calculateAccessTokenHash(_ accessToken: String) throws -> String {
        guard let tokenData = accessToken.data(using: .ascii) else {
            throw DPoPProofError.encodingFailed
        }
        
        let hash = SHA256.hash(data: tokenData)
        return Data(hash).base64URLEncodedString()
    }
    
    /// Convert DER-encoded ECDSA signature to raw format (r || s)
    /// DER format: 0x30 [total-length] 0x02 [r-length] [r] 0x02 [s-length] [s]
    /// Raw format: [r (32 bytes)] || [s (32 bytes)]
    private func convertDERToRaw(_ der: Data) throws -> Data {
        var index = 0
        let bytes = [UInt8](der)
        
        // Check SEQUENCE tag
        guard bytes[index] == 0x30 else {
            throw DPoPProofError.signatureConversionFailed
        }
        index += 1
        
        // Skip total length
        index += 1
        
        // Parse r
        guard bytes[index] == 0x02 else {
            throw DPoPProofError.signatureConversionFailed
        }
        index += 1
        
        let rLength = Int(bytes[index])
        index += 1
        
        var r = Data(bytes[index..<(index + rLength)])
        index += rLength
        
        // Remove leading zero if present (for positive numbers)
        if r.count == 33 && r[0] == 0x00 {
            r = r.dropFirst()
        }
        
        // Pad to 32 bytes if needed
        while r.count < 32 {
            r.insert(0x00, at: 0)
        }
        
        // Parse s
        guard bytes[index] == 0x02 else {
            throw DPoPProofError.signatureConversionFailed
        }
        index += 1
        
        let sLength = Int(bytes[index])
        index += 1
        
        var s = Data(bytes[index..<(index + sLength)])
        
        // Remove leading zero if present
        if s.count == 33 && s[0] == 0x00 {
            s = s.dropFirst()
        }
        
        // Pad to 32 bytes if needed
        while s.count < 32 {
            s.insert(0x00, at: 0)
        }
        
        // Concatenate r and s
        return r + s
    }
}

// MARK: - Models

private struct DPoPHeader: Codable {
    let typ: String
    let alg: String
    let jwk: DPoPJWK
}

private struct DPoPPayload: Codable {
    let jti: String
    let htm: String
    let htu: String
    let iat: Int
    var ath: String?
}

// MARK: - Errors

public enum DPoPProofError: Error {
    case encodingFailed
    case signatureConversionFailed
    case invalidSignature
}
