using System.Collections.ObjectModel;

namespace StepikAnalyticsDesktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<NavigationSection> Sections { get; }
    private NavigationSection _selectedSection;

    public MainWindowViewModel(
        CoursesViewModel coursesViewModel,
        DashboardViewModel dashboardViewModel,
        SyncViewModel syncViewModel,
        SettingsViewModel settingsViewModel)
    {
        Sections = new ObservableCollection<NavigationSection>
        {
            new("Курсы", coursesViewModel),
            new("Дашборд", dashboardViewModel),
            new("Синхронизация", syncViewModel),
            new("Настройки", settingsViewModel)
        };
        _selectedSection = Sections[0];
    }

    public NavigationSection SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }
}

public sealed class NavigationSection
{
    public NavigationSection(string title, ViewModelBase viewModel)
    {
        Title = title;
        ViewModel = viewModel;
    }

    public string Title { get; }
    public ViewModelBase ViewModel { get; }
}
