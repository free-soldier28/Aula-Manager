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

    public IReadOnlyList<KeyboardRowViewModel> Rows { get; }

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
        Rows = BuildLayout();
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
            var colors = new RgbColor[F75Layout.LedCount];
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

    public void RefreshFromDevice() => IsConnected = _session.Current is not null;

    private IReadOnlyList<KeyboardRowViewModel> BuildLayout()
    {
        IKeyboardLayout layout = F75Layout.Instance;
        return KeyLayout.Select(row =>
        {
            var cells = new List<KeyCellViewModel>();
            double x = 0;
            double placed = 0;
            foreach (var k in row)
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

    // Gap is the space in pixels left of a key. The function row is grouped in
    // blocks of four (F1-F4, F5-F8, F9-F12) with small gaps between them, F1
    // sits above "2" and F12 ends where the Backspace key ends.
    private static readonly (string Name, string Label, double Width, double Gap)[][] KeyLayout =
    {
        new (string Name, string Label, double Width, double Gap)[]
        {
            ("esc", "Esc", 1.5, 0), ("f1", "F1", 1, 20), ("f2", "F2", 1, 3), ("f3", "F3", 1, 3), ("f4", "F4", 1, 3),
            ("f5", "F5", 1, 17), ("f6", "F6", 1, 3), ("f7", "F7", 1, 3), ("f8", "F8", 1, 3),
            ("f9", "F9", 1, 17), ("f10", "F10", 1, 3), ("f11", "F11", 1, 3), ("f12", "F12", 1, 3),
        },
        new (string Name, string Label, double Width, double Gap)[]
        {
            ("`", "`", 1, 0), ("1", "1", 1, 3), ("2", "2", 1, 3), ("3", "3", 1, 3), ("4", "4", 1, 3), ("5", "5", 1, 3),
            ("6", "6", 1, 3), ("7", "7", 1, 3), ("8", "8", 1, 3), ("9", "9", 1, 3), ("0", "0", 1, 3), ("-", "-", 1, 3),
            ("=", "=", 1, 3), ("backspace", "Bksp", 2.0, 3), ("del", "Del", 1, 3),
        },
        new (string Name, string Label, double Width, double Gap)[]
        {
            ("tab", "Tab", 1.5, 0), ("q", "Q", 1, 3), ("w", "W", 1, 3), ("e", "E", 1, 3), ("r", "R", 1, 3),
            ("t", "T", 1, 3), ("y", "Y", 1, 3), ("u", "U", 1, 3), ("i", "I", 1, 3), ("o", "O", 1, 3), ("p", "P", 1, 3),
            ("[", "[", 1, 3), ("]", "]", 1, 3), ("\\", "\\", 1.5, 3), ("pgup", "PgUp", 1, 3),
        },
        new (string Name, string Label, double Width, double Gap)[]
        {
            ("caps", "Caps", 1.75, 0), ("a", "A", 1, 3), ("s", "S", 1, 3), ("d", "D", 1, 3), ("f", "F", 1, 3),
            ("g", "G", 1, 3), ("h", "H", 1, 3), ("j", "J", 1, 3), ("k", "K", 1, 3), ("l", "L", 1, 3), (";", ";", 1, 3),
            ("'", "'", 1, 3), ("enter", "Enter", 2.3571, 3), ("pgdn", "PgDn", 1, 3),
        },
        new (string Name, string Label, double Width, double Gap)[]
        {
            ("lshift", "Shift", 2.25, 0), ("z", "Z", 1, 3), ("x", "X", 1, 3), ("c", "C", 1, 3), ("v", "V", 1, 3),
            ("b", "B", 1, 3), ("n", "N", 1, 3), ("m", "M", 1, 3), (",", ",", 1, 3), (".", ".", 1, 3), ("/", "/", 1, 3),
            ("rshift", "Shift", 1.75, 3), ("up", "↑", 1, 6), ("end", "End", 1, 3),
        },
        new (string Name, string Label, double Width, double Gap)[]
        {
            ("lctrl", "Ctrl", 1.25, 0), ("lwin", "Win", 1.25, 3), ("lalt", "Alt", 1.25, 3), ("space", "Space", 7.2857, 3),
            ("fn", "Fn", 1.25, 3), ("rctrl", "Ctrl", 1.25, 3),
            ("left", "←", 1, 6), ("down", "↓", 1, 3), ("right", "→", 1, 3),
        },
    };
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
