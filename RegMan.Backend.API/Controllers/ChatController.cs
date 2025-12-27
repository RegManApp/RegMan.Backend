using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
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

        public ChatController(IChatService chatService)
        {
            this.chatService = chatService;
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
