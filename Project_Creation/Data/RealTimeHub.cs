using Microsoft.AspNetCore.SignalR;

namespace Project_Creation.Data
{
    public class RealTimeHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        public async Task SendNotification(string user, string notification)
        {
            await Clients.All.SendAsync("ReceiveNotification", user, notification);
        }
        public async Task SendChatMessage(string sender, string receiver, string message)
        {
            await Clients.User(receiver).SendAsync("ReceiveChatMessage", sender, message);
        }
    }
}
