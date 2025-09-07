using System.Collections.Concurrent;

namespace PrivateChatApp.Api.Models
{
    public class PrivateConversation
    {
        public string ConversationId { get; set; } = string.Empty;
        public string User1Id { get; set; } = string.Empty;
        public string User1Username { get; set; } = string.Empty;
        public string User2Id { get; set; } = string.Empty;
        public string User2Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
        public ConcurrentQueue<ChatMessage> Messages { get; set; } = new();
        
        public bool ContainsUser(string userId)
        {
            return User1Id == userId || User2Id == userId;
        }
        
        public string GetOtherUserId(string userId)
        {
            return User1Id == userId ? User2Id : User1Id;
        }
        
        public string GetOtherUsername(string userId)
        {
            return User1Id == userId ? User2Username : User1Username;
        }
    }
}