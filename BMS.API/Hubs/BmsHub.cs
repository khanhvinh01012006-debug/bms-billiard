using Microsoft.AspNetCore.SignalR;

namespace BMS.API.Hubs;

/// <summary>
/// Empty hub used only as a server-to-client push channel. Clients connect and listen
/// for "TableStatusChanged", "MatchRequestChanged" and "CueRentalChanged" events, which
/// are broadcast by controllers AFTER the corresponding change has been saved to the DB.
/// </summary>
public class BmsHub : Hub
{
}
