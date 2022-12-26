using System.IO;
using MemoryPack;
using Quark.Data.Settings;

namespace Quark.Services;

internal class SettingService
{
    private readonly string _path;

    public Settings Settings { get; }

    public SettingService(string path)
    {
        this._path = path;
        this.Settings = Read(path);
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
