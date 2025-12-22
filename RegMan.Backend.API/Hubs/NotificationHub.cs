using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RegMan.Backend.API.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
