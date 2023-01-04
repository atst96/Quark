using System.Collections;
using System.Collections.Generic;
using MemoryPack;

namespace Quark.Data.Project;

[MemoryPackable]
internal partial class ProjectComposition
{
    public string Name { get; set; }
    public string Directory { get; set; }

    public LinkedList<TrackCompositionBase> Tracks { get; set; }

    [MemoryPackConstructor]
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    private ProjectComposition() { }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public ProjectComposition(string name, string directory)
    {
        this.Name = name;
        this.Directory = directory;
        this.Tracks = new LinkedList<TrackCompositionBase>();
    }
}
