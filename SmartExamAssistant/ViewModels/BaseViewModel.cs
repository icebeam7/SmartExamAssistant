using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartExamAssistant.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        string title;

        [ObservableProperty]
        bool isBusy;
    }
}
