namespace IdentityServerAspNetIdentityPasskeys.Models;

public class SessionExchangeRequest
{
    public string Assertion { get; set; } = null!;
}

public class SessionExchangeResponse
{
    public string SessionId { get; set; } = null!;
    public List<CookieData> Cookies { get; set; } = new();
    public double ExpiresAt { get; set; } // Unix timestamp for iOS compatibility
}

public class CookieData
{
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string Domain { get; set; } = null!;
    public string Path { get; set; } = "/";
    public bool Secure { get; set; }
    public bool HttpOnly { get; set; }
    public string SameSite { get; set; } = "Lax";
    public double? Expires { get; set; } // Unix timestamp for iOS compatibility
}
