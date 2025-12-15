import Foundation
import CryptoKit
import Security

/// A utility for deterministic P-256 private key derivation from a PRF output,
/// intended for use with Demonstration of Proof-of-Possession (DPoP).
@available(iOS 18.0, macOS 15.0, *)
public struct DPoPKeyDeriver {
    
    /// Derives a seed from the given PRF output using HKDF-SHA256 with a context-specific info.
    ///
    /// - Parameters:
    ///   - prfOutput: The input keying material from a pseudorandom function.
    ///   - rpId: The relying party identifier string.
    ///   - credentialId: The credential identifier data.
    ///   - outputByteCount: The length of the derived seed in bytes. Defaults to 32.
    /// - Returns: A derived seed as `Data` of length `outputByteCount`.
    public static func deriveSeedFromPRF(prfOutput: Data, rpId: String, credentialId: Data, outputByteCount: Int = 32) -> Data {
        let info = Data("webauthn-prf:dpop:v1".utf8) + Data(rpId.utf8) + credentialId
        return HKDF<SHA256>.deriveKey(inputKeyMaterial: SymmetricKey(data: prfOutput),
                                      salt: Data(),
                                      info: info,
                                      outputByteCount: outputByteCount).withUnsafeBytes { Data($0) }
    }
    
    /// Creates a CryptoKit P256 private key from a 32-byte seed via SecKey.
    ///
    /// - Parameter seed: A 32-byte data seed representing the raw private scalar.
    /// - Throws: `DPoPKeyDeriverError.invalidSeedLength` if seed length is not 32 bytes,
    ///           or `DPoPKeyDeriverError.secKeyCreationFailed` if key creation fails.
    /// - Returns: A `P256.Signing.PrivateKey` corresponding to the given seed.
    public static func makeP256PrivateKey(fromSeed seed: Data) throws -> P256.Signing.PrivateKey {
        guard seed.count == 32 else {
            throw DPoPKeyDeriverError.invalidSeedLength
        }
        
        return try P256.Signing.PrivateKey(x963Representation: seed)
    }
    
    /// Convenience method that derives a deterministic P256 private key for DPoP from PRF output.
    ///
    /// - Parameters:
    ///   - prfOutput: The input keying material from a pseudorandom function.
    ///   - rpId: The relying party identifier string.
    ///   - credentialId: The credential identifier data.
    /// - Throws: Errors thrown by `makeP256PrivateKey`.
    /// - Returns: A deterministic P256 signing private key suitable for DPoP.
    public static func deriveDPoPPrivateKey(prfOutput: Data, rpId: String, credentialId: Data) throws -> P256.Signing.PrivateKey {
        let seed = deriveSeedFromPRF(prfOutput: prfOutput, rpId: rpId, credentialId: credentialId)
        return try makeP256PrivateKey(fromSeed: seed)
    }
    
    
    /// Errors thrown by DPoPKeyDeriver
    public enum DPoPKeyDeriverError: Error {
        /// Seed length is invalid (not 32 bytes).
        case invalidSeedLength
        /// Failed to create SecKey from seed data.
        case secKeyCreationFailed
    }
}
