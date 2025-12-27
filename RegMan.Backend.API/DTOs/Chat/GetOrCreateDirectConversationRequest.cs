using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.API.DTOs.Chat
{
    public class GetOrCreateDirectConversationRequest
    {
        [Required]
        public string OtherUserId { get; set; } = string.Empty;

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
