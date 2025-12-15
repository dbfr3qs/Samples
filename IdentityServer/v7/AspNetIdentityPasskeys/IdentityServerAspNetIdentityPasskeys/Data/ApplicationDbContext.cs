// Copyright (c) Duende Software. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using IdentityServerAspNetIdentityPasskeys.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerAspNetIdentityPasskeys.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<PasskeyChallenge> PasskeyChallenges { get; set; }
    public DbSet<PasskeyCredential> PasskeyCredentials { get; set; }
    public DbSet<DeviceBinding> DeviceBindings { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PasskeyChallenge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.UserId);
        });

        builder.Entity<PasskeyCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CredentialId).IsUnique();
        });

        builder.Entity<DeviceBinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.PublicKeyThumbprint).IsUnique();
            entity.HasIndex(e => e.RefreshTokenHandle);
        });
    }
}
