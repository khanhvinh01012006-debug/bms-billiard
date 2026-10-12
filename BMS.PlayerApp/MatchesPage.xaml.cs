using BMS.PlayerApp.Services;
using BMS.Shared.Dtos;

namespace BMS.PlayerApp;

public partial class MatchesPage : ContentPage
{
    private readonly PlayerApiService _playerApiService;
    private List<PlayerProfileDto> _players = new();
    private int _selectedPlayerId;

    public MatchesPage(PlayerApiService playerApiService)
    {
        InitializeComponent();
        _playerApiService = playerApiService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPlayersAsync();
        await LoadSuggestionsAsync();
    }

    private async Task LoadPlayersAsync()
    {
        _players = await _playerApiService.GetPlayersAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlayerPicker.ItemsSource = _players.Select(p => $"{p.DisplayName} (ELO {p.EloRating})").ToList();
            if (_players.Count > 0)
            {
                PlayerPicker.SelectedIndex = 0;
                _selectedPlayerId = _players[0].Id;
            }
        });
    }

    private async void OnPlayerSelected(object? sender, EventArgs e)
    {
        if (PlayerPicker.SelectedIndex < 0 || PlayerPicker.SelectedIndex >= _players.Count) return;
        _selectedPlayerId = _players[PlayerPicker.SelectedIndex].Id;
        await LoadSuggestionsAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadSuggestionsAsync();
        SuggestionsRefreshView.IsRefreshing = false;
    }

    private async Task LoadSuggestionsAsync()
    {
        if (_selectedPlayerId <= 0) return;

        var suggestions = await _playerApiService.GetSuggestionsAsync(_selectedPlayerId);
        var viewModels = suggestions.Select(s => new SuggestionItemViewModel(s)).ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SuggestionsCollectionView.ItemsSource = viewModels;
        });
    }

    private async void OnInviteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int targetPlayerId }) return;

        var (success, error) = await _playerApiService.SendMatchRequestAsync(_selectedPlayerId, targetPlayerId);
        await DisplayAlert("Ghép trận", success ? "Đã gửi lời mời ghép trận." : error, "OK");
    }

    private class SuggestionItemViewModel
    {
        public SuggestionItemViewModel(PlayerProfileDto dto)
        {
            Id = dto.Id;
            DisplayName = dto.DisplayName;
            EloLabel = $"ELO {dto.EloRating}";
        }

        public int Id { get; }
        public string DisplayName { get; }
        public string EloLabel { get; }
    }
}
