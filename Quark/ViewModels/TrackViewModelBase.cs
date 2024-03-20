using Quark.Mvvm;

namespace Quark.ViewModels;

public abstract class TrackViewModelBase : ViewModelBase
{
    internal ProjectViewModel ProjectVeiwMdoel { get; }

    internal TrackViewModelBase(ProjectViewModel projectViewModel)
    {
        this.ProjectVeiwMdoel = projectViewModel;
    }
}
