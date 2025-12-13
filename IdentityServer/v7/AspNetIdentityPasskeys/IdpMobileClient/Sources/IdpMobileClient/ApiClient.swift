import Foundation

/// Client for making authenticated API requests
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
        let accessToken = try await oauthClient.getValidAccessToken()
        
        guard let url = URL(string: "\(apiBaseURL)\(path)") else {
            throw ApiError.invalidURL
        }
        
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw ApiError.invalidResponse
        }
        
        guard (200...299).contains(httpResponse.statusCode) else {
            throw ApiError.httpError(statusCode: httpResponse.statusCode)
        }
        
        return data
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
        let data = try await get(path: "/test")
        return String(data: data, encoding: .utf8) ?? ""
    }
}

public enum ApiError: Error {
    case invalidURL
    case invalidResponse
    case httpError(statusCode: Int)
    case decodingError
}
