using System.Text.Json;
using System.Text.Json.Serialization;
using Aula.Core.Abstractions;
using Aula.Core.Logging;
using Aula.Core.Services;
using Microsoft.Extensions.Logging;

namespace Aula.Core.Models;

public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILogger<ProfileService> _log;

    public ProfileService(string? directory = null)
    {
        _path = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aula");
        _log = AulaLogging.Logger<ProfileService>();
    }

    public string DirectoryPath => _path;

    public string BuildPath(string name) => Path.Combine(_path, Sanitize(name) + ".json");

    public KeyboardProfile Save(string name, KeyboardProfile profile)
    {
        Directory.CreateDirectory(_path);
        string path = BuildPath(name);
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
        _log.LogInformation("Saved profile '{Name}' to {Path}", name, path);
        return profile;
    }

    public KeyboardProfile? Load(string name)
    {
        string path = BuildPath(name);
        if (File.Exists(path))
        {
            _log.LogDebug("Loading profile '{Name}' from {Path}", name, path);
            return LoadFromPath(path);
        }

        _log.LogDebug("Profile '{Name}' not found at {Path}", name, path);
        return null;
    }

    public KeyboardProfile LoadFromPath(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<KeyboardProfile>(json, JsonOptions)
            ?? throw new AulaException($"Profile '{path}' is empty or invalid.");
    }

    public void Apply(string name, IAulaKeyboard keyboard)
    {
        KeyboardProfile? profile = Load(name) ?? throw new AulaException($"Profile '{name}' not found.");
        _log.LogInformation("Applying profile '{Name}'", name);
        ApplyProfile(profile, keyboard);
    }

    public void ApplyProfile(KeyboardProfile profile, IAulaKeyboard keyboard)
    {
        var colors = ResolveKeyColors(profile, keyboard);
        var config = profile.Lighting with { PerKeyColors = colors };
        keyboard.Lighting.Apply(config);
    }

    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_path))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(_path, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Delete(string name)
    {
        string path = BuildPath(name);
        if (!File.Exists(path))
        {
            _log.LogDebug("Delete skipped: profile '{Name}' not found", name);
            return false;
        }

        File.Delete(path);
        _log.LogInformation("Deleted profile '{Name}'", name);
        return true;
    }

    private static IReadOnlyList<RgbColor>? ResolveKeyColors(KeyboardProfile profile, IAulaKeyboard keyboard)
    {
        if (profile.KeyColors is not { Count: > 0 })
        {
            return null;
        }

        var colors = new RgbColor[126];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = new RgbColor(0, 0, 0);
        }

        foreach ((string key, RgbColor color) in profile.KeyColors)
        {
            int index = keyboard.Layout.GetLedIndex(key);
            if (index < 0)
            {
                throw new AulaException($"Profile references unknown key '{key}'.");
            }

            colors[index] = color;
        }

        return colors;
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }
}
