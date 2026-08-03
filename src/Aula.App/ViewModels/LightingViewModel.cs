using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aula.App.Services;
using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Services;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aula.App.ViewModels;

public partial class LightingViewModel : ObservableObject
{
    private readonly KeyboardSession _session;
    private readonly ProfileService _profiles = new();
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private IReadOnlyList<LedEffect> _effects = Array.Empty<LedEffect>();
    private string? _layoutModelId;
    private IReadOnlyList<PreviewLed> _positions = Array.Empty<PreviewLed>();
    private DispatcherTimer? _previewTimer;
    private int _previousEffectId = -1;

    public IReadOnlyList<KeyboardRowViewModel> Rows { get; private set; }

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
    [NotifyPropertyChangedFor(nameof(PreviewBrush))]
    private int _red = 255;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    [NotifyPropertyChangedFor(nameof(PreviewBrush))]
    private int _green = 255;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    [NotifyPropertyChangedFor(nameof(PreviewBrush))]
    private int _blue = 255;

    public RgbColor Color => new((byte)Math.Clamp(Red, 0, 255), (byte)Math.Clamp(Green, 0, 255), (byte)Math.Clamp(Blue, 0, 255));

    public string ColorHex => Color.ToHex();

    public IBrush PreviewBrush => new SolidColorBrush(Avalonia.Media.Color.FromArgb(255, (byte)Math.Clamp(Red, 0, 255), (byte)Math.Clamp(Green, 0, 255), (byte)Math.Clamp(Blue, 0, 255)));

    public IReadOnlyList<PaletteColor> Palette { get; } = LightingPalette.Create();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    [NotifyPropertyChangedFor(nameof(PreviewBrush))]
    private HsvColor _wheelColor = new(1.0, 1.0, 1.0, 1.0);

    private bool _syncingWheel;

    [ObservableProperty]
    private KeyCellViewModel? _selectedKey;

    [ObservableProperty]
    private bool _isColorful;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _profilesList = Array.Empty<string>();

    [ObservableProperty]
    private string? _selectedProfile;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    private IKeyboardLayout CurrentLayout => _session.Current?.Layout ?? F75Layout.Instance;

    public LightingViewModel(KeyboardSession session)
    {
        _session = session;
        _session.Changed += RefreshFromDevice;
        Rows = BuildLayout(CurrentLayout);
        _positions = EffectPreviewRenderer.BuildPositions(CurrentLayout);
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

        RefreshLayout();
        RefreshProfiles();
        ReadFromDevice();
    }

    private void RefreshLayout()
    {
        string modelId = _session.Current?.Model.Id ?? string.Empty;
        if (_layoutModelId != modelId)
        {
            _layoutModelId = modelId;
            Rows = BuildLayout(CurrentLayout);
            _positions = EffectPreviewRenderer.BuildPositions(CurrentLayout);
        }
    }

    partial void OnSelectedEffectChanged(EffectItem? value)
    {
        if (value is null)
        {
            HasBrightness = HasSpeed = HasColor = false;
            StopPreview();
            return;
        }

        HasBrightness = value.Effect.HasBrightness;
        HasSpeed = value.Effect.HasSpeed;
        HasColor = value.Effect.HasColor;
        if (!HasColor)
        {
            IsColorful = false;
        }

        if (value.Effect.Id == 21)
        {
            StopPreview();
            if (_previousEffectId != 21)
            {
                ClearCanvas();
            }
        }
        else
        {
            StartPreview();
        }

        _previousEffectId = value.Effect.Id;
    }

    partial void OnIsColorfulChanged(bool value)
    {
        if (value)
        {
            Red = Green = Blue = 255;
        }
    }

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

