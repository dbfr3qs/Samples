// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityServerAspNetIdentityPasskeys.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerAspNetIdentityPasskeys.Passkeys;

public static class PasskeyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Converts a byte array to base64url encoding (RFC 4648 Section 5)
    /// </summary>
    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static IEndpointConventionBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var accountGroup = endpoints.MapGroup("/Identity/Account").ExcludeFromDescription();

        accountGroup.MapPost("/PasskeyCreationOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
            }

            var userId = await userManager.GetUserIdAsync(user);
            var userName = await userManager.GetUserNameAsync(user) ?? "User";
            var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
            {
                Id = userId,
                Name = userName,
                DisplayName = userName
            });
            
            // Add PRF (Pseudo-Random Function) extension to the creation options
            var options = JsonNode.Parse(optionsJson);
            if (options != null)
            {
                var extensions = options["extensions"] ?? new JsonObject();
                extensions["prf"] = new JsonObject
                {
                    ["eval"] = new JsonObject
                    {
                        ["first"] = ToBase64Url(System.Text.Encoding.UTF8.GetBytes("first-salt"))
                    }
                };
                options["extensions"] = extensions;
                optionsJson = options.ToJsonString();
            }
            
            return TypedResults.Content(optionsJson, contentType: "application/json");
        });

        accountGroup.MapPost("/PasskeyRequestOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromQuery] string? username) =>
        {
            var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
            var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
            
            // Add PRF (Pseudo-Random Function) extension to the request options
            var options = JsonNode.Parse(optionsJson);
            if (options != null)
            {
                var extensions = options["extensions"] ?? new JsonObject();
                extensions["prf"] = new JsonObject
                {
                    ["eval"] = new JsonObject
                    {
                        ["first"] = ToBase64Url(System.Text.Encoding.UTF8.GetBytes("first-salt"))
                    }
                };
                options["extensions"] = extensions;
                optionsJson = options.ToJsonString();
            }
            
            return TypedResults.Content(optionsJson, contentType: "application/json");
        });

        return accountGroup;
    }
}
