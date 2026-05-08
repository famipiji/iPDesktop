using iPDesktop.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace iPDesktop;

public partial class ProfileSettingPage : ContentPage
{
    private static readonly string _profilesPath =
        Path.Combine(FileSystem.AppDataDirectory, "profiles.json");

    private readonly ObservableCollection<ProfileClass> _classes = [];
    private readonly ObservableCollection<string> _properties = [];
    private ProfileClass? _selected;

    public ProfileSettingPage()
    {
        InitializeComponent();
        ClassListView.ItemsSource    = _classes;
        PropertiesListView.ItemsSource = _properties;
        ClassModePicker.SelectedIndex  = 0;
        ClassTypePicker.SelectedIndex  = 0;
        LoadProfiles();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void LoadProfiles()
    {
        try
        {
            if (!File.Exists(_profilesPath)) return;
            var list = JsonSerializer.Deserialize<List<ProfileClass>>(
                File.ReadAllText(_profilesPath));
            if (list is null) return;
            foreach (var c in list) _classes.Add(c);
        }
        catch { }
    }

    private void SaveProfiles()
    {
        try
        {
            File.WriteAllText(_profilesPath,
                JsonSerializer.Serialize(_classes.ToList()));
        }
        catch { }
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────

    private void OnTabClassTapped(object? sender, TappedEventArgs e)     => SetTab(0);
    private void OnTabPropertiesTapped(object? sender, TappedEventArgs e) => SetTab(1);
    private void OnTabValueListTapped(object? sender, TappedEventArgs e)  => SetTab(2);

    private void SetTab(int index)
    {
        ClassTabContent.IsVisible        = index == 0;
        PropertiesTabContent.IsVisible   = index == 1;
        ValueListTabContent.IsVisible    = index == 2;

        TabClassBorder.BackgroundColor      = index == 0 ? Colors.White : Color.FromArgb("#E0E0E0");
        TabPropertiesBorder.BackgroundColor = index == 1 ? Colors.White : Color.FromArgb("#E0E0E0");
        TabValueListBorder.BackgroundColor  = index == 2 ? Colors.White : Color.FromArgb("#E0E0E0");
    }

    // ── Class list ────────────────────────────────────────────────────────────

    private async void OnAddClassClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Add Class", "Class name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        _classes.Add(new ProfileClass { Name = name.Trim() });
    }

    private void OnRemoveClassClicked(object? sender, EventArgs e)
    {
        if (_selected is null) return;
        _classes.Remove(_selected);
        _selected = null;
        UpdateForm(null);
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selected = e.CurrentSelection.FirstOrDefault() as ProfileClass;
        UpdateForm(_selected);
    }

    private void UpdateForm(ProfileClass? pc)
    {
        bool on = pc is not null;

        SelectedClassLabel.Text           = on ? pc!.Name : "<Not Selected>";
        SelectedClassLabel.TextColor      = on ? Colors.Black : Color.FromArgb("#888888");
        SelectedClassLabel.FontAttributes = on ? FontAttributes.None : FontAttributes.Italic;

        ClassModePicker.IsEnabled    = on;
        ClassIdEntry.IsEnabled       = on;
        ClassNameEntry.IsEnabled     = on;
        ClassTypePicker.IsEnabled    = on;
        AddPropertiesBtn.IsEnabled   = on;
        RemovePropertiesBtn.IsEnabled = on;
        SetupExportBtn.IsEnabled     = on;

        _properties.Clear();

        if (pc is null)
        {
            ClassModePicker.SelectedIndex = 0;
            ClassIdEntry.Text             = "";
            ClassNameEntry.Text           = "";
            ClassTypePicker.SelectedIndex = 0;
        }
        else
        {
            ClassModePicker.SelectedIndex = Math.Max(0, ClassModePicker.Items.IndexOf(pc.Mode));
            ClassIdEntry.Text             = pc.Id;
            ClassNameEntry.Text           = pc.Name;
            ClassTypePicker.SelectedIndex = Math.Max(0, ClassTypePicker.Items.IndexOf(pc.Type));
            foreach (var p in pc.Properties) _properties.Add(p);
        }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    private async void OnAddPropertyClicked(object? sender, EventArgs e)
    {
        if (_selected is null) return;
        var name = await DisplayPromptAsync("Add Property", "Property name:");
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = name.Trim();
        _selected.Properties.Add(trimmed);
        _properties.Add(trimmed);
    }

    private void OnRemovePropertyClicked(object? sender, EventArgs e)
    {
        if (_selected is null) return;
        if (PropertiesListView.SelectedItem is not string sel) return;
        _selected.Properties.Remove(sel);
        _properties.Remove(sel);
    }

    private async void OnSetupExportClicked(object? sender, EventArgs e)
        => await DisplayAlertAsync("Export", "Export configuration coming soon.", "OK");

    // ── Bottom buttons ────────────────────────────────────────────────────────

    private void CommitForm()
    {
        if (_selected is null) return;
        _selected.Name = ClassNameEntry.Text?.Trim() ?? _selected.Name;
        _selected.Id   = ClassIdEntry.Text?.Trim()   ?? _selected.Id;
        _selected.Mode = ClassModePicker.SelectedItem as string ?? "Offline";
        _selected.Type = ClassTypePicker.SelectedItem as string ?? "Document";
    }

    private async void OnSaveCloseClicked(object? sender, EventArgs e)
    {
        CommitForm();
        SaveProfiles();
        await Navigation.PopModalAsync();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();
}