    private void SyncWheel()
    {
        if (_syncingWheel)
        {
            return;
        }

        _syncingWheel = true;
        try
        {
            Avalonia.Media.Color c = Avalonia.Media.Color.FromArgb(255, (byte)Math.Clamp(Red, 0, 255), (byte)Math.Clamp(Green, 0, 255), (byte)Math.Clamp(Blue, 0, 255));
            WheelColor = c.ToHsv();
            if (SelectedKey is { } key && SelectedEffect?.Effect.Id == 21)
            {
                key.Color = Color;
            }
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

    [RelayCommand]
    private void SelectKey(KeyCellViewModel key)
    {
        if (SelectedEffect?.Effect.Id != 21)
        {
            Message = "Select the custom effect to paint keys.";
            return;
        }

        if (SelectedKey is { } previous)
        {
            previous.IsSelected = false;
        }

        SelectedKey = key;
        key.IsSelected = true;
        key.Color = Color;
    }

    [RelayCommand]
    private void FillAll()
    {
        if (SelectedEffect?.Effect.Id != 21)
        {
            Message = "Select the custom effect to paint keys.";
            return;
        }

        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            key.Color = Color;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (SelectedEffect?.Effect.Id != 21)
        {
            Message = "Select the custom effect to paint keys.";
            return;
        }

        ClearCanvas();
    }

    private void ClearCanvas()
    {
        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            key.Color = default;
        }
    }

    private void StartPreview()
    {
        if (_previewTimer is not null || !IsConnected || SelectedEffect is null || SelectedEffect.Effect.Id == 21)
        {
            return;
        }

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _previewTimer.Tick += (_, _) => RenderPreview();
        _previewTimer.Start();
        RenderPreview();
    }

    private void StopPreview()
    {
        if (_previewTimer is null)
        {
            return;
        }

        _previewTimer.Stop();
        _previewTimer = null;
    }

    private void RenderPreview()
    {
        if (_session.Current is null || SelectedEffect is null || SelectedEffect.Effect.Id == 21)
        {
            StopPreview();
            return;
        }

        RgbColor[] colors = EffectPreviewRenderer.Render(
            SelectedEffect.Effect,
            _positions,
            _watch.Elapsed.TotalSeconds,
            HasBrightness ? Brightness : 9,
            HasSpeed ? Speed : 2,
            Color,
            IsColorful);

        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            if (key.LedIndex >= 0 && key.LedIndex < colors.Length)
            {
                key.Color = colors[key.LedIndex];
            }
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

        try
        {
            if (SelectedEffect.Effect.Id == 21)
            {
                RgbColor[] colors = CollectKeyColors();
                keyboard.Lighting.Apply(new LightingConfig(EffectId: 21, PerKeyColors: colors));
                int colored = colors.Count(c => c != default);
                Message = $"Applied per-key colors to {colored} LED(s).";
                return;
            }

            bool solidColor = SelectedEffect.Effect.Id == 1;
            var config = new LightingConfig(
                SelectedEffect.Effect.Id,
                HasBrightness ? Brightness : null,
                HasSpeed ? Speed : null,
                HasColor && (!IsColorful || solidColor) ? Color : null,
                IsColorful);

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

    private RgbColor[] CollectKeyColors()
    {
        IKeyboardLayout layout = CurrentLayout;
        var colors = new RgbColor[layout.LedCount];
        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            if (key.LedIndex >= 0 && key.LedIndex < colors.Length)
            {
                colors[key.LedIndex] = key.Color;
            }
        }

        return colors;
    }

    partial void OnNewProfileNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SelectedProfile = value;
    }

    [RelayCommand]
    private void RefreshProfiles() => ProfilesList = _profiles.List();

    [RelayCommand]
    private void SaveProfile()
    {
        if (_session.Current is not { } keyboard)
        {
            Message = "No device connected.";
            return;
        }

        string name = NewProfileName.Trim();
        if (name.Length == 0)
        {
            Message = "Enter a profile name.";
            return;
        }

        try
        {
            var profile = new KeyboardProfile(
                name,
                BuildLightingConfig(),
                BuildKeyColorsFromCanvas(),
                Model: keyboard.Model.Id);
            _profiles.Save(name, profile);
            RefreshProfiles();
            SelectedProfile = name;
            Message = $"Saved profile '{name}'.";
        }
        catch (Exception ex)
        {
            Message = $"Failed to save profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ApplyProfile()
    {
        if (_session.Current is null)
        {
            Message = "No device connected.";
            return;
        }

        if (SelectedProfile is null)
        {
            Message = "Select a profile first.";
            return;
        }

        try
        {
            _profiles.Apply(SelectedProfile, _session.Current);
            Message = $"Applied profile '{SelectedProfile}'.";
            ReadFromDevice();
        }
        catch (Exception ex)
        {
            Message = $"Failed to apply profile: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            Message = "Select a profile first.";
            return;
        }

        try
        {
            _profiles.Delete(SelectedProfile);
            RefreshProfiles();
            Message = $"Deleted profile '{SelectedProfile}'.";
        }
        catch (Exception ex)
        {
            Message = $"Failed to delete profile: {ex.Message}";
        }
    }

    private LightingConfig BuildLightingConfig()
    {
        if (SelectedEffect is null)
        {
            return new LightingConfig(EffectId: 0);
        }

        bool solidColor = SelectedEffect.Effect.Id == 1;
        return new LightingConfig(
            SelectedEffect.Effect.Id,
            HasBrightness ? Brightness : null,
            HasSpeed ? Speed : null,
            HasColor && (!IsColorful || solidColor) ? Color : null,
            IsColorful);
    }

    private IReadOnlyDictionary<string, RgbColor>? BuildKeyColorsFromCanvas()
    {
        var colors = new Dictionary<string, RgbColor>();
        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            if (key.Color != default)
            {
                colors[key.Name] = key.Color;
            }
        }

        return colors.Count > 0 ? colors : null;
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

    private IReadOnlyList<KeyboardRowViewModel> BuildLayout(IKeyboardLayout layout)
    {
        return layout.Rows.Select(row =>
        {
            var cells = new List<KeyCellViewModel>();
            double x = 0;
            double placed = 0;
            foreach (KeyShape k in row)
            {
                x += k.Gap;
                double margin = x - placed;
                cells.Add(new KeyCellViewModel(k.Name, k.Label, layout.GetLedIndex(k.Name), k.Width, margin, SelectKey));
                placed += margin + k.Width * KeyCellViewModel.KeyUnit;
                x = placed;
            }

            return new KeyboardRowViewModel(cells);
        }).ToList();
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

public partial class KeyCellViewModel : ObservableObject
{
    internal const double KeyUnit = 28;

    private readonly Action<KeyCellViewModel>? _onSelect;

    public KeyCellViewModel(string name, string label, int ledIndex, double width, double marginLeft, Action<KeyCellViewModel>? onSelect = null)
    {
        Name = name;
        Label = label;
        LedIndex = ledIndex;
        Width = width;
        MarginLeft = marginLeft;
        _onSelect = onSelect;
    }

    public string Name { get; }

    public string Label { get; }

    public int LedIndex { get; }

    public double Width { get; }

    public double MarginLeft { get; }

    public Thickness Margin => new(MarginLeft, 0, 0, 0);

    public double PixelWidth => Width * KeyUnit;

    [ObservableProperty]
    private RgbColor _color;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnColorChanged(RgbColor value)
    {
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(Foreground));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedBorder));
        OnPropertyChanged(nameof(BorderSize));
    }

    public IBrush Background => new SolidColorBrush(Avalonia.Media.Color.FromRgb(Color.R, Color.G, Color.B));

    public IBrush Foreground => new SolidColorBrush(TextColor(Color));

    public IBrush SelectedBorder => IsSelected ? Brushes.White : Brushes.Transparent;

    public double BorderSize => IsSelected ? 2 : 0;

    public override string ToString() => $"{Name} = {Color.ToHex()}";

    [RelayCommand]
    private void Select() => _onSelect?.Invoke(this);

    private static Color TextColor(RgbColor c)
    {
        double luminance = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
        return luminance > 128 ? Colors.Black : Colors.White;
    }
}

public sealed record KeyboardRowViewModel(IReadOnlyList<KeyCellViewModel> Keys);
