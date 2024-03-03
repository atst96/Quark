using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Quark.Services;
using Quark.UI.Mvvm;

namespace Quark.Behaviors;

public class ViewModelLocator
{
    static ViewModelLocator()
    {
        DisposeDataContextOnWindowClosedProperty.Changed.AddClassHandler<Window>(OnDisposeDataContextOnWindowClosedPropertyChanged);
        TypeProperty.Changed.AddClassHandler<Window>(OnTypePropertyChanged);
    }

    private static IServiceProvider GetServiceProvider() => ServiceLocator.ServiceProvider;

    public static readonly AttachedProperty<bool> DisposeDataContextOnWindowClosedProperty = AvaloniaProperty.RegisterAttached<ViewModelLocator, Window, bool>(
        "DisposeDataContextOnWindowClosed", defaultValue: false, inherits: false, BindingMode.OneTime);

    public static bool GetDisposeDataContextOnWindowClosed(Window element)
        => element.GetValue(DisposeDataContextOnWindowClosedProperty);

    public static void SetDisposeDataContextOnWindowClosed(Window element, bool value)
        => element.SetValue(DisposeDataContextOnWindowClosedProperty, value);

    private static void OnDisposeDataContextOnWindowClosedPropertyChanged(Window target, AvaloniaPropertyChangedEventArgs args)
    {
        if (Design.IsDesignMode)
            return;


        if (args.GetNewValue<bool>())
            target.Closed += OnClosed;
        else
            target.Closed -= OnClosed;
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        var w = (Window)sender!;
        if (GetDisposeDataContextOnWindowClosed(w) && w.DataContext is IDisposable d)
            d.Dispose();

        w.Closed -= OnClosed;
    }

    public static readonly AttachedProperty<Type?> TypeProperty = AvaloniaProperty.RegisterAttached<ViewModelLocator, Window, Type?>(
        "Type", defaultValue: null, inherits: false, BindingMode.OneTime);

    public static Type? GetType(Window element)
        => element.GetValue(TypeProperty);

    public static void SetType(Window element, Type? value)
        => element.SetValue(TypeProperty, value);

    public static object? GetViewModelFromType(Window window, Type type)
    {
        var obj = GetServiceProvider().GetService(type);

        if (obj is IDialogServiceViewModel dsvm)
        {
            dsvm.DialogService?.SetOwner(window);
        }

        return obj;
    }

    private static void OnTypePropertyChanged(Window window, AvaloniaPropertyChangedEventArgs args)
    {
        if (window is null || Design.IsDesignMode)
            return;

        var oldDataContext = window.DataContext;
        if (oldDataContext is IDisposable disposable)
            disposable.Dispose();


        window.DataContext = args.NewValue is Type type
            ? GetViewModelFromType(window, type)
            : null;
    }
}
