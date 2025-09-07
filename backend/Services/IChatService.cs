using PrivateChatApp.Api.Models;

namespace PrivateChatApp.Api.Services
{
    public interface IChatService
    {
        Task<ChatUser> AddUserAsync(string connectionId, string username);
        Task RemoveUserAsync(string connectionId);
        Task<ChatUser?> GetUserByConnectionIdAsync(string connectionId);
        Task<ChatUser?> GetUserByUsernameAsync(string username);
        Task<List<ChatUser>> GetOnlineUsersAsync();
        Task UpdateUserLastSeenAsync(string userId);
        
        Task<PrivateConversation> GetOrCreateConversationAsync(string user1Id, string user2Id);
        Task<PrivateConversation?> GetConversationAsync(string conversationId);
        Task<List<PrivateConversation>> GetUserConversationsAsync(string userId);
        
        Task<ChatMessage> AddMessageAsync(string conversationId, string senderId, string receiverId, string message);
        Task<List<ChatMessage>> GetConversationMessagesAsync(string conversationId, int limit = 50);
        Task MarkMessagesAsReadAsync(string conversationId, string userId);
    }
}