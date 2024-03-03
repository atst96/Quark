using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Chrome;
using Avalonia.Platform.Storage;
using Quark.DependencyInjection;
using Quark.Views;

namespace Quark.Services;

[Prototype]
public class DialogService
{
    private Window? _window;

    public void SetOwner(Window window)
    {
        this._window = window;
    }

    public void UnregisterOwner(Window window)
    {
        if (this._window == window)
            this._window = null;
    }

    public Task ShowSettingWindow()
    {
        // MEMO: どのウィンドウからの呼び出しであっても、設定ウィンドウの親ウィンドウは常にメインウィンドウとする
        var owner = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow!;
        return new PreferenceWindow().ShowDialog(owner);
    }

    private IStorageProvider GetStorageProvider()
        => this._window?.StorageProvider ?? throw new Exception("StorageProvider not found.");

    public async Task<string?> SelectFolderAsync(string? title = null, string? startupFolder = null)
    {
        var storageProvider = this.GetStorageProvider();
        var startupFolder2 = startupFolder == null
            ? null
            : await storageProvider.TryGetFolderFromPathAsync(startupFolder).ConfigureAwait(false);


        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startupFolder2,
        })
        .ConfigureAwait(false);

        return result?.FirstOrDefault()?.TryGetLocalPath();
    }
}
