using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("User ID claim (NameIdentifier) is missing from the authorized token.");
            return userId;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageAsync([FromQuery] string receiverId, [FromQuery] string textMessage)
        {
            var senderId = GetUserId();
            var conversation = await chatService.SendMessageAsync(senderId, receiverId, textMessage);
            return Ok(ApiResponse<object>.SuccessResponse(conversation, "Message sent successfully"));
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationsAsync()
        {
            var senderId = GetUserId();
            var conversations = await chatService.GetUserConversationsAsync(senderId);
            return Ok(ApiResponse<object>.SuccessResponse(conversations));
        }

        [HttpGet("{convoId}")]
        public async Task<IActionResult> GetConversationByIdAsync(int convoId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var senderId = GetUserId();
            var conversation = await chatService.ViewConversationAsync(senderId, convoId, page, pageSize);
            return Ok(ApiResponse<object>.SuccessResponse(conversation));
        }
    }
}
