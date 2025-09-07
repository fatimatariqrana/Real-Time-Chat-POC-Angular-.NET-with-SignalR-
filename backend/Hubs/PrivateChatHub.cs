using Microsoft.AspNetCore.SignalR;
using PrivateChatApp.Api.Models;
using PrivateChatApp.Api.Services;

namespace PrivateChatApp.Api.Hubs
{
    public class PrivateChatHub : Hub
    {
        private readonly IChatService _chatService;

        public PrivateChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task JoinChat(string username)
        {
            try
            {
                var user = await _chatService.AddUserAsync(Context.ConnectionId, username);
                
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.UserId}");
                
                var onlineUsers = await _chatService.GetOnlineUsersAsync();
                await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers.Select(u => new 
                {
                    u.UserId,
                    u.Username,
                    u.IsOnline,
                    u.LastSeen
                }));

                await Clients.Caller.SendAsync("UserJoined", new 
                {
                    user.UserId,
                    user.Username,
                    Message = "Successfully joined the chat!"
                });

                var conversations = await _chatService.GetUserConversationsAsync(user.UserId);
                await Clients.Caller.SendAsync("UserConversations", conversations.Select(c => new 
                {
                    c.ConversationId,
                    OtherUserId = c.GetOtherUserId(user.UserId),
                    OtherUsername = c.GetOtherUsername(user.UserId),
                    c.LastMessageAt
                }));
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to join chat: {ex.Message}");
            }
        }

        public async Task StartPrivateChat(string targetUsername)
        {
            try
            {
                var currentUser = await _chatService.GetUserByConnectionIdAsync(Context.ConnectionId);
                var targetUser = await _chatService.GetUserByUsernameAsync(targetUsername);

                if (currentUser == null)
                {
                    await Clients.Caller.SendAsync("Error", "You are not connected");
                    return;
                }

                if (targetUser == null)
                {
                    await Clients.Caller.SendAsync("Error", "Target user not found or offline");
                    return;
                }

                if (currentUser.UserId == targetUser.UserId)
                {
                    await Clients.Caller.SendAsync("Error", "Cannot start chat with yourself");
                    return;
                }

                var conversation = await _chatService.GetOrCreateConversationAsync(
                    currentUser.UserId, targetUser.UserId);

                var messages = await _chatService.GetConversationMessagesAsync(conversation.ConversationId);

                var conversationInfo = new
                {
                    conversation.ConversationId,
                    OtherUserId = targetUser.UserId,
                    OtherUsername = targetUser.Username,
                    Messages = messages.Select(m => new 
                    {
                        m.Id,
                        m.SenderId,
                        m.SenderUsername,
                        m.Message,
                        m.Timestamp,
                        m.IsRead
                    })
                };

                await Clients.Caller.SendAsync("PrivateChatStarted", conversationInfo);

                if (targetUser.IsOnline)
                {
                    var targetConversationInfo = new
                    {
                        conversation.ConversationId,
                        OtherUserId = currentUser.UserId,
                        OtherUsername = currentUser.Username,
                        Messages = messages.Select(m => new 
                        {
                            m.Id,
                            m.SenderId,
                            m.SenderUsername,
                            m.Message,
                            m.Timestamp,
                            m.IsRead
                        })
                    };

                    await Clients.Client(targetUser.ConnectionId).SendAsync("PrivateChatStarted", targetConversationInfo);
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to start private chat: {ex.Message}");
            }
        }

        public async Task SendPrivateMessage(string conversationId, string message)
        {
            try
            {
                var currentUser = await _chatService.GetUserByConnectionIdAsync(Context.ConnectionId);
                if (currentUser == null)
                {
                    await Clients.Caller.SendAsync("Error", "You are not connected");
                    return;
                }

                var conversation = await _chatService.GetConversationAsync(conversationId);
                if (conversation == null || !conversation.ContainsUser(currentUser.UserId))
                {
                    await Clients.Caller.SendAsync("Error", "Conversation not found or access denied");
                    return;
                }

                var receiverId = conversation.GetOtherUserId(currentUser.UserId);
                var receiver = await _chatService.GetUserByUsernameAsync(conversation.GetOtherUsername(currentUser.UserId));

                var chatMessage = await _chatService.AddMessageAsync(
                    conversationId, currentUser.UserId, receiverId, message);

                var messageData = new
                {
                    chatMessage.Id,
                    chatMessage.SenderId,
                    chatMessage.SenderUsername,
                    chatMessage.Message,
                    chatMessage.Timestamp,
                    ConversationId = conversationId
                };

                await Clients.Caller.SendAsync("MessageSent", messageData);

                if (receiver != null && receiver.IsOnline)
                {
                    await Clients.Client(receiver.ConnectionId).SendAsync("PrivateMessageReceived", messageData);
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
            }
        }

        public async Task MarkMessagesAsRead(string conversationId)
        {
            try
            {
                var currentUser = await _chatService.GetUserByConnectionIdAsync(Context.ConnectionId);
                if (currentUser != null)
                {
                    await _chatService.MarkMessagesAsReadAsync(conversationId, currentUser.UserId);
                    await Clients.Caller.SendAsync("MessagesMarkedAsRead", conversationId);
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to mark messages as read: {ex.Message}");
            }
        }

        public async Task GetOnlineUsers()
        {
            try
            {
                var onlineUsers = await _chatService.GetOnlineUsersAsync();
                var currentUser = await _chatService.GetUserByConnectionIdAsync(Context.ConnectionId);
                
                var availableUsers = onlineUsers
                    .Where(u => u.UserId != currentUser?.UserId)
                    .Select(u => new 
                    {
                        u.UserId,
                        u.Username,
                        u.IsOnline,
                        u.LastSeen
                    });

                await Clients.Caller.SendAsync("OnlineUsersList", availableUsers);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to get online users: {ex.Message}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var user = await _chatService.GetUserByConnectionIdAsync(Context.ConnectionId);
                if (user != null)
                {
                    await _chatService.RemoveUserAsync(Context.ConnectionId);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{user.UserId}");
                    
                    var onlineUsers = await _chatService.GetOnlineUsersAsync();
                    await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers.Select(u => new 
                    {
                        u.UserId,
                        u.Username,
                        u.IsOnline,
                        u.LastSeen
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disconnection: {ex.Message}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}