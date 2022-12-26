using System.Collections.Generic;
using Quark.Models.Neutrino;
using Quark.Mvvm;
using Quark.Services;

namespace Quark.ViewModels;

internal class MainWindowViewModel : ViewModelBase
{
    private NeutrinoService _neutrino;

    public IList<ModelInfo> Models { get; }

    public MainWindowViewModel(NeutrinoService neutrino) : base()
    {
        this._neutrino = neutrino;
        this.Models = neutrino.GetModels();
    }
}
