import Foundation
import WebKit

/// WebView that automatically injects id_token_hint for seamless SSO
@available(iOS 15.0, *)
public final class AuthenticatedWebView: WKWebView {
    private var idTokenHint: String?
    private let idpBaseURL: String
    
    public init(
        frame: CGRect = .zero,
        configuration: WKWebViewConfiguration = WKWebViewConfiguration(),
        idpBaseURL: String = "https://idp.dev.internal"
    ) {
        self.idpBaseURL = idpBaseURL
        super.init(frame: frame, configuration: configuration)
        self.navigationDelegate = self
    }
    
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
    
    /// Set the ID token for SSO
    public func setIdToken(_ idToken: String) {
        self.idTokenHint = idToken
        print("✅ [AuthenticatedWebView] ID token set for SSO")
    }
    
    /// Clear the ID token
    public func clearIdToken() {
        self.idTokenHint = nil
        print("ℹ️ [AuthenticatedWebView] ID token cleared")
    }
}

// MARK: - WKNavigationDelegate

@available(iOS 15.0, *)
extension AuthenticatedWebView: WKNavigationDelegate {
    public func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        
        guard let url = navigationAction.request.url else {
            decisionHandler(.allow)
            return
        }
        
        // Check if this is a navigation to the IdP authorize endpoint
        if url.absoluteString.starts(with: "\(idpBaseURL)/connect/authorize") {
            print("🔍 [AuthenticatedWebView] Detected navigation to authorize endpoint")
            
            // If we have an ID token, inject it into the URL
            if let idTokenHint = idTokenHint {
                if let modifiedURL = injectIdTokenHint(url: url, idToken: idTokenHint) {
                    print("✅ [AuthenticatedWebView] Injected id_token_hint and prompt=none")
                    
                    // Create new request with modified URL
                    var newRequest = navigationAction.request
                    newRequest.url = modifiedURL
                    
                    // Cancel current navigation and load modified request
                    decisionHandler(.cancel)
                    webView.load(newRequest)
                    return
                }
            } else {
                print("ℹ️ [AuthenticatedWebView] No ID token available for SSO")
            }
        }
        
        decisionHandler(.allow)
    }
    
    private func injectIdTokenHint(url: URL, idToken: String) -> URL? {
        guard var components = URLComponents(url: url, resolvingAgainstBaseURL: false) else {
            return nil
        }
        
        var queryItems = components.queryItems ?? []
        
        // Add id_token_hint
        queryItems.append(URLQueryItem(name: "id_token_hint", value: idToken))
        
        // Add prompt=none for silent authentication
        queryItems.append(URLQueryItem(name: "prompt", value: "none"))
        
        components.queryItems = queryItems
        
        return components.url
    }
    
    public func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        print("❌ [AuthenticatedWebView] Navigation failed: \(error.localizedDescription)")
        
        // Check if this is a login_required error (expected when no session exists)
        if let url = webView.url, url.absoluteString.contains("error=login_required") {
            print("ℹ️ [AuthenticatedWebView] Silent authentication failed - no active session")
            // Application should handle this by showing login UI
        }
    }
}
