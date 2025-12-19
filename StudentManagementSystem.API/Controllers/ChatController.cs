using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService chatService;
        public ChatController(IChatService chatService)
        {
            this.chatService = chatService;
        }
        private string GetStudentID()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(studentId))
                throw new InvalidOperationException("User ID claim (NameIdentifier) is missing from the authorized token.");
            return studentId;
        }
        [HttpPost]
        public async Task<IActionResult> SendMessageAsync([FromQuery] string receiverId, [FromQuery] string textMessage)
        {
            var senderId = GetStudentID();
            // In a real application, you would get the senderId from the authenticated user context
           // string senderId = "49deef11-c383-4758-8509-f6006d1281da"; // Placeholder for demonstration
            var conversation = await chatService.SendMessageAsync(senderId, receiverId, textMessage);
            return Ok(conversation);
        }
        [HttpGet]
        public async Task<IActionResult> GetConversationsAsync()
        {
            var senderId = GetStudentID();
            // In a real application, you would get the senderId from the authenticated user context
           // string senderId = "49deef11-c383-4758-8509-f6006d1281da"; // Placeholder for demonstration
            var conversation = await chatService.GetUserConversationsAsync(senderId);
            return Ok(conversation);
        }
        [HttpGet("convoId")]
        public async Task<IActionResult> GetConversationsAsync(int convoId)
        {
            var senderId = GetStudentID();
            // In a real application, you would get the senderId from the authenticated user context
           // string senderId = "49deef11-c383-4758-8509-f6006d1281da"; // Placeholder for demonstration
            var conversation = await chatService.ViewConversationAsync(senderId, convoId,1,10);
            return Ok(conversation);
        }
    }
}
