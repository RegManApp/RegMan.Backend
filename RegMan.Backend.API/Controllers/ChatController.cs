using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.API.Common;
using RegMan.Backend.API.DTOs.Chat;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all chat endpoints
    public class ChatController : ControllerBase
    {
        private readonly IChatService chatService;
        private readonly IHubContext<ChatHub> chatHub;

        public ChatController(IChatService chatService, IHubContext<ChatHub> chatHub)
        {
            this.chatService = chatService;
            this.chatHub = chatHub;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("id")
                ?? string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationsAsync()
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var conversations = await chatService.GetUserConversationsAsync(userId);
            return Ok(ApiResponse<ViewConversationsDTO>.SuccessResponse(conversations));
        }

        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsersAsync([FromQuery] SearchUsersRequest request)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var results = await chatService.SearchUsersAsync(userId, request.Query, request.Limit);
            return Ok(ApiResponse<List<ChatUserSearchResultDTO>>.SuccessResponse(results));
        }

        [HttpPost("conversations/direct")]
        public async Task<IActionResult> GetOrCreateDirectConversationAsync([FromBody] GetOrCreateDirectConversationRequest request)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var conversation = await chatService.GetOrCreateDirectConversationAsync(
                userId,
                request.OtherUserId,
                request.Page,
                request.PageSize
            );

            // Notify the other participant (if online) to join the new/existing conversation group.
            // This avoids requiring a refresh when a new chat is started.
            await chatHub.Clients.User(request.OtherUserId)
                .SendAsync("ConversationCreated", conversation.ConversationId);

            return Ok(ApiResponse<ViewConversationDTO>.SuccessResponse(conversation));
        }

        [HttpGet("{conversationId:int}")]
        public async Task<IActionResult> GetConversationByIdAsync(
            int conversationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var conversation = await chatService.ViewConversationAsync(userId, conversationId, page, pageSize);
            return Ok(ApiResponse<ViewConversationDTO>.SuccessResponse(conversation));
        }

        [HttpPost("conversations/{conversationId:int}/read")]
        public async Task<IActionResult> MarkConversationReadAsync(int conversationId)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var receipts = await chatService.MarkConversationMessagesReadAsync(userId, conversationId);

            // Notify each sender of their messages being read.
            foreach (var receipt in receipts)
            {
                if (!string.IsNullOrWhiteSpace(receipt.SenderId))
                {
                    await chatHub.Clients.User(receipt.SenderId).SendAsync("MessageRead", receipt);
                }
            }

            return Ok(ApiResponse<object>.SuccessResponse(new { count = receipts.Sum(r => r.MessageIds.Count) }));
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageAsync(
            [FromQuery] string? receiverId,
            [FromQuery] int? conversationId,
            [FromQuery] string textMessage)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            if (string.IsNullOrWhiteSpace(textMessage))
                return BadRequest(ApiResponse<string>.FailureResponse("Message content is required", StatusCodes.Status400BadRequest));

            // Either receiverId (new conversation) OR conversationId (existing conversation) must be provided.
            if (string.IsNullOrWhiteSpace(receiverId) && conversationId is null)
                return BadRequest(ApiResponse<string>.FailureResponse("receiverId or conversationId is required", StatusCodes.Status400BadRequest));

            var conversation = await chatService.SendMessageAsync(userId, receiverId, conversationId, textMessage);
            return Ok(ApiResponse<ViewConversationDTO>.SuccessResponse(conversation, "Message sent successfully"));
        }
    }
}
