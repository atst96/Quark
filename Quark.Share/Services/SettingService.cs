using MemoryPack;
using Quark.Data.Settings;
using Quark.DependencyInjection;
using Quark.Utils;

namespace Quark.Services;

[Singleton]
internal class SettingService
{
    private readonly string _path;

    public Settings Settings { get; }

    public SettingService()
    {
        var path = PathUtil.GetAbsolutePath(Config.SettingFile);
        this.Settings = Read(this._path = path);
    }

    private static Settings Read(string path)
    {
        try
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length > 0)
            {
                return MemoryPackSerializer.Deserialize<Settings>(data)!;
            }
        }
        catch (FileNotFoundException)
        {
            // pass
        }

        return new Settings();
    }

    public void Save()
    {
        File.WriteAllBytes(this._path, MemoryPackSerializer.Serialize(this.Settings));
    }
}
