import Foundation
import Security

/// Secure storage for OAuth tokens using iOS Keychain
public final class TokenStorage: @unchecked Sendable {
    private let service = "com.idp.mobile.client"
    
    public init() {}
    
    // MARK: - Token Storage
    
    public func saveAccessToken(_ token: String) throws {
        try saveToKeychain(key: "access_token", value: token)
    }
    
    public func getAccessToken() -> String? {
        return getFromKeychain(key: "access_token")
    }
    
    public func saveRefreshToken(_ token: String) throws {
        try saveToKeychain(key: "refresh_token", value: token)
    }
    
    public func getRefreshToken() -> String? {
        return getFromKeychain(key: "refresh_token")
    }
    
    public func saveIdToken(_ token: String) throws {
        try saveToKeychain(key: "id_token", value: token)
    }
    
    public func getIdToken() -> String? {
        return getFromKeychain(key: "id_token")
    }
    
    public func saveTokenExpiry(_ expiry: Date) throws {
        let timestamp = expiry.timeIntervalSince1970
        try saveToKeychain(key: "token_expiry", value: String(timestamp))
    }
    
    public func getTokenExpiry() -> Date? {
        guard let timestampString = getFromKeychain(key: "token_expiry"),
              let timestamp = TimeInterval(timestampString) else {
            return nil
        }
        return Date(timeIntervalSince1970: timestamp)
    }
    
    public func isAccessTokenValid() -> Bool {
        guard let expiry = getTokenExpiry() else {
            return false
        }
        // Consider token invalid if it expires within 60 seconds
        return expiry.timeIntervalSinceNow > 60
    }
    
    public func clearTokens() {
        deleteFromKeychain(key: "access_token")
        deleteFromKeychain(key: "refresh_token")
        deleteFromKeychain(key: "id_token")
        deleteFromKeychain(key: "token_expiry")
    }
    
    // MARK: - Keychain Operations
    
    private func saveToKeychain(key: String, value: String) throws {
        guard let data = value.data(using: .utf8) else {
            throw TokenStorageError.encodingFailed
        }
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key,
            kSecValueData as String: data
        ]
        
        // Delete existing item if present
        SecItemDelete(query as CFDictionary)
        
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else {
            throw TokenStorageError.keychainError(status)
        }
    }
    
    private func getFromKeychain(key: String) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]
        
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        
        guard status == errSecSuccess,
              let data = result as? Data,
              let value = String(data: data, encoding: .utf8) else {
            return nil
        }
        
        return value
    }
    
    private func deleteFromKeychain(key: String) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: key
        ]
        
        SecItemDelete(query as CFDictionary)
    }
}

public enum TokenStorageError: Error {
    case encodingFailed
    case keychainError(OSStatus)
}
