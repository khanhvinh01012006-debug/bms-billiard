using BMS.PlayerApp.Services;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace BMS.PlayerApp;

public partial class CuesPage : ContentPage
{
    private readonly CueApiService _cueApiService;
    private readonly ApiOptions _apiOptions;
    private readonly AuthState _authState;
    private HubConnection? _hubConnection;

    public CuesPage(CueApiService cueApiService, ApiOptions apiOptions, AuthState authState)
    {
        InitializeComponent();
        _cueApiService = cueApiService;
        _apiOptions = apiOptions;
        _authState = authState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StaffLabel.IsVisible = _authState.IsStaff;
        StaffRentalsSection.IsVisible = _authState.IsStaff;
        await LoadCuesAsync();
        if (_authState.IsStaff)
        {
            await LoadActiveRentalsAsync();
        }
        await ConnectHubAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    private async Task ConnectHubAsync()
    {
        if (_hubConnection is not null) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_apiOptions.BaseUrl}/hubs/bms")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<CueRentalDto>("CueRentalChanged", async _ =>
        {
            await LoadCuesAsync();
            if (_authState.IsStaff)
            {
                await LoadActiveRentalsAsync();
            }
        });

        try
        {
            await _hubConnection.StartAsync();
        }
        catch
        {
            // Real-time updates are best-effort; pull-to-refresh still works.
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadCuesAsync();
        if (_authState.IsStaff)
        {
            await LoadActiveRentalsAsync();
        }
        CuesRefreshView.IsRefreshing = false;
    }

    private async Task LoadActiveRentalsAsync()
    {
        var rentals = await _cueApiService.GetActiveRentalsAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ActiveRentalsCollectionView.ItemsSource = rentals;
        });
    }

    private async void OnReturnOkClicked(object? sender, EventArgs e) => await ReturnRentalAsync(sender, "Ok", null, null);

    private async void OnReturnDamagedClicked(object? sender, EventArgs e) =>
        await ReturnRentalAsync(sender, "Damaged", 50000m, "Cơ bị hư hỏng khi trả.");

    private async void OnReturnLostClicked(object? sender, EventArgs e) =>
        await ReturnRentalAsync(sender, "Lost", 300000m, "Khách làm mất cơ, đã lập biên bản đền tiền.");

    private async Task ReturnRentalAsync(object? sender, string outcome, decimal? penaltyAmount, string? note)
    {
        if (sender is not Button { CommandParameter: int rentalId }) return;

        var (success, error) = await _cueApiService.ReturnAsync(rentalId, outcome, penaltyAmount, note);
        await DisplayAlert("Trả cơ", success ? "Đã ghi nhận." : error, "OK");
        await LoadCuesAsync();
        await LoadActiveRentalsAsync();
    }

    private async Task LoadCuesAsync()
    {
        var cues = await _cueApiService.GetCuesAsync();
        var viewModels = cues.Select(c => new CueItemViewModel(c)).ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            CuesCollectionView.ItemsSource = viewModels;
        });
    }

    private async void OnRentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int cueId }) return;

        var name = string.IsNullOrWhiteSpace(CustomerNameEntry.Text) ? "Khách vãng lai" : CustomerNameEntry.Text;
        var (success, error) = await _cueApiService.RentAsync(cueId, name);

        await DisplayAlert("Thuê cơ", success ? "Thuê cơ thành công!" : error, "OK");
        await LoadCuesAsync();
    }

    private class CueItemViewModel
    {
        public CueItemViewModel(CueDto dto)
        {
            Id = dto.Id;
            Name = dto.Name;
            AvailableLabel = $"Còn {dto.AvailableCount} cơ";
            PriceLabel = $"Giá: {dto.PricePerHour:N0} đ/giờ";
            CanRent = dto.AvailableCount > 0;
        }

        public int Id { get; }
        public string Name { get; }
        public string AvailableLabel { get; }
        public string PriceLabel { get; }
        public bool CanRent { get; }
    }
}
