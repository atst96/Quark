using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Quark.Compatibles.Windows;

namespace Quark.Controls;

/// <summary>
/// 画面外配置のシークバーコントロール
/// </summary>
[ContentProperty(nameof(Child))]
[DefaultProperty(nameof(Child))]
[Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)]
public class IsolatedSeekBar : FrameworkElement
{
    private Window _window;

    static IsolatedSeekBar()
    {
        FocusableProperty.OverrideMetadata(typeof(IsolatedSeekBar), new FrameworkPropertyMetadata(false));
        Control.IsTabStopProperty.OverrideMetadata(typeof(IsolatedSeekBar), new FrameworkPropertyMetadata(false));
    }

    /// <summary>
    /// オーナーウィンドウ
    /// </summary>
    public Window? Owner
    {
        get => (Window)this.GetValue(OwnerProperty);
        set => this.SetValue(OwnerProperty, value);
    }

    /// <summary><see cref="Owner"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty OwnerProperty =
        DependencyProperty.Register(nameof(Owner), typeof(Window), typeof(IsolatedSeekBar), new PropertyMetadata(null, OwnerPropertyChanged));

    /// <summary>
    /// <see cref="Owner"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OwnerPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IsolatedSeekBar)d)._window.Owner = e.NewValue as Window;
    }

    /// <summary>表示／非表示フラグ</summary>
    public bool IsOpen
    {
        get => (bool)this.GetValue(IsOpenProperty);
        set => this.SetValue(IsOpenProperty, value);
    }

    /// <summary><see cref="IsOpen"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(IsolatedSeekBar), new PropertyMetadata(false, IsOpenPropertyChanged));

    /// <summary>
    /// <see cref="IsOpen"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void IsOpenPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = (IsolatedSeekBar)d;
        var w = s._window;

        bool isOpen = (bool)e.NewValue;
        if (isOpen)
        {
            if (w.Visibility != Visibility.Visible)
                w.Visibility = Visibility.Visible;
        }
        else
        {
            if (w.Visibility == Visibility.Visible)
                w.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>上位置</summary>
    public double Top
    {
        get => (double)this.GetValue(TopProperty);
        set => this.SetValue(TopProperty, value);
    }

    /// <summary><see cref="Top"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty TopProperty =
        DependencyProperty.Register("Top", typeof(double), typeof(IsolatedSeekBar), new PropertyMetadata(double.NaN, OnTopPtopertyChanged));

    /// <summary>
    /// <see cref="Top"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnTopPtopertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IsolatedSeekBar)d)._window.Top = (double)e.NewValue;
    }

    /// <summary>左位置</summary>
    public double Left
    {
        get => (double)this.GetValue(LeftProperty);
        set => this.SetValue(LeftProperty, value);
    }

    /// <summary><see cref="Left"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.Register(nameof(Left), typeof(double), typeof(IsolatedSeekBar), new PropertyMetadata(double.NaN, OnLeftPropertyChanged));

    /// <summary>
    /// <see cref="Left"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnLeftPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IsolatedSeekBar)d)._window.Left = (double)e.NewValue;
    }


    /// <summary>内容</summary>
    [Bindable(true)]
    public UIElement Child
    {
        get => (UIElement)this.GetValue(ChildProperty);
        set => this.SetValue(ChildProperty, value);
    }

    /// <summary><see cref="Child"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(nameof(Child), typeof(UIElement), typeof(IsolatedSeekBar), new PropertyMetadata(null));

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public IsolatedSeekBar() : base()
    {
        this._window = this.CreateWindow();
        this.LayoutUpdated += this.OnLayoutUpdated;
        this.Unloaded += this.OnUnLoaded;
    }

    /// <summary>
    /// アンロード時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnUnLoaded(object sender, RoutedEventArgs e)
    {
        this.LayoutUpdated -= this.OnLayoutUpdated;
        this.Unloaded -= this.OnLayoutUpdated;
    }

    /// <summary>
    /// レイアウト更新時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var w = this._window;
        w.Width = this.Width;
        w.Height = this.Height;
    }

    /// <summary>
    /// ウィンドウを作成する。
    /// </summary>
    /// <returns></returns>
    private Window CreateWindow()
    {
        var window = new Window()
        {
            Title = "SeekBar",
            Topmost = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            IsHitTestVisible = false,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            UseLayoutRounding = true,
            SnapsToDevicePixels = true,
            ShowActivated = false,
        };

        window.SourceInitialized += this.OnChildWindowSourceInitialized;
        window.GotFocus += this.OnChildWindowFocus;

        RenderOptions.SetEdgeMode(window, EdgeMode.Aliased);

        // 各値をバインド
        this.SetBinding(window, Window.TopProperty, IsolatedSeekBar.TopProperty);
        this.SetBinding(window, Window.LeftProperty, IsolatedSeekBar.LeftProperty);
        this.SetBinding(window, Window.ContentProperty, ChildProperty);
        return window;
    }

    /// <summary>
    /// 生成したウィンドウ初期化時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnChildWindowSourceInitialized(object? sender, EventArgs e)
    {
        var window = this._window;
        window.SourceInitialized -= this.OnChildWindowSourceInitialized;

        nint hwnd = new WindowInteropHelper(window).Handle;

        // マウス押下時にフォーカスが移動しないようにする
        uint style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        _ = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// ウィンドウのフォーカス時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void OnChildWindowFocus(object sender, RoutedEventArgs e)
    {
        // フォーカスをウィンドウに移す
        Window.GetWindow(this)?.Focus();
    }

    /// <summary>
    /// コントロール間でプロパティをバインドする。
    /// </summary>
    /// <param name="element"></param>
    /// <param name="destProperty"></param>
    /// <param name="srcProperty"></param>
    private void SetBinding(FrameworkElement element, DependencyProperty destProperty, DependencyProperty srcProperty)
        => element.SetBinding(destProperty, new Binding(srcProperty.Name) { Source = this, Mode = BindingMode.OneWay });
}
