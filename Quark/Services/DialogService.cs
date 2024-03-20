using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Quark.DependencyInjection;
using Quark.ViewModels;
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

    private static Task<IStorageFolder?> GetFolder(IStorageProvider storageProvider, string? path)
    {
        if (path == null)
            return Task.FromResult<IStorageFolder?>(null);

        return storageProvider.TryGetFolderFromPathAsync(path);
    }

    private static string? GetSingleLocalPath<T>(IReadOnlyList<T>? item)
        where T : IStorageItem
    {
        // TODO: IStorageItemの解放は必要?
        return item?.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SelectFolderAsync(string? title = null, string? initialDirectory = null)
    {
        var storageProvider = this.GetStorageProvider();
        var startupLocation = await GetFolder(storageProvider, initialDirectory).ConfigureAwait(false);


        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startupLocation,
        })
        .ConfigureAwait(false);

        return GetSingleLocalPath(result);
    }

    public async Task<string?> SelectOpenFileAsync(string? title = null, IReadOnlyList<FilePickerFileType>? fileTypeFilters = null, string? initialDirectory = null)
    {
        var storageProvider = this.GetStorageProvider();
        var startupLocation = await GetFolder(storageProvider, initialDirectory).ConfigureAwait(false);

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startupLocation,
            FileTypeFilter = fileTypeFilters,

        })
        .ConfigureAwait(false);

        return GetSingleLocalPath(result);
    }

    public async Task<string?> SelectSaveFileAsync(string? title = null, IReadOnlyList<FilePickerFileType>? fileTypeFilters = null, string? initialDirectory = null)
    {
        var storageProvider = this.GetStorageProvider();
        var startupLocation = await GetFolder(storageProvider, initialDirectory).ConfigureAwait(false);


        var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedStartLocation = startupLocation,
            FileTypeChoices = fileTypeFilters,

        })
        .ConfigureAwait(false);

        return result?.TryGetLocalPath();
    }

    internal Task ImportMusicXmlAsync(MusicXMLImportWindowViewModel viewModel)
        => Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = this._window!;

            viewModel.DialogService.SetOwner(owner);

            Window? window = null;
            try
            {
                window = new MusicXMLImportWindow()
                {
                    DataContext = viewModel
                };

                viewModel.DialogService.SetOwner(window);

                await window.ShowDialog(owner).ConfigureAwait(false);
            }
            finally
            {
                if (window != null)
                    viewModel.DialogService.UnregisterOwner(window);
            }
        });

    internal Task ShowProgressDialog(ProgressWindowViewModel viewModel)
        => Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = this._window!;

            viewModel.DialogService.SetOwner(owner);

            Window? window = null;
            try
            {
                window = new ProgressWindow()
                {
                    DataContext = viewModel
                };

                viewModel.DialogService.SetOwner(window);

                await window.ShowDialog(owner).ConfigureAwait(false);
            }
            finally
            {
                if (window != null)
                    viewModel.DialogService.UnregisterOwner(window);
            }
        });

    public void Close()
    {
        this._window?.Close();
    }
}
