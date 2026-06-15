using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.Totp.Services;
public sealed class TotpService
{
    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<Guid, TotpRecord> _records;
    public TotpService(IApplicationPaths paths)
    {
        var dir = Path.Combine(paths.DataPath, "totp"); Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "users.json");
        _records = File.Exists(_path) ? JsonSerializer.Deserialize<Dictionary<Guid, TotpRecord>>(File.ReadAllText(_path)) ?? new() : new();
    }
    public TotpRecord? Get(Guid userId) { lock (_gate) return _records.TryGetValue(userId, out var r) ? r : null; }
    public string BeginSetup(Guid userId, string username)
    {
        var secret = Base32(RandomNumberGenerator.GetBytes(20));
        lock (_gate) { _records[userId] = new TotpRecord(secret, false, DateTimeOffset.UtcNow); Save(); }
        return secret;
    }
    public bool Confirm(Guid userId, string code)
    {
        lock (_gate) { if (!_records.TryGetValue(userId, out var r) || !Verify(r.Secret, code)) return false; _records[userId] = r with { Enabled = true }; Save(); return true; }
    }
    public void Reset(Guid userId) { lock (_gate) { _records.Remove(userId); Save(); } }
    public bool IsEnabled(Guid userId) => Get(userId)?.Enabled == true;
    public bool VerifyUser(Guid userId, string? code) => code is not null && Get(userId) is { Enabled: true } r && Verify(r.Secret, code);
    public static string OtpAuthUri(string issuer, string username, string secret) => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(username)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
    private static bool Verify(string secret, string code)
    {
        code = new string(code.Where(char.IsDigit).ToArray()); if (code.Length != 6) return false;
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (var i = -1; i <= 1; i++) if (Code(secret, counter + i) == code) return true;
        return false;
    }
    private static string Code(string secret, long counter)
    {
        Span<byte> msg = stackalloc byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(msg, counter);
        using var hmac = new HMACSHA1(FromBase32(secret)); var hash = hmac.ComputeHash(msg.ToArray()); var offset = hash[^1] & 0xf;
        var bin = ((hash[offset] & 0x7f) << 24) | ((hash[offset + 1] & 0xff) << 16) | ((hash[offset + 2] & 0xff) << 8) | (hash[offset + 3] & 0xff);
        return (bin % 1_000_000).ToString("D6");
    }
    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true }));
    private static string Base32(byte[] bytes) { const string a = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var bits = 0; var val = 0; var sb = new StringBuilder(); foreach (var b in bytes) { val = (val << 8) | b; bits += 8; while (bits >= 5) { sb.Append(a[(val >> (bits - 5)) & 31]); bits -= 5; } } if (bits > 0) sb.Append(a[(val << (5 - bits)) & 31]); return sb.ToString(); }
    private static byte[] FromBase32(string s) { const string a = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var bits = 0; var val = 0; var list = new List<byte>(); foreach (var c in s.TrimEnd('=').ToUpperInvariant()) { var idx = a.IndexOf(c); if (idx < 0) continue; val = (val << 5) | idx; bits += 5; if (bits >= 8) { list.Add((byte)((val >> (bits - 8)) & 255)); bits -= 8; } } return list.ToArray(); }
}
public sealed record TotpRecord(string Secret, bool Enabled, DateTimeOffset CreatedUtc);
