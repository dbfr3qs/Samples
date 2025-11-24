// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerAspNetIdentityPasskeys.Pages.Account;

[SecurityHeaders]
[AllowAnonymous]
public class PrfDemoModel : PageModel
{
    private readonly IAntiforgery _antiforgery;

    public string? PrfOutput { get; set; }
    public bool PrfEnabled { get; set; }
    public string? ErrorMessage { get; set; }
    public string AntiforgeryToken { get; set; } = string.Empty;

    public PrfDemoModel(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public void OnGet(string? prfOutput, bool prfEnabled = false)
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        AntiforgeryToken = tokens.RequestToken ?? string.Empty;

        // Handle PRF output from query parameters (passed after WebAuthn ceremony)
        if (prfEnabled && !string.IsNullOrEmpty(prfOutput))
        {
            PrfOutput = prfOutput;
            PrfEnabled = true;
        }
        else if (prfEnabled)
        {
            ErrorMessage = "PRF was enabled but no output was received.";
        }
    }
}
