using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aula.App.Services;
using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Services;
using Avalonia.Media;

namespace Aula.App.ViewModels;

public partial class LightingViewModel : ObservableObject
{
    private readonly KeyboardSession _session;
    private IReadOnlyList<LedEffect> _effects = Array.Empty<LedEffect>();

    [ObservableProperty]
    private IReadOnlyList<EffectItem> _effectItems = Array.Empty<EffectItem>();

    [ObservableProperty]
    private EffectItem? _selectedEffect;

    [ObservableProperty]
    private bool _hasBrightness;

    [ObservableProperty]
    private bool _hasSpeed;

    [ObservableProperty]
    private bool _hasColor;

    [ObservableProperty]
    private int _brightness = 5;

    [ObservableProperty]
    private int _speed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private int _red = 255;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private int _green = 255;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private int _blue = 255;

    public RgbColor Color => new((byte)Math.Clamp(Red, 0, 255), (byte)Math.Clamp(Green, 0, 255), (byte)Math.Clamp(Blue, 0, 255));

    public string ColorHex => Color.ToHex();

    public IReadOnlyList<PaletteColor> Palette { get; } = LightingPalette.Create();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private HsvColor _wheelColor = new(1.0, 1.0, 1.0, 1.0);

    partial void OnWheelColorChanged(HsvColor value)
    {
        if (_syncingWheel)
        {
            return;
        }

        Avalonia.Media.Color c = value.ToRgb();
        Red = c.R;
        Green = c.G;
        Blue = c.B;
    }

    partial void OnRedChanged(int value) => SyncWheel();
    partial void OnGreenChanged(int value) => SyncWheel();
    partial void OnBlueChanged(int value) => SyncWheel();

    private bool _syncingWheel;

    private void SyncWheel()
    {
        if (_syncingWheel)
        {
            return;
        }

        _syncingWheel = true;
        try
        {
            Color c = Avalonia.Media.Color.FromArgb(255, (byte)Math.Clamp(Red, 0, 255), (byte)Math.Clamp(Green, 0, 255), (byte)Math.Clamp(Blue, 0, 255));
            WheelColor = c.ToHsv();
        }
        finally
        {
            _syncingWheel = false;
        }
    }

    [RelayCommand]
    private void PickColor(PaletteColor color)
    {
        Red = color.Color.R;
        Green = color.Color.G;
        Blue = color.Color.B;
    }

    [ObservableProperty]
    private bool _isColorful;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _message = string.Empty;

    public LightingViewModel(KeyboardSession session)
    {
        _session = session;
        _session.Changed += RefreshFromDevice;
    }

    public void RefreshFromDevice()
    {
        IAulaKeyboard? keyboard = _session.Current;
        IsConnected = keyboard is not null;

        _effects = keyboard?.Model.Effects ?? Array.Empty<LedEffect>();
        EffectItems = _effects.Select(e => new EffectItem(e)).ToList();
        if (EffectItems.Count > 0)
        {
            SelectedEffect = EffectItems[0];
        }

        ReadFromDevice();
    }

    partial void OnSelectedEffectChanged(EffectItem? value)
    {
        if (value is null)
        {
            HasBrightness = HasSpeed = HasColor = false;
            return;
        }

        HasBrightness = value.Effect.HasBrightness;
        HasSpeed = value.Effect.HasSpeed;
        HasColor = value.Effect.HasColor;
        if (!HasColor)
        {
            IsColorful = false;
        }
    }

    partial void OnIsColorfulChanged(bool value)
    {
        if (value)
        {
            Red = Green = Blue = 255;
        }
    }

    [RelayCommand]
    private void ReloadDevice()
    {
        Message = "Reloading device…";
        try
        {
            _session.Refresh();
            IsConnected = _session.IsConnected;
            Message = _session.IsConnected ? "Device reloaded." : "No device after reload.";
        }
        catch (Exception ex)
        {
            Message = "Reload failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Read()
    {
        if (_session.Current is null)
        {
            Message = "No device connected.";
            return;
        }

        ReadFromDevice();
        Message = "Read current config.";
    }

    [RelayCommand]
    private void Apply()
    {
        if (_session.Current is not { } keyboard)
        {
            Message = "No device connected.";
            return;
        }

        if (SelectedEffect is null)
        {
            Message = "Select an effect first.";
            return;
        }

        var config = new LightingConfig(
            SelectedEffect.Effect.Id,
            HasBrightness ? Brightness : null,
            HasSpeed ? Speed : null,
            HasColor && !IsColorful ? Color : null,
            IsColorful);

        try
        {
            keyboard.Lighting.Apply(config);
            Message = $"Applied '{SelectedEffect.Effect.Name}' (brightness={config.Brightness?.ToString() ?? "-"}, speed={config.Speed?.ToString() ?? "-"}).";
        }
        catch (Exception ex)
        {
            Message = "Failed to apply: " + ex.Message;
        }
    }

    [RelayCommand]
    private void TurnOff()
    {
        if (_session.Current is not { } keyboard)
        {
            Message = "No device connected.";
            return;
        }

        try
        {
            keyboard.Lighting.TurnOff();
            Message = "Lighting turned off.";
        }
        catch (Exception ex)
        {
            Message = "Failed to turn off: " + ex.Message;
        }
    }

    private void ReadFromDevice()
    {
        if (_session.Current is not { } keyboard)
        {
            return;
        }

        KeyboardConfig config;
        int id;
        try
        {
            config = keyboard.Lighting.ReadConfig();
        }
        catch (Exception ex)
        {
            Message = "Failed to read config: " + ex.Message;
            return;
        }
        id = config.EffectId;
        SelectedEffect = EffectItems.FirstOrDefault(i => i.Effect.Id == id) ?? EffectItems.FirstOrDefault();

        EffectParams? p = config.GetParams(id);
        if (p is { } effectParams)
        {
            Brightness = Math.Clamp((int)effectParams.Brightness, 0, 9);
            Speed = Math.Clamp((int)effectParams.Speed, 0, 4);
            IsColorful = effectParams.Colorful;
            if (IsColorful)
            {
                Red = Green = Blue = 255;
            }
        }
    }
}

public sealed record EffectItem(LedEffect Effect)
{
    public int Id => Effect.Id;
    public string Name => Effect.Name;
    public override string ToString() => $"{Effect.Id,2}  {Effect.Name}";
}

public sealed record PaletteColor(RgbColor Color)
{
    public string Hex => Color.ToHex();
}

public static class LightingPalette
{
    public static IReadOnlyList<PaletteColor> Create()
    {
        string[] hexes =
        {
            "#FFFFFF", "#FF4444", "#FFA500", "#FFEB3B", "#76FF03",
            "#00E676", "#00BFA5", "#26C6DA", "#40C4FF", "#2979FF",
            "#7C4DFF", "#B388FF", "#F50057", "#FF6E40", "#8D6E63",
            "#78909C",
        };
        return hexes.Select(RgbColor.FromHex).Select(c => new PaletteColor(c)).ToList();
    }
}
