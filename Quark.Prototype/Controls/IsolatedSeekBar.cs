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
[Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)]
public class IsolatedSeekBar : FrameworkElement
{
    /// <summary>ウィンドウ</summary>
    private Window _window;
    /// <summary><see cref="_window"/>のDPI</summary>
    private DpiScale _windowDdpi;

    static IsolatedSeekBar()
    {
        FocusableProperty.OverrideMetadata(typeof(IsolatedSeekBar), new FrameworkPropertyMetadata(false));
        Control.IsTabStopProperty.OverrideMetadata(typeof(IsolatedSeekBar), new FrameworkPropertyMetadata(false));
    }

    /// <summary>背景色</summary>
    public Brush Background
    {
        get => (Brush)this.GetValue(BackgroundProperty);
        set => this.SetValue(BackgroundProperty, value);
    }

    /// <summary><seealso cref="Background"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(IsolatedSeekBar), new PropertyMetadata(null));

    /// <summary>枠線色</summary>
    public Brush BorderBrush
    {
        get => (Brush)this.GetValue(BorderBrushProperty);
        set => this.SetValue(BorderBrushProperty, value);
    }

    /// <summary><seealso cref="BorderBrush"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(IsolatedSeekBar), new PropertyMetadata(null));

    /// <summary>枠線のサイズ</summary>
    public Thickness BorderThickness
    {
        get => (Thickness)this.GetValue(BorderThicknessProperty);
        set => this.SetValue(BorderThicknessProperty, value);
    }

    /// <summary><seealso cref="BorderThickness"/>の依存関係プロパティ</summary>
    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(nameof(BorderThickness), typeof(Thickness), typeof(IsolatedSeekBar),
            new PropertyMetadata(new Thickness(1.0d, 0.0d, 1.0d, 0.0d)));

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
        var dpi = this._windowDdpi;
        var window = this._window;

        (double w, double h) = (this.Width, this.Height);
        if (double.IsNaN(h))
            h = this.ActualHeight;

        // TODO: ディスプレイスケーリングが150%の時にBackgroundが描画されなくなるためWidthは一旦考慮しない
        window.Width = w;
        window.Height = Math.Round(h / dpi.DpiScaleY);
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
        window.DpiChanged += this.OnWindowDpiChanged;

        this._windowDdpi = VisualTreeHelper.GetDpi(window);

        RenderOptions.SetBitmapScalingMode(window, BitmapScalingMode.NearestNeighbor);

        // 各値をバインド
        this.SetBinding(window, Window.TopProperty, TopProperty);
        this.SetBinding(window, Window.LeftProperty, LeftProperty);
        this.SetBinding(window, Window.BackgroundProperty, BackgroundProperty);
        this.SetBinding(window, Window.BorderBrushProperty, BorderBrushProperty);
        this.SetBinding(window, Window.BorderThicknessProperty, BorderThicknessProperty);

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
    /// ウィンドウのDPI変更時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnWindowDpiChanged(object sender, DpiChangedEventArgs e)
    {
        this._windowDdpi = e.NewDpi;
    }

    /// <summary>
    /// コントロール間でプロパティをバインドする。
    /// </summary>
    /// <param name="element"></param>
    /// <param name="destProperty"></param>
    /// <param name="srcProperty"></param>
    private void SetBinding(FrameworkElement element, DependencyProperty destProperty, DependencyProperty srcProperty)
        => element.SetBinding(destProperty, new Binding(srcProperty.Name) { Source = this, Mode = BindingMode.OneWay });

    /// <summary>
    /// 表示する
    /// </summary>
    /// <param name="left">横位置</param>
    /// <param name="top">上位置</param>
    /// <param name="height">t赤さ</param>
    public void Show(double left, double top, double height)
    {
        using (this.Dispatcher.DisableProcessing())
        using (this._window.Dispatcher.DisableProcessing())
        {
            this.Left = left;
            this.Top = top;
            this.Height = height;

            if (!this.IsOpen)
                this.IsOpen = true;
        }
    }

    /// <summary>隠す</summary>
    public void Hide()
    {
        if (this.IsOpen)
            this.IsOpen = false;
    }
}
