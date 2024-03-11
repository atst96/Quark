using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Quark.Controls;

/// <summary>
/// 画面外配置のシークバーコントロール
/// </summary>
public class IsolatedSeekBar : Control
{
    /// <summary>ウィンドウ</summary>
    private Window _window;
    ///// <summary><see cref="_window"/>のDPI</summary>
    //private DpiScale _windowDdpi;

    static IsolatedSeekBar()
    {
        FocusableProperty.OverrideDefaultValue<IsolatedSeekBar>(false);
        IsTabStopProperty.OverrideDefaultValue<IsolatedSeekBar>(false);
    }

    /// <summary>背景色</summary>
    public IBrush? Background
    {
        get => this.GetValue(BackgroundProperty);
        set => this.SetValue(BackgroundProperty, value);
    }

    /// <summary><seealso cref="Background"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<IsolatedSeekBar, IBrush?>(nameof(Background));

    /// <summary>枠線色</summary>
    public IBrush? BorderBrush
    {
        get => this.GetValue(BorderBrushProperty);
        set => this.SetValue(BorderBrushProperty, value);
    }

    /// <summary><seealso cref="BorderBrush"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = AvaloniaProperty.Register<IsolatedSeekBar, IBrush?>(nameof(BorderBrush));

    /// <summary>枠線のサイズ</summary>
    public Thickness BorderThickness
    {
        get => this.GetValue(BorderThicknessProperty);
        set => this.SetValue(BorderThicknessProperty, value);
    }

    /// <summary><seealso cref="BorderThickness"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<IsolatedSeekBar, Thickness>(nameof(BorderThickness), defaultValue: new Thickness(1.0d, 0.0d, 1.0d, 0.0d));

    /// <summary>
    /// オーナーウィンドウ
    /// </summary>
    public Window? Owner
    {
        get => this.GetValue(OwnerProperty);
        set => this.SetValue(OwnerProperty, value);
    }

    /// <summary><see cref="Owner"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<Window?> OwnerProperty = AvaloniaProperty.Register<IsolatedSeekBar, Window?>(nameof(Owner));

    /// <summary>表示／非表示フラグ</summary>
    public bool IsOpen
    {
        get => (bool)this.GetValue(IsOpenProperty);
        set => this.SetValue(IsOpenProperty, value);
    }

    /// <summary><see cref="IsOpen"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<IsolatedSeekBar, bool>(nameof(IsOpen));

    /// <summary>
    /// <see cref="IsOpen"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private void OnIsOpenPropertyChanged(bool newValue)
    {
        if (this.IsVisible != newValue)
        {
            if (!newValue)
                this._window.Show(this.Owner! ?? (TopLevel.GetTopLevel(this) as Window)!);
            else
                this._window.Hide();
        }
    }

    /// <summary>上位置</summary>
    public int Top
    {
        get => this.GetValue(TopProperty);
        set => this.SetValue(TopProperty, value);
    }

    /// <summary><see cref="Top"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<int> TopProperty = AvaloniaProperty.Register<IsolatedSeekBar, int>(nameof(Top));

    /// <summary>
    /// <see cref="Top"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private void OnTopPropertyChanged(int newValue)
    {
        var w = this._window;

        w.Position = new(w.Position.X, newValue);
    }

    /// <summary>左位置</summary>
    public int Left
    {
        get => this.GetValue(LeftProperty);
        set => this.SetValue(LeftProperty, value);
    }

    /// <summary><see cref="Left"/>の依存関係プロパティ</summary>
    public static readonly StyledProperty<int> LeftProperty = AvaloniaProperty.Register<IsolatedSeekBar, int>(nameof(Left));

    /// <summary>
    /// <see cref="Left"/>プロパティ変更時
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private void OnLeftPropertyChanged(int newValue)
    {
        var w = this._window;
        w.Position = new(newValue, w.Position.Y);
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
        // var dpi = this._windowDdpi;
        var window = this._window;

        (double w, double h) = (this.Width, this.Height);
        if (double.IsNaN(h))
            h = this.Bounds.Height;

        // TODO: ディスプレイスケーリングが150%の時にBackgroundが描画されなくなるためWidthは一旦考慮しない
        window.Width = w;
        // window.Height = Math.Round(h / dpi.DpiScaleY);
        window.Height = Math.Round(h);
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
            SystemDecorations = SystemDecorations.None,
            CanResize = false,
            //WindowStyle = WindowStyle.None,
            //ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            IsHitTestVisible = false,
            // AllowsTransparency = true,
            Background = Brushes.Transparent,
            UseLayoutRounding = true,
            // SnapsToDevicePixels = true,
            Width = this.Width,
            ShowActivated = false,
        };

        window.Initialized += this.OnChildWindowSourceInitialized;
        window.GotFocus += this.OnChildWindowFocus;
        // window.DpiChanged += this.OnWindowDpiChanged;

        //this._windowDdpi = VisualTreeHelper.GetDpi(window);

        //RenderOptions.SetBitmapScalingMode(window, BitmapScalingMode.NearestNeighbor);

        // 各値をバインド
        this.SetBinding(window, Window.BackgroundProperty, BackgroundProperty);
        this.SetBinding(window, Window.BorderBrushProperty, BorderBrushProperty);
        this.SetBinding(window, Window.BorderThicknessProperty, BorderThicknessProperty);

        return window;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        this._window.Width = this.Bounds.Width;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        if (e.WidthChanged)
            this._window.Width = e.NewSize.Width;
    }

    /// <summary>
    /// 生成したウィンドウ初期化時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnChildWindowSourceInitialized(object? sender, EventArgs e)
    {
        var window = this._window;
        window.Initialized -= this.OnChildWindowSourceInitialized;

        // HACK: ウィンドウにフォーカスが移動しないようにする
        //nint hwnd = new WindowInteropHelper(window).Handle;

        //// マウス押下時にフォーカスが移動しないようにする
        //uint style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        //_ = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE);
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
        // Window.GetWindow(this)?.Focus();
        TopLevel.GetTopLevel(this)?.Focus();
    }

    ///// <summary>
    ///// ウィンドウのDPI変更時
    ///// </summary>
    ///// <param name="sender"></param>
    ///// <param name="e"></param>
    //private void OnWindowDpiChanged(object sender, DpiChangedEventArgs e)
    //{
    //    this._windowDdpi = e.NewDpi;
    //}

    /// <summary>
    /// コントロール間でプロパティをバインドする。
    /// </summary>
    /// <param name="element"></param>
    /// <param name="destProperty"></param>
    /// <param name="srcProperty"></param>
    private void SetBinding<T>(Control element, AvaloniaProperty<T> destProperty, AvaloniaProperty<T> srcProperty)
        => element.Bind(destProperty, new Binding(srcProperty.Name) { Source = this, Mode = BindingMode.OneWay });

    /// <summary>
    /// 表示する
    /// </summary>
    /// <param name="left">横位置</param>
    /// <param name="top">上位置</param>
    /// <param name="height">t赤さ</param>
    public void Show(int left, int top, double height)
    {
        //using (this.Dispatcher.DisableProcessing())
        //using (this._window.Dispatcher.DisableProcessing())
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        var proeprty = change.Property;


        if (proeprty == IsOpenProperty)
        {
            this.OnIsOpenPropertyChanged(change.GetNewValue<bool>());
        }
        else if (proeprty == LeftProperty)
        {
            this.OnLeftPropertyChanged(change.GetNewValue<int>());
        }
        else if (proeprty == TopProperty)
        {
            this.OnTopPropertyChanged(change.GetNewValue<int>());
        }
    }
}
