import Foundation
import Security

/// Secure storage for PRF output and derived keys using Keychain
@available(iOS 15.0, macOS 12.0, *)
public final class SecureStorage: @unchecked Sendable {
    private let serviceName = "com.idp.mobile.securestorage"
    
    public init() {}
    
    // MARK: - PRF Output Storage
    
    /// Store PRF output with 15-day expiry
    public func storePrfOutput(_ prfOutput: Data, forCredentialId credentialId: Data) throws {
        let key = "prf_output_\(credentialId.base64EncodedString())"
        
        let expiryDate = Date().addingTimeInterval(15 * 24 * 60 * 60) // 15 days
        let expiryData = try JSONEncoder().encode(expiryDate)
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: serviceName,
            kSecAttrAccount as String: key,
            kSecValueData as String: prfOutput,
            kSecAttrGeneric as String: expiryData,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        ]
        
        // Delete existing item if present
        SecItemDelete(query as CFDictionary)
        
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else {
            throw SecureStorageError.storeFailed(status)
        }
        
        print("✅ [SecureStorage] Stored PRF output for credential (expires in 15 days)")
    }
    
    /// Retrieve PRF output if not expired
    public func getPrfOutput(forCredentialId credentialId: Data) throws -> Data? {
        let key = "prf_output_\(credentialId.base64EncodedString())"
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: serviceName,
            kSecAttrAccount as String: key,
            kSecReturnData as String: true,
            kSecReturnAttributes as String: true
        ]
        
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        
        guard status == errSecSuccess,
              let dict = result as? [String: Any],
              let data = dict[kSecValueData as String] as? Data,
              let expiryData = dict[kSecAttrGeneric as String] as? Data else {
            if status == errSecItemNotFound {
                print("ℹ️ [SecureStorage] PRF output not found for credential")
                return nil
            }
            throw SecureStorageError.retrieveFailed(status)
        }
        
        // Check expiry
        let expiryDate = try JSONDecoder().decode(Date.self, from: expiryData)
        if Date() > expiryDate {
            print("⚠️ [SecureStorage] PRF output expired, removing from keychain")
            try deletePrfOutput(forCredentialId: credentialId)
            return nil
        }
        
        print("✅ [SecureStorage] Retrieved PRF output for credential")
        return data
    }
    
    /// Delete PRF output
    public func deletePrfOutput(forCredentialId credentialId: Data) throws {
        let key = "prf_output_\(credentialId.base64EncodedString())"
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: serviceName,
            kSecAttrAccount as String: key
        ]
        
        let status = SecItemDelete(query as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw SecureStorageError.deleteFailed(status)
        }
    }
    
    // MARK: - DPoP Key Storage
    
    /// Store derived DPoP private key
    public func storeDPoPKey(_ keyData: Data, forCredentialId credentialId: Data) throws {
        let key = "dpop_key_\(credentialId.base64EncodedString())"
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: serviceName,
            kSecAttrAccount as String: key,
            kSecValueData as String: keyData,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        ]
        
        // Delete existing item if present
        SecItemDelete(query as CFDictionary)
        
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else {
            throw SecureStorageError.storeFailed(status)
        }
        
        print("✅ [SecureStorage] Stored DPoP key for credential")
    }
    
    /// Retrieve DPoP private key
    public func getDPoPKey(forCredentialId credentialId: Data) throws -> Data? {
        let key = "dpop_key_\(credentialId.base64EncodedString())"
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: serviceName,
            kSecAttrAccount as String: key,
            kSecReturnData as String: true
        ]
        
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        
        guard status == errSecSuccess else {
            if status == errSecItemNotFound {
                return nil
            }
            throw SecureStorageError.retrieveFailed(status)
        }
        
        return result as? Data
    }
    
    /// Delete DPoP key
    public func deleteDPoPKey(forCredentialId credentialId: Data) throws {
        let key = "dpop_key_\(credentialId.base64EncodedString())"
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: serviceName,
            kSecAttrAccount as String: key
        ]
        
        let status = SecItemDelete(query as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw SecureStorageError.deleteFailed(status)
        }
    }
}

// MARK: - Errors

public enum SecureStorageError: Error {
    case storeFailed(OSStatus)
    case retrieveFailed(OSStatus)
    case deleteFailed(OSStatus)
    case encodingFailed
    case decodingFailed
}
