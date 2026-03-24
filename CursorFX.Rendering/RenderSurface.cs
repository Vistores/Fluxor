using System.Windows;
using System.Windows.Media;
using CursorFX.Core.Services;

namespace CursorFX.Rendering;

public sealed class RenderSurface : FrameworkElement
{
    public static readonly DependencyProperty EffectManagerProperty =
        DependencyProperty.Register(
            nameof(EffectManager),
            typeof(EffectManager),
            typeof(RenderSurface),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RenderOffsetProperty =
        DependencyProperty.Register(
            nameof(RenderOffset),
            typeof(Vector),
            typeof(RenderSurface),
            new FrameworkPropertyMetadata(default(Vector), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDormantProperty =
        DependencyProperty.Register(
            nameof(IsDormant),
            typeof(bool),
            typeof(RenderSurface),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public EffectManager? EffectManager
    {
        get => (EffectManager?)GetValue(EffectManagerProperty);
        set => SetValue(EffectManagerProperty, value);
    }

    public Vector RenderOffset
    {
        get => (Vector)GetValue(RenderOffsetProperty);
        set => SetValue(RenderOffsetProperty, value);
    }

    public bool IsDormant
    {
        get => (bool)GetValue(IsDormantProperty);
        set => SetValue(IsDormantProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (EffectManager is null || IsDormant)
        {
            return;
        }

        drawingContext.PushTransform(new TranslateTransform(-RenderOffset.X, -RenderOffset.Y));
        EffectManager.Render(drawingContext);
        drawingContext.Pop();
    }
}
