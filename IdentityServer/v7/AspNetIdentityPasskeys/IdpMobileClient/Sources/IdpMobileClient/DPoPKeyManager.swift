import Foundation
import CryptoKit

/// Manages DPoP key derivation from PRF output using HKDF
@available(iOS 15.0, macOS 12.0, *)
public final class DPoPKeyManager: @unchecked Sendable {
    private let secureStorage: SecureStorage
    
    public init(secureStorage: SecureStorage = SecureStorage()) {
        self.secureStorage = secureStorage
    }
    
    // MARK: - Key Derivation
    
    /// Derive P-256 private key from PRF output using HKDF
    /// Returns the private key and its JWK representation
    public func deriveKeyFromPrf(prfOutput: Data, credentialId: Data) throws -> (privateKey: P256.Signing.PrivateKey, jwk: DPoPJWK, thumbprint: String) {
        print("🔑 [DPoPKeyManager] Deriving P-256 key from PRF output (length: \(prfOutput.count))")
        
        // Use HKDF to derive 32 bytes for P-256 private key
        let salt = "dpop-key-derivation".data(using: .utf8)!
        let info = "p256-signing-key".data(using: .utf8)!
        
        let derivedKey = HKDF<SHA256>.deriveKey(
            inputKeyMaterial: SymmetricKey(data: prfOutput),
            salt: salt,
            info: info,
            outputByteCount: 32
        )
        
        // Convert to raw bytes
        var keyBytes = [UInt8](repeating: 0, count: 32)
        derivedKey.withUnsafeBytes { bytes in
            keyBytes = Array(bytes)
        }
        
        // Create P-256 private key from raw bytes
        let privateKey = try P256.Signing.PrivateKey(rawRepresentation: Data(keyBytes))
        
        // Generate JWK representation (public key only for DPoP proofs)
        let jwk = try generateJWK(from: privateKey.publicKey)
        
        // Calculate JWK thumbprint per RFC 7638
        let thumbprint = try calculateThumbprint(jwk: jwk)
        
        // Store the derived key for reuse
        let keyData = privateKey.rawRepresentation
        try secureStorage.storeDPoPKey(keyData, forCredentialId: credentialId)
        
        print("✅ [DPoPKeyManager] Key derived successfully, thumbprint: \(thumbprint.prefix(20))...")
        
        return (privateKey, jwk, thumbprint)
    }
    
    /// Retrieve cached DPoP key
    public func getCachedKey(forCredentialId credentialId: Data) throws -> P256.Signing.PrivateKey? {
        guard let keyData = try secureStorage.getDPoPKey(forCredentialId: credentialId) else {
            return nil
        }
        
        return try P256.Signing.PrivateKey(rawRepresentation: keyData)
    }
    
    /// Get or derive DPoP key (checks cache first, then derives from PRF if needed)
    public func getOrDeriveKey(prfOutput: Data?, credentialId: Data) throws -> (privateKey: P256.Signing.PrivateKey, jwk: DPoPJWK, thumbprint: String) {
        // Try to get cached key first
        if let cachedKey = try getCachedKey(forCredentialId: credentialId) {
            print("✅ [DPoPKeyManager] Using cached DPoP key")
            let jwk = try generateJWK(from: cachedKey.publicKey)
            let thumbprint = try calculateThumbprint(jwk: jwk)
            return (cachedKey, jwk, thumbprint)
        }
        
        // No cached key, need PRF output to derive
        guard let prfOutput = prfOutput else {
            throw DPoPKeyError.noPrfOutput
        }
        
        return try deriveKeyFromPrf(prfOutput: prfOutput, credentialId: credentialId)
    }
    
    // MARK: - JWK Generation
    
    /// Generate JWK representation from P-256 public key
    private func generateJWK(from publicKey: P256.Signing.PublicKey) throws -> DPoPJWK {
        let rawRepresentation = publicKey.rawRepresentation
        
        // P-256 uncompressed point is 65 bytes: 0x04 || x (32 bytes) || y (32 bytes)
        guard rawRepresentation.count == 65, rawRepresentation[0] == 0x04 else {
            throw DPoPKeyError.invalidPublicKey
        }
        
        let x = rawRepresentation[1..<33]
        let y = rawRepresentation[33..<65]
        
        return DPoPJWK(
            kty: "EC",
            crv: "P-256",
            x: Data(x).base64URLEncodedString(),
            y: Data(y).base64URLEncodedString()
        )
    }
    
    /// Calculate JWK thumbprint per RFC 7638
    private func calculateThumbprint(jwk: DPoPJWK) throws -> String {
        // Create canonical JSON representation
        let canonicalJson = """
        {"crv":"\(jwk.crv)","kty":"\(jwk.kty)","x":"\(jwk.x)","y":"\(jwk.y)"}
        """
        
        guard let jsonData = canonicalJson.data(using: .utf8) else {
            throw DPoPKeyError.thumbprintFailed
        }
        
        // SHA-256 hash
        let hash = SHA256.hash(data: jsonData)
        
        // Base64URL encode
        return Data(hash).base64URLEncodedString()
    }
}

// MARK: - Models

public struct DPoPJWK: Codable, Sendable {
    public let kty: String
    public let crv: String
    public let x: String
    public let y: String
    
    public init(kty: String, crv: String, x: String, y: String) {
        self.kty = kty
        self.crv = crv
        self.x = x
        self.y = y
    }
}

// MARK: - Errors

public enum DPoPKeyError: Error {
    case noPrfOutput
    case invalidPublicKey
    case thumbprintFailed
    case keyDerivationFailed
}
