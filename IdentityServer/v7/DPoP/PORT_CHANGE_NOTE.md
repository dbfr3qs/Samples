# Port Change - Chrome ERR_UNSAFE_PORT Fix

## Issue

Chrome blocks certain ports that it considers unsafe, including port **6666**. When trying to access the AttackerApi on this port, you'll see:

```
ERR_UNSAFE_PORT
```

## Solution

Changed the AttackerApi port from **6666** to **7666**.

## Files Updated

All references to port 6666 have been updated to 7666:

### Backend
- `AttackerApi/Program.cs` - Changed `app.Run()` port

### Frontend
- `AttackerApi/wwwroot/attack.js` - Updated `ATTACKER_API` constant
- `AttackerApi/wwwroot/index.html` - Updated fetch URLs
- `WebClient/Views/Home/AttackDemo.cshtml` - Updated script src and fetch URLs
- `WebClient/Views/Home/AttackVictim.cshtml` - Updated script src and fetch URLs

### Documentation
- `QUICK_START.md` - Updated all port references
- `ATTACK_DEMO_README.md` - Updated all port references
- `start-all.sh` - Updated port in echo statements and script

## Chrome's Unsafe Ports

Chrome blocks these ports by default:
- 6666 (IRC)
- 6667 (IRC)
- And many others...

Port 7666 is safe and not on Chrome's blocklist.

## No Further Action Required

The change is complete and the build succeeds. Simply restart the AttackerApi and it will now run on port 7666.
