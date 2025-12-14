namespace IdentityServerAspNetIdentityPasskeys.Services;

public class NativeOriginValidator
{
    private readonly HashSet<string> _allowedBundleIds = new()
    {
        "com.idp.mobile",
        "com.idp.mobiledemo"
    };
    
    private readonly HashSet<string> _allowedWebOrigins = new()
    {
        "https://idp.dev.internal",
        "https://idp.dev.internal:5001",
        "https://localhost:5001",
        "https://localhost"
    };
    
    public bool IsOriginValid(string origin)
    {
        Console.WriteLine($"[DEBUG] Validating origin: {origin}");
        
        if (origin.StartsWith("https://"))
        {
            var isValid = _allowedWebOrigins.Contains(origin);
            Console.WriteLine($"[DEBUG] Web origin valid: {isValid}");
            return isValid;
        }
        
        if (origin.StartsWith("ios:bundle-id://"))
        {
            var bundleId = origin.Substring("ios:bundle-id://".Length);
            var isValid = _allowedBundleIds.Contains(bundleId);
            Console.WriteLine($"[DEBUG] iOS bundle ID '{bundleId}' valid: {isValid}");
            return isValid;
        }
        
        if (origin.StartsWith("android:apk-key-hash:"))
        {
            Console.WriteLine($"[DEBUG] Android origin not supported yet");
            return false;
        }
        
        Console.WriteLine($"[DEBUG] Unknown origin format");
        return false;
    }
    
    public void AddAllowedBundleId(string bundleId)
    {
        _allowedBundleIds.Add(bundleId);
    }
    
    public void AddAllowedWebOrigin(string origin)
    {
        _allowedWebOrigins.Add(origin);
    }
}
