using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Modelado.App;

/// <summary>
/// The control templates the design needs and Fluent does not draw (MOD4, from PhotoBoost's
/// Templates). Built in code rather than parsed, and a new one per call: a template is sealed on the
/// thread that first applies it, so one shared through a static field is refused by a second window.
/// </summary>
internal static class Templates
{
    /// <summary>The name a template's outer border goes by, which its triggers fill.</summary>
    private const string Ground = "Ground";

    /// <summary>A button with no fill until the pointer is over it, rounded at 6: the toolbar's buttons.</summary>
    /// <returns>A new template.</returns>
    public static ControlTemplate Ghost()
    {
        FrameworkElementFactory content = new(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        FrameworkElementFactory ground = new(typeof(Border), Ground);
        ground.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        ground.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        ground.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        ground.AppendChild(content);

        ControlTemplate template = new(typeof(ButtonBase)) { VisualTree = ground };
        template.Triggers.Add(Filled(UIElement.IsMouseOverProperty, "SubtleFillColorSecondaryBrush"));
        template.Triggers.Add(Filled(ButtonBase.IsPressedProperty, "SubtleFillColorTertiaryBrush"));

        Trigger disabled = new() { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
        template.Triggers.Add(disabled);
        return template;
    }

    private static Trigger Filled(DependencyProperty property, string key)
    {
        Trigger trigger = new() { Property = property, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension(key), Ground));
        return trigger;
    }
}
