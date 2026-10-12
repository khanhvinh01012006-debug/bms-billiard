using BMS.PlayerApp.Services;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace BMS.PlayerApp;

public partial class MainPage : ContentPage
{
    private readonly TableApiService _tableApiService;
    private readonly ApiOptions _apiOptions;
    private readonly AuthState _authState;
    private HubConnection? _hubConnection;

    public MainPage(TableApiService tableApiService, ApiOptions apiOptions, AuthState authState)
    {
        InitializeComponent();
        _tableApiService = tableApiService;
        _apiOptions = apiOptions;
        _authState = authState;
        _authState.Changed += OnAuthChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RefreshAuthUi();
        await LoadTablesAsync();
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

    private void OnAuthChanged()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            RefreshAuthUi();
            await LoadTablesAsync();
        });
    }

    private void RefreshAuthUi()
    {
        if (_authState.IsAuthenticated)
        {
            AuthButton.Text = "Đăng xuất";
            AuthStatusLabel.IsVisible = true;
            AuthStatusLabel.Text = _authState.IsStaff
                ? $"Nhân viên: {_authState.FullName}"
                : $"Xin chào, {_authState.FullName}";
            CustomerNameEntry.IsVisible = !_authState.IsStaff;
            VerifyPhoneButton.IsVisible = true;
        }
        else
        {
            AuthButton.Text = "Đăng nhập";
            AuthStatusLabel.IsVisible = false;
            CustomerNameEntry.IsVisible = true;
            VerifyPhoneButton.IsVisible = false;
        }
    }

    private async void OnVerifyPhoneClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("verify-phone");
    }

    private async void OnAuthButtonClicked(object? sender, EventArgs e)
    {
        if (_authState.IsAuthenticated)
        {
            _authState.SignOut();
            RefreshAuthUi();
            await LoadTablesAsync();
        }
        else
        {
            await Shell.Current.GoToAsync("login");
        }
    }

    private async Task ConnectHubAsync()
    {
        if (_hubConnection is not null) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_apiOptions.BaseUrl}/hubs/bms")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<TableStatusChangedEvent>("TableStatusChanged", async _ =>
        {
            await LoadTablesAsync();
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
        await LoadTablesAsync();
        TablesRefreshView.IsRefreshing = false;
    }

    private async Task LoadTablesAsync()
    {
        var tables = await _tableApiService.GetTablesAsync();
        var viewModels = tables.Select(t => new TableItemViewModel(t, _authState.IsStaff)).ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TablesCollectionView.ItemsSource = viewModels;
        });
    }

    private async void OnBookTableClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int tableId }) return;

        var name = string.IsNullOrWhiteSpace(CustomerNameEntry.Text) ? "Khách vãng lai" : CustomerNameEntry.Text;
        var (success, error) = await _tableApiService.CreateBookingAsync(tableId, name);

        await DisplayAlert("Đặt bàn", success ? "Đặt bàn thành công!" : error, "OK");
        await LoadTablesAsync();
    }

    private async void OnCheckInClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int tableId }) return;

        var name = string.IsNullOrWhiteSpace(CustomerNameEntry.Text) ? "Khách vãng lai" : CustomerNameEntry.Text;
        var (success, error) = await _tableApiService.CheckInAsync(tableId, name);

        await DisplayAlert("Nhận bàn", success ? "Đã nhận bàn." : error, "OK");
        await LoadTablesAsync();
    }

    private async void OnCheckOutClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int tableId }) return;

        var (success, result, error) = await _tableApiService.CheckOutAsync(tableId);
        var message = success && result is not null
            ? $"Thời gian: {result.Duration:hh\\:mm\\:ss}, Thành tiền: {result.TotalAmount:N0} đ"
            : error;

        await DisplayAlert("Trả bàn", message, "OK");
        await LoadTablesAsync();
    }

    private class TableItemViewModel
    {
        public TableItemViewModel(TableDto dto, bool isStaff)
        {
            Id = dto.Id;
            Name = dto.Name;
            var isAvailable = dto.Status == TableStatus.Available;
            ShowBookButton = isAvailable && !isStaff;
            ShowCheckInButton = isAvailable && isStaff;
            ShowCheckOutButton = dto.Status == TableStatus.Occupied && isStaff;
            StatusLabel = dto.Status switch
            {
                TableStatus.Available => "Trống",
                TableStatus.Occupied => "Đang chơi",
                TableStatus.Reserved => "Đã đặt",
                TableStatus.Maintenance => "Bảo trì",
                _ => dto.Status.ToString()
            };
            CapacityLabel = $"Sức chứa: {dto.Capacity} người";
            PriceLabel = $"Giá: {dto.PricePerHour:N0} đ/giờ";
        }

        public int Id { get; }
        public string Name { get; }
        public bool ShowBookButton { get; }
        public bool ShowCheckInButton { get; }
        public bool ShowCheckOutButton { get; }
        public string StatusLabel { get; }
        public string CapacityLabel { get; }
        public string PriceLabel { get; }
    }
}
