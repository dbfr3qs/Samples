using IdentityServerAspNetIdentityPasskeys.Data;
using IdentityServerAspNetIdentityPasskeys.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerAspNetIdentityPasskeys.Services;

public interface IDeviceBindingStore
{
    Task<DeviceBinding> CreateAsync(DeviceBinding binding);
    Task<DeviceBinding?> GetByThumbprintAsync(string thumbprint);
    Task<DeviceBinding?> GetByRefreshTokenAsync(string refreshTokenHandle);
    Task<List<DeviceBinding>> GetByUserIdAsync(string userId);
    Task UpdateAsync(DeviceBinding binding);
    Task DeleteAsync(int id);
}

public class DeviceBindingStore : IDeviceBindingStore
{
    private readonly ApplicationDbContext _context;

    public DeviceBindingStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceBinding> CreateAsync(DeviceBinding binding)
    {
        _context.DeviceBindings.Add(binding);
        await _context.SaveChangesAsync();
        return binding;
    }

    public async Task<DeviceBinding?> GetByThumbprintAsync(string thumbprint)
    {
        return await _context.DeviceBindings
            .FirstOrDefaultAsync(b => b.PublicKeyThumbprint == thumbprint);
    }

    public async Task<DeviceBinding?> GetByRefreshTokenAsync(string refreshTokenHandle)
    {
        return await _context.DeviceBindings
            .FirstOrDefaultAsync(b => b.RefreshTokenHandle == refreshTokenHandle);
    }

    public async Task<List<DeviceBinding>> GetByUserIdAsync(string userId)
    {
        return await _context.DeviceBindings
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.LastUsedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(DeviceBinding binding)
    {
        _context.DeviceBindings.Update(binding);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var binding = await _context.DeviceBindings.FindAsync(id);
        if (binding != null)
        {
            _context.DeviceBindings.Remove(binding);
            await _context.SaveChangesAsync();
        }
    }
}
