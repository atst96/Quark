using System;
using System.Collections.Generic;
using MemoryPack;
using Quark.Data.Projects.Tracks;

namespace Quark.Data.Project;

[MemoryPackable]
internal partial class ProjectConfig
{
    public string Name { get; set; }

    [Obsolete("Deprecated", false)]
    public string? Directory { get; set; }

    public IEnumerable<TrackBaseConfig> Tracks { get; init; }

    [MemoryPackConstructor]
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    private ProjectConfig() { }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public ProjectConfig(string name, IEnumerable<TrackBaseConfig> tracks)
    {
        this.Name = name;
        this.Tracks = tracks;
    }
}
