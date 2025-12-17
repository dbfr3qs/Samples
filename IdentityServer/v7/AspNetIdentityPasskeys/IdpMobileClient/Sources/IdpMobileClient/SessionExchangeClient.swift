import Foundation

@available(iOS 15.0, macOS 12.0, *)
public final class SessionExchangeClient: @unchecked Sendable {
    private let idpBaseURL: String
    
    public init(idpBaseURL: String = "https://idp.dev.internal") {
        self.idpBaseURL = idpBaseURL
    }
    
    public func exchangeForSession(assertion: String) async throws -> SessionExchangeResponse {
        print("🔄 [SessionExchange] Exchanging assertion for session cookies...")
        
        guard let url = URL(string: "\(idpBaseURL)/connect/session-exchange") else {
            throw SessionExchangeError.invalidURL
        }
        
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let requestBody = SessionExchangeRequest(assertion: assertion)
        request.httpBody = try JSONEncoder().encode(requestBody)
        
        let (data, response) = try await URLSession.shared.data(for: request)
        
        guard let httpResponse = response as? HTTPURLResponse else {
            throw SessionExchangeError.invalidResponse
        }
        
        print("📡 [SessionExchange] Response status: \(httpResponse.statusCode)")
        
        guard httpResponse.statusCode == 200 else {
            let errorBody = String(data: data, encoding: .utf8) ?? "Unknown error"
            print("❌ [SessionExchange] Error: \(errorBody)")
            throw SessionExchangeError.httpError(statusCode: httpResponse.statusCode, message: errorBody)
        }
        
        let responseData = try JSONDecoder().decode(SessionExchangeResponseDTO.self, from: data)
        
        print("✅ [SessionExchange] Received \(responseData.cookies.count) cookies")
        
        // Convert cookie data to HTTPCookie objects
        var cookies: [HTTPCookie] = []
        for cookieData in responseData.cookies {
            var properties: [HTTPCookiePropertyKey: Any] = [
                .name: cookieData.name,
                .value: cookieData.value,
                .domain: cookieData.domain,
                .path: cookieData.path,
                .secure: cookieData.secure ? "TRUE" : "FALSE"
            ]
            
            if let expires = cookieData.expires {
                properties[.expires] = expires
            }
            
            if let cookie = HTTPCookie(properties: properties) {
                cookies.append(cookie)
                print("   🍪 \(cookie.name) for \(cookie.domain ?? "unknown")")
            }
        }
        
        return SessionExchangeResponse(
            sessionId: responseData.sessionId,
            cookies: cookies,
            expiresAt: responseData.expiresAt
        )
    }
}

// MARK: - Request/Response Models

struct SessionExchangeRequest: Codable {
    let assertion: String
}

struct SessionExchangeResponseDTO: Codable {
    let sessionId: String
    let cookies: [CookieDataDTO]
    let expiresAt: Date
}

struct CookieDataDTO: Codable {
    let name: String
    let value: String
    let domain: String
    let path: String
    let secure: Bool
    let httpOnly: Bool
    let sameSite: String
    let expires: Date?
}

public struct SessionExchangeResponse {
    public let sessionId: String
    public let cookies: [HTTPCookie]
    public let expiresAt: Date
}

public enum SessionExchangeError: Error {
    case invalidURL
    case invalidResponse
    case httpError(statusCode: Int, message: String)
}
