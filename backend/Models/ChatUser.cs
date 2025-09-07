namespace PrivateChatApp.Api.Models
{
    public class ChatUser
    {
        public string UserId { get; set; } = Guid.NewGuid().ToString();
        public string ConnectionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; } = true;
    }
}