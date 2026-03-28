using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using CursorFX.App.Services;

namespace CursorFX.App;

public partial class ColorPickerWindow : Window, INotifyPropertyChanged
{
    private bool _updatingFromHex;
    private string _hexColor;
    private byte _alpha;
    private byte _red;
    private byte _green;
    private byte _blue;
    private readonly LocalizationService _localizationService;

    public ColorPickerWindow(string title, string initialColor, LocalizationService localizationService)
    {
        _localizationService = localizationService;
        InitializeComponent();
        PickerTitle = title;
        _hexColor = NormalizeHex(initialColor);
        ApplyHex(_hexColor);
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PickerTitle { get; }

    public string WindowTitle => _localizationService.Get("colorPicker.windowTitle");

    public string CancelText => _localizationService.Get("colorPicker.cancel");

    public string ConfirmText => _localizationService.Get("colorPicker.confirm");

    public string HexLabel => _localizationService.Get("colorPicker.hex");

    public string SelectedColor => HexColor;

    public System.Windows.Media.Brush PreviewBrush => new SolidColorBrush(System.Windows.Media.Color.FromArgb(Alpha, Red, Green, Blue));

    public string HexColor
    {
        get => _hexColor;
        set
        {
            if (_hexColor == value)
            {
                return;
            }

            _hexColor = value.ToUpperInvariant();
            OnPropertyChanged();

            if (_updatingFromHex)
            {
                return;
            }

            if (TryParseColor(_hexColor, out var color))
            {
                ApplyColor(color);
            }
        }
    }

    public byte Alpha
    {
        get => _alpha;
        set => SetChannel(ref _alpha, value);
    }

    public byte Red
    {
        get => _red;
        set => SetChannel(ref _red, value);
    }

    public byte Green
    {
        get => _green;
        set => SetChannel(ref _green, value);
    }

    public byte Blue
    {
        get => _blue;
        set => SetChannel(ref _blue, value);
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SetChannel(ref byte channel, byte value, [CallerMemberName] string? propertyName = null)
    {
        if (channel == value)
        {
            return;
        }

        channel = value;
        OnPropertyChanged(propertyName);
        SyncHexFromChannels();
    }

    private void SyncHexFromChannels()
    {
        _updatingFromHex = true;
        HexColor = $"#{Alpha:X2}{Red:X2}{Green:X2}{Blue:X2}";
        _updatingFromHex = false;
        OnPropertyChanged(nameof(PreviewBrush));
    }

    private void ApplyHex(string value)
    {
        if (!TryParseColor(value, out var color))
        {
            color = Colors.White;
        }

        ApplyColor(color);
    }

    private void ApplyColor(System.Windows.Media.Color color)
    {
        _alpha = color.A;
        _red = color.R;
        _green = color.G;
        _blue = color.B;
        OnPropertyChanged(nameof(Alpha));
        OnPropertyChanged(nameof(Red));
        OnPropertyChanged(nameof(Green));
        OnPropertyChanged(nameof(Blue));
        OnPropertyChanged(nameof(PreviewBrush));
        _updatingFromHex = true;
        _hexColor = $"#{_alpha:X2}{_red:X2}{_green:X2}{_blue:X2}";
        OnPropertyChanged(nameof(HexColor));
        _updatingFromHex = false;
    }

    private static bool TryParseColor(string? value, out System.Windows.Media.Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeHex(string? value)
    {
        return TryParseColor(value, out var color)
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : "#FFFFFFFF";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
