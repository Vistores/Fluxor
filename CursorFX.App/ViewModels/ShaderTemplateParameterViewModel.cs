using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CursorFX.Core.Models;

namespace CursorFX.App.ViewModels;

public sealed class ShaderTemplateParameterViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> AdvancedKeys =
    [
        "trailMode",
        "waveAmplitude",
        "waveFrequency",
        "noiseAmount",
        "ribbonSoftness",
        "detail",
        "gravityX",
        "gravityY",
        "randomness",
        "driftStrength",
        "spawnRadius",
        "spawnRate",
        "matrixDamping",
        "idleScatterThreshold",
        "idleScatterRadius",
        "idleScatterSpeed",
        "trailFreedom",
        "trailSpawnSpacing",
        "trailLifetime",
        "sampleOpacity",
        "backdropSize",
        "distortion"
    ];

    private readonly TemplateParameterDefinition _definition;
    private readonly Action _onChanged;
    private readonly Func<string, string, string?> _pickColor;
    private double _numberValue;
    private string _colorValue;
    private bool _booleanValue;

    public ShaderTemplateParameterViewModel(
        TemplateParameterDefinition definition,
        TemplateParameterValue? initialValue,
        Action onChanged,
        Func<string, string, string?> pickColor)
    {
        _definition = definition;
        _onChanged = onChanged;
        _pickColor = pickColor;
        _numberValue = initialValue?.NumberValue ?? definition.DefaultNumber;
        _colorValue = initialValue?.ColorValue ?? definition.DefaultColor;
        _booleanValue = initialValue?.BooleanValue ?? definition.DefaultBoolean;
        PickColorCommand = new RelayCommand(OpenColorPicker);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key => _definition.Key;

    public string DisplayName => _definition.DisplayName;

    public PluginParameterSection Section => _definition.Section;

    public bool IsNumber => _definition.Type == TemplateParameterType.Number;

    public bool IsColor => _definition.Type == TemplateParameterType.Color;

    public bool IsToggle => _definition.Type == TemplateParameterType.Toggle;

    public bool IsAdvanced => _definition.IsAdvanced || AdvancedKeys.Contains(Key);

    public string HelperText => Key switch
    {
        "sourceLag" => "Controls how tightly the effect sticks to the cursor.",
        "inertia" => "Higher values smooth motion and reduce sudden jumps.",
        "trailLength" => "How long the trail remains visible behind the cursor.",
        "trailThickness" => "Base width of the trail body.",
        "trailFade" => "How quickly the trail fades away.",
        "glowSize" => "Size of the glow around the cursor.",
        "glowOpacity" => "Brightness of the cursor glow.",
        "rippleRadius" => "Maximum size of the click impact ring.",
        "rippleLifetime" => "How long click feedback remains visible.",
        "rippleThickness" => "Thickness of the click impact ring.",
        "size" => "Overall size of the main shader layer.",
        "opacity" => "Transparency of the main shader layer.",
        "motion" => "Primary motion speed of the effect.",
        "particles" => "Amount of secondary accents used by this profile.",
        "waveAmplitude" => "How strongly the ribbon bends away from its base path.",
        "waveFrequency" => "How dense the ribbon waves appear.",
        "noiseAmount" => "Adds controlled irregularity to the trail edge.",
        "trailMode" => "Switches between the available trail rendering styles.",
        _ when IsToggle => "Turns this layer on or off for the current profile.",
        _ => string.Empty
    };

    public Visibility HelperVisibility => string.IsNullOrWhiteSpace(HelperText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NumberVisibility => IsNumber ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColorVisibility => IsColor ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToggleVisibility => IsToggle ? Visibility.Visible : Visibility.Collapsed;

    public double Minimum => _definition.Min;

    public double Maximum => _definition.Max;

    public double Step => _definition.Step;

    public RelayCommand PickColorCommand { get; }

    public double NumberValue
    {
        get => _numberValue;
        set
        {
            if (Math.Abs(_numberValue - value) < 0.0001)
            {
                return;
            }

            _numberValue = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public string ColorValue
    {
        get => _colorValue;
        private set
        {
            if (string.Equals(_colorValue, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _colorValue = value.ToUpperInvariant();
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool BooleanValue
    {
        get => _booleanValue;
        set
        {
            if (_booleanValue == value)
            {
                return;
            }

            _booleanValue = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public TemplateParameterValue ToValue()
    {
        return new TemplateParameterValue
        {
            NumberValue = IsNumber ? NumberValue : null,
            ColorValue = IsColor ? ColorValue : null,
            BooleanValue = IsToggle ? BooleanValue : null
        };
    }

    private void OpenColorPicker()
    {
        var picked = _pickColor(DisplayName, ColorValue);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            ColorValue = picked;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
