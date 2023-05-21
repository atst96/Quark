namespace Quark.Behaviors;

/// <summary>
/// ウィンドウを閉じるイベントの要求情報
/// </summary>
public class WindowCloseRequest
{
    /// <summary>要求がキャンセルされたかを取得する</summary>
    public bool IsCancelled { get; private set; } = false;

    /// <summary>要求をキャンセルする</summary>
    public void Cancel() => this.IsCancelled = true;

    public override string ToString()
        => $"{nameof(this.IsCancelled)}: {this.IsCancelled}";
}
