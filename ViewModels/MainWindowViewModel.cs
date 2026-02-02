using System.Collections.ObjectModel;

namespace StepikAnalyticsDesktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<NavigationSection> Sections { get; }
    private NavigationSection _selectedSection;

    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        CoursesViewModel coursesViewModel)
    {
        Sections = new ObservableCollection<NavigationSection>
        {
            new("Статистика", dashboardViewModel),
            new("Добавление курсов", coursesViewModel)
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
