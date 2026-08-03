using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aula.App.Services;
using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Services;
using Avalonia;
using Avalonia.Media;

namespace Aula.App.ViewModels;

public partial class PerKeyViewModel : ObservableObject
{
    private readonly KeyboardSession _session;

    public IReadOnlyList<KeyboardRowViewModel> Rows { get; private set; }

    private string? _layoutModelId;

    private IKeyboardLayout CurrentLayout => _session.Current?.Layout ?? F75Layout.Instance;

    [ObservableProperty]
    private KeyCellViewModel? _selectedKey;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _message = string.Empty;

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

    public PerKeyViewModel(KeyboardSession session)
    {
        _session = session;
        _session.Changed += RefreshFromDevice;
        Rows = BuildLayout(CurrentLayout);
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
            if (SelectedKey is { } key)
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
    private void SelectKey(KeyCellViewModel key)
    {
        if (SelectedKey is { } previous)
        {
            previous.IsSelected = false;
        }

        SelectedKey = key;
        key.IsSelected = true;
        key.Color = Color;
    }

    [RelayCommand]
    private void PickColor(PaletteColor color)
    {
        Red = color.Color.R;
        Green = color.Color.G;
        Blue = color.Color.B;
    }

    [RelayCommand]
    private void Apply()
    {
        if (_session.Current is not { } keyboard)
        {
            Message = "No device connected.";
            return;
        }

        try
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

            keyboard.Lighting.Apply(new LightingConfig(EffectId: 21, PerKeyColors: colors));
            int colored = colors.Count(c => c != default);
            Message = $"Applied per-key colors to {colored} LED(s).";
        }
        catch (Exception ex)
        {
            Message = "Failed to apply: " + ex.Message;
        }
    }

    [RelayCommand]
    private void FillAll()
    {
        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            key.Color = Color;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (KeyCellViewModel key in Rows.SelectMany(r => r.Keys))
        {
            key.Color = default;
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

    public void RefreshFromDevice()
    {
        IsConnected = _session.Current is not null;
        string modelId = _session.Current?.Model.Id ?? string.Empty;
        if (_layoutModelId != modelId)
        {
            _layoutModelId = modelId;
            Rows = BuildLayout(CurrentLayout);
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
