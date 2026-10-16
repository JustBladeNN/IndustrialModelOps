using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ModelOps.Application;
using ModelOps.Domain;

namespace ModelOps.Desktop;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ModelOpsApiClient _apiClient;
    private string _apiBaseUrl = "http://localhost:5080/";
    private string _newProjectName = "Plant model";
    private int _elementCount = 10_000;
    private ProjectResponse? _selectedProject;
    private string _statusMessage = "Start PostgreSQL and the API, then load projects.";
    private int _progressPercent;
    private bool _isBusy;

    public MainViewModel(ModelOpsApiClient apiClient)
    {
        _apiClient = apiClient;
        LoadProjectsCommand = new AsyncRelayCommand(LoadProjectsAsync, () => !IsBusy);
        CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync, () => !IsBusy);
        GenerateElementsCommand = new AsyncRelayCommand(
            GenerateElementsAsync,
            () => !IsBusy && SelectedProject is not null);
        StartCheckCommand = new AsyncRelayCommand(
            StartCheckAsync,
            () => !IsBusy && SelectedProject is not null);
    }

    public ObservableCollection<ProjectResponse> Projects { get; } = [];
    public ObservableCollection<IssueResponse> Issues { get; } = [];

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetField(ref _apiBaseUrl, value);
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set => SetField(ref _newProjectName, value);
    }

    public int ElementCount
    {
        get => _elementCount;
        set => SetField(ref _elementCount, value);
    }

    public ProjectResponse? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetField(ref _selectedProject, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ICommand LoadProjectsCommand { get; }
    public ICommand CreateProjectCommand { get; }
    public ICommand GenerateElementsCommand { get; }
    public ICommand StartCheckCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task LoadProjectsAsync()
    {
        await ExecuteAsync(async cancellationToken =>
        {
            ConfigureClient();
            var projects = await _apiClient.GetProjectsAsync(cancellationToken);
            Projects.Clear();
            foreach (var project in projects)
            {
                Projects.Add(project);
            }

            SelectedProject = Projects.FirstOrDefault();
            StatusMessage = $"Loaded {Projects.Count} project(s).";
        });
    }

    private async Task CreateProjectAsync()
    {
        await ExecuteAsync(async cancellationToken =>
        {
            ConfigureClient();
            var project = await _apiClient.CreateProjectAsync(NewProjectName, cancellationToken);
            Projects.Insert(0, project);
            SelectedProject = project;
            StatusMessage = $"Project '{project.Name}' created.";
        });
    }

    private async Task GenerateElementsAsync()
    {
        var selected = SelectedProject!;
        await ExecuteAsync(async cancellationToken =>
        {
            ConfigureClient();
            StatusMessage = $"Generating {ElementCount:N0} model elements...";
            await _apiClient.GenerateElementsAsync(selected.Id, ElementCount, cancellationToken);
            StatusMessage = $"Generated {ElementCount:N0} model elements in PostgreSQL.";
            await RefreshSelectedProjectAsync(selected.Id, cancellationToken);
        });
    }

    private async Task StartCheckAsync()
    {
        var selected = SelectedProject!;
        await ExecuteAsync(async cancellationToken =>
        {
            ConfigureClient();
            Issues.Clear();
            ProgressPercent = 0;
            var run = await _apiClient.StartCheckAsync(selected.Id, cancellationToken);
            StatusMessage = $"Validation {run.Id} queued.";

            while (run.Status is ValidationRunStatus.Pending or ValidationRunStatus.Running)
            {
                await Task.Delay(350, cancellationToken);
                run = await _apiClient.GetRunAsync(run.Id, cancellationToken);
                ProgressPercent = run.ProgressPercent;
                StatusMessage = $"Checking model: {run.ProcessedElements:N0}/{run.TotalElements:N0}";
            }

            if (run.Status == ValidationRunStatus.Failed)
            {
                throw new InvalidOperationException(run.Error ?? "Validation failed.");
            }

            var issues = await _apiClient.GetIssuesAsync(run.Id, cancellationToken);
            foreach (var issue in issues.Items)
            {
                Issues.Add(issue);
            }

            ProgressPercent = 100;
            StatusMessage = $"Completed: {run.IssueCount:N0} issue(s). Showing first {Issues.Count}.";
        });
    }

    private async Task RefreshSelectedProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var projects = await _apiClient.GetProjectsAsync(cancellationToken);
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        SelectedProject = Projects.FirstOrDefault(x => x.Id == projectId);
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            IsBusy = true;
            await operation(CancellationToken.None);
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ConfigureClient() => _apiClient.SetBaseAddress(ApiBaseUrl);

    private void RaiseCommandStates()
    {
        foreach (var command in new[]
                 {
                     LoadProjectsCommand,
                     CreateProjectCommand,
                     GenerateElementsCommand,
                     StartCheckCommand
                 }.OfType<AsyncRelayCommand>())
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
