using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MandarinJuiceWpf.CustomControls;

public partial class EclipseImageControl
{
    // AccentBrush
    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(
            nameof(AccentBrush),
            typeof(Brush),
            typeof(EclipseImageControl),
            new PropertyMetadata(null));
    public Brush? AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    // CornerRadius
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(double),
            typeof(EclipseImageControl),
            new PropertyMetadata(5.0));
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    // InnerClickCommand
    public static readonly DependencyProperty InnerClickCommandProperty =
        DependencyProperty.Register(
            nameof(InnerClickCommand),
            typeof(ICommand),
            typeof(EclipseImageControl),
            new PropertyMetadata(null));
    public ICommand InnerClickCommand
    {
        get => (ICommand)GetValue(InnerClickCommandProperty);
        set => SetValue(InnerClickCommandProperty, value);
    }

    // ImageToolTip
    public static readonly DependencyProperty ImageToolTipProperty =
        DependencyProperty.Register(
            nameof(ImageToolTip),
            typeof(string),
            typeof(EclipseImageControl),
            new PropertyMetadata(string.Empty));
    public string ImageToolTip
    {
        get => (string)GetValue(ImageToolTipProperty);
        set => SetValue(ImageToolTipProperty, value);
    }

    // Base64InnerImage
    public static readonly DependencyProperty Base64InnerImageProperty =
        DependencyProperty.Register(
            nameof(Base64InnerImage),
            typeof(string),
            typeof(EclipseImageControl),
            new PropertyMetadata(string.Empty, CheckIfInnerImageExists));
    public string Base64InnerImage
    {
        get => (string)GetValue(Base64InnerImageProperty);
        set => SetValue(Base64InnerImageProperty, value);
    }

    // ShouldBeVisible
    private static readonly DependencyPropertyKey ShouldBeVisiblePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ShouldBeVisible),
            typeof(bool),
            typeof(EclipseImageControl),
            new PropertyMetadata(true));
    
    internal static readonly DependencyProperty ShouldBeVisibleProperty = ShouldBeVisiblePropertyKey.DependencyProperty;

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool ShouldBeVisible
    {
        get => (bool)GetValue(ShouldBeVisibleProperty);
        private set => SetValue(ShouldBeVisiblePropertyKey, value);
    }

    // HideWhenEmpty
    public static readonly DependencyProperty HideWhenEmptyProperty =
        DependencyProperty.Register(
            nameof(HideWhenEmpty),
            typeof(bool),
            typeof(EclipseImageControl),
            new PropertyMetadata(true, CheckIfInnerImageExists));
    public bool HideWhenEmpty
    {
        get => (bool)GetValue(HideWhenEmptyProperty);
        set => SetValue(HideWhenEmptyProperty, value);
    }

    private static void CheckIfInnerImageExists(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EclipseImageControl)d;
        if (control.HideWhenEmpty)
            control.ShouldBeVisible = !string.IsNullOrWhiteSpace(control.Base64InnerImage);
        else
            control.ShouldBeVisible = true;
    }

    private void AccentBrushSetDefaultIfNull()
        => AccentBrush ??= Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush ?? Brushes.Orange;

    public EclipseImageControl()
    {
        InitializeComponent();
        AccentBrushSetDefaultIfNull();
        CheckIfInnerImageExists(this, new DependencyPropertyChangedEventArgs());
    }
}