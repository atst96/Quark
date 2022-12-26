using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Quark.Data.Settings;
using Quark.Models.Neutrino;
using Quark.Utils;

namespace Quark.Services;

internal class NeutrinoService
{
    private Settings _setting;

    private const string BinDirName = "bin";
    private const string ModelDirName = "model";

    public NeutrinoService(SettingService settingService)
    {
        this._setting = settingService.Settings;
    }

    public IList<ModelInfo> GetModels()
    {
        var path = Path.Combine(this._setting.Neutrino.Directory!, ModelDirName);

        return NeutrinoModelUtil.GetModels(path);
    }
}
