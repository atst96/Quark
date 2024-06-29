using System;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;

namespace Quark.Renderers;

internal abstract class DynamicsRenderer : RendererBase
{
    public static DynamicsRenderer Create(INeutrinoTrack track)
        => track switch
        {
            NeutrinoV1Track v1 => new MgcDynamicsRenderer<NeutrinoV1Phrase, double>(v1),
            NeutrinoV2Track v2 => new MspecDynamicsDirectRenderer<NeutrinoV2Phrase, float>(v2),
            _ => throw new NotSupportedException()
        };
}
