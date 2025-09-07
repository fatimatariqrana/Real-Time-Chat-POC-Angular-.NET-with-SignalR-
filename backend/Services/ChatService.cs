using System.Collections.Concurrent;
using PrivateChatApp.Api.Models;

namespace PrivateChatApp.Api.Services
{
    public class ChatService : IChatService
    {
        private readonly ConcurrentDictionary<string, ChatUser> _usersByConnectionId = new();
        private readonly ConcurrentDictionary<string, ChatUser> _usersByUsername = new();
        private readonly ConcurrentDictionary<string, PrivateConversation> _conversations = new();
        private readonly object _lockObject = new();


        public async Task<ChatUser> AddUserAsync(string connectionId, string username)
        {
            var existingUser = await GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                await RemoveUserAsync(existingUser.ConnectionId);
            }

            var user = new ChatUser
            {
                ConnectionId = connectionId,
                Username = username,
                IsOnline = true
            };

            _usersByConnectionId.TryAdd(connectionId, user);
            _usersByUsername.TryAdd(username, user);

            return user;
        }

        public async Task RemoveUserAsync(string connectionId)
        {
            if (_usersByConnectionId.TryRemove(connectionId, out var user))
            {
                user.IsOnline = false;
                user.LastSeen = DateTime.UtcNow;
                _usersByUsername.TryRemove(user.Username, out _);
            }
            await Task.CompletedTask;
        }

        public async Task<ChatUser?> GetUserByConnectionIdAsync(string connectionId)
        {
            _usersByConnectionId.TryGetValue(connectionId, out var user);
            return await Task.FromResult(user);
        }

        public async Task<ChatUser?> GetUserByUsernameAsync(string username)
        {
            _usersByUsername.TryGetValue(username, out var user);
            return await Task.FromResult(user);
        }

        public async Task<List<ChatUser>> GetOnlineUsersAsync()
        {
            var onlineUsers = _usersByConnectionId.Values
                .Where(u => u.IsOnline)
                .ToList();
            return await Task.FromResult(onlineUsers);
        }

        public async Task UpdateUserLastSeenAsync(string userId)
        {
            var user = _usersByConnectionId.Values.FirstOrDefault(u => u.UserId == userId);
            if (user != null)
            {
                user.LastSeen = DateTime.UtcNow;
            }
            await Task.CompletedTask;
        }


        public async Task<PrivateConversation> GetOrCreateConversationAsync(string user1Id, string user2Id)
        {
            var conversationId = string.Compare(user1Id, user2Id) < 0 
                ? $"{user1Id}_{user2Id}" 
                : $"{user2Id}_{user1Id}";

            if (_conversations.TryGetValue(conversationId, out var existingConversation))
            {
                return existingConversation;
            }

            var user1 = _usersByConnectionId.Values.FirstOrDefault(u => u.UserId == user1Id);
            var user2 = _usersByConnectionId.Values.FirstOrDefault(u => u.UserId == user2Id);

            if (user1 == null || user2 == null)
            {
                throw new InvalidOperationException("One or both users not found");
            }

            var conversation = new PrivateConversation
            {
                ConversationId = conversationId,
                User1Id = user1.UserId,
                User1Username = user1.Username,
                User2Id = user2.UserId,
                User2Username = user2.Username
            };

            _conversations.TryAdd(conversationId, conversation);
            return await Task.FromResult(conversation);
        }

        public async Task<PrivateConversation?> GetConversationAsync(string conversationId)
        {
            _conversations.TryGetValue(conversationId, out var conversation);
            return await Task.FromResult(conversation);
        }

        public async Task<List<PrivateConversation>> GetUserConversationsAsync(string userId)
        {
            var userConversations = _conversations.Values
                .Where(c => c.ContainsUser(userId))
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            return await Task.FromResult(userConversations);
        }


        public async Task<ChatMessage> AddMessageAsync(string conversationId, string senderId, string receiverId, string message)
        {
            var conversation = await GetConversationAsync(conversationId);
            if (conversation == null)
            {
                throw new InvalidOperationException("Conversation not found");
            }

            var sender = _usersByConnectionId.Values.FirstOrDefault(u => u.UserId == senderId);
            var receiver = _usersByConnectionId.Values.FirstOrDefault(u => u.UserId == receiverId);

            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                SenderUsername = sender?.Username ?? "Unknown",
                ReceiverId = receiverId,
                ReceiverUsername = receiver?.Username ?? "Unknown",
                Message = message,
                IsDelivered = receiver?.IsOnline ?? false
            };

            conversation.Messages.Enqueue(chatMessage);
            conversation.LastMessageAt = DateTime.UtcNow;

            lock (_lockObject)
            {
                while (conversation.Messages.Count > 100)
                {
                    conversation.Messages.TryDequeue(out _);
                }
            }

            return chatMessage;
        }

        public async Task<List<ChatMessage>> GetConversationMessagesAsync(string conversationId, int limit = 50)
        {
            var conversation = await GetConversationAsync(conversationId);
            if (conversation == null)
            {
                return new List<ChatMessage>();
            }

            var messages = conversation.Messages.ToArray()
                .TakeLast(limit)
                .ToList();

            return messages;
        }

        public async Task MarkMessagesAsReadAsync(string conversationId, string userId)
        {
            var conversation = await GetConversationAsync(conversationId);
            if (conversation == null) return;

            var messages = conversation.Messages.ToArray();
            foreach (var message in messages.Where(m => m.ReceiverId == userId && !m.IsRead))
            {
                message.IsRead = true;
            }
        }

    }
}