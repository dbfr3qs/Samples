import Foundation

/// Client for making authenticated API requests
@available(iOS 15.0, macOS 12.0, *)
public final class ApiClient: @unchecked Sendable {
    private let apiBaseURL: String
    private let oauthClient: OAuthClient
    
    public init(
        apiBaseURL: String = "https://api.dev.internal:5002",
        oauthClient: OAuthClient
    ) {
        self.apiBaseURL = apiBaseURL
        self.oauthClient = oauthClient
    }
    
    /// Make an authenticated GET request to the API
    public func get(path: String) async throws -> Data {
        print("🌐 [ApiClient] Getting valid access token...")
        let accessToken = try await oauthClient.getValidAccessToken()
        print("✅ [ApiClient] Got access token: \(accessToken.prefix(20))...")
        
        guard let url = URL(string: "\(apiBaseURL)\(path)") else {
            print("❌ [ApiClient] Invalid URL: \(apiBaseURL)\(path)")
            throw ApiError.invalidURL
        }
        
        print("🌐 [ApiClient] Making GET request to: \(url)")
        
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            
            guard let httpResponse = response as? HTTPURLResponse else {
                print("❌ [ApiClient] Invalid response type")
                throw ApiError.invalidResponse
            }
            
            print("📡 [ApiClient] Response status: \(httpResponse.statusCode)")
            
            guard (200...299).contains(httpResponse.statusCode) else {
                print("❌ [ApiClient] HTTP error: \(httpResponse.statusCode)")
                let responseBody = String(data: data, encoding: .utf8) ?? "Unable to decode"
                print("📄 [ApiClient] Response body: \(responseBody)")
                throw ApiError.httpError(statusCode: httpResponse.statusCode)
            }
            
            let responseBody = String(data: data, encoding: .utf8) ?? "Unable to decode"
            print("✅ [ApiClient] Success! Response: \(responseBody.prefix(200))...")
            
            return data
        } catch let error as URLError {
            print("❌ [ApiClient] URLError: \(error.localizedDescription)")
            print("❌ [ApiClient] URLError code: \(error.code.rawValue)")
            print("❌ [ApiClient] URLError domain: \(error.errorCode)")
            throw error
        } catch {
            print("❌ [ApiClient] Unexpected error: \(error)")
            throw error
        }
    }
    
    /// Make an authenticated POST request to the API
    public func post(path: String, body: Data? = nil) async throws -> Data {
        let accessToken = try await oauthClient.getValidAccessToken()
        
        guard let url = URL(string: "\(apiBaseURL)\(path)") else {
            throw ApiError.invalidURL
        }
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        
        if let body = body {
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.httpBody = body
        }
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }
        
        guard (200...299).contains(httpResponse.statusCode) else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
        
        return data
    }
    
    /// Convenience method to call a test endpoint
    public func callTestEndpoint() async throws -> String {
        let data = try await get(path: "/claims")
        let rawString = String(data: data, encoding: .utf8) ?? ""
        
        // Try to pretty print JSON if the response is JSON
        if let jsonData = rawString.data(using: .utf8),
           let jsonObject = try? JSONSerialization.jsonObject(with: jsonData),
           let prettyData = try? JSONSerialization.data(withJSONObject: jsonObject, options: [.prettyPrinted, .sortedKeys]),
           let prettyString = String(data: prettyData, encoding: .utf8) {
            print("📄 [ApiClient] Pretty printed JSON response:")
            print(prettyString)
            return prettyString
        }
        
        // If not JSON, return as-is
        return rawString
    }
}

public enum ApiError: Error {
    case invalidURL
    case invalidResponse
    case httpError(statusCode: Int)
    case decodingError
}
