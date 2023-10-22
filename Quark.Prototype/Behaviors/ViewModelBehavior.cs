using System;
using System.ComponentModel;
using System.Windows;
using Quark.Services;

namespace Quark.Behaviors;

public static class ViewModelBehavior
{
    private static IServiceProvider GetServiceProvider() => ServiceLocator.ServiceProvider;

    public static bool GetDisposeDataContextOnWindowClosed(DependencyObject obj)
        => (bool)obj.GetValue(DisposeDataContextOnWindowClosedProperty);

    public static void SetDisposeDataContextOnWindowClosed(DependencyObject obj, bool value)
        => obj.SetValue(DisposeDataContextOnWindowClosedProperty, value);

    public static readonly DependencyProperty DisposeDataContextOnWindowClosedProperty =
        DependencyProperty.RegisterAttached("DisposeDataContextOnWindowClosed", typeof(bool), typeof(FrameworkElement), new PropertyMetadata(false, OnDisposeDataContextOnClosePropertyChanged));

    private static void OnDisposeDataContextOnClosePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(d))
        {
            if (d is Window w)
            {
                var newValue = (bool)e.NewValue;

                if (newValue)
                {
                    w.Closed += OnClosed;
                }
                else
                {
                    w.Closed -= OnClosed;
                }
            }
        }
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        var w = (Window)sender!;
        if (GetDisposeDataContextOnWindowClosed(w) && w.DataContext is IDisposable d)
        {
            d.Dispose();
        }

        w.Closed -= OnClosed;
    }

    public static Type GetType(DependencyObject obj)
        => (Type)obj.GetValue(TypeProperty);

    public static void SetType(DependencyObject obj, Type value)
        => obj.SetValue(TypeProperty, value);

    // Using a DependencyProperty as the backing store for Type.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.RegisterAttached("Type", typeof(Type), typeof(FrameworkElement), new PropertyMetadata(null, OnTypePropertyChanged));

    private static void OnTypePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(d))
        {
            d.SetValue(FrameworkElement.DataContextProperty,
                e.NewValue is Type type ? GetServiceProvider().GetService(type) : null);
        }
    }
}
