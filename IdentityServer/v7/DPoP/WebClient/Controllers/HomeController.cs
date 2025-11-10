// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebClient.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [AllowAnonymous]
    public IActionResult Index() => View();

    public IActionResult Secure() => View();

    public async Task<IActionResult> Renew()
    {
        await HttpContext.GetUserAccessTokenAsync(new UserTokenRequestParameters { ForceTokenRenewal = true });
        return RedirectToAction(nameof(Secure));
    }

    public IActionResult Logout() => SignOut("oidc");

    public async Task<IActionResult> CallApi()
    {
        var client = _httpClientFactory.CreateClient("client");

        var response = await client.GetStringAsync("identity");
        ViewBag.Json = response.PrettyPrintJson();

        return View();
    }

    [AllowAnonymous]
    public IActionResult AttackDemo() => View();

    [AllowAnonymous]
    public IActionResult AttackVictim() => View();

    [AllowAnonymous]
    public async Task<IActionResult> ExchangeStolenCode(string code, string codeVerifier, string state)
    {
        // This endpoint manually exchanges the stolen authorization code for tokens
        // using the stolen code_verifier and the attacker's DPoP key
        
        var client = _httpClientFactory.CreateClient("dpop_client");
        
        var tokenRequest = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", "https://localhost:5010/signin-oidc" },
            { "client_id", "dpop_public" }, // Use public client - no secret needed!
            { "code_verifier", codeVerifier }
        };
        
        var response = await client.PostAsync("https://localhost:5001/connect/token", 
            new FormUrlEncodedContent(tokenRequest));
        
        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = await response.Content.ReadAsStringAsync();
            ViewBag.Success = true;
            ViewBag.TokenResponse = tokenResponse;
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            ViewBag.Success = false;
            ViewBag.Error = error;
        }
        
        return View("AttackSuccess");
    }
}
