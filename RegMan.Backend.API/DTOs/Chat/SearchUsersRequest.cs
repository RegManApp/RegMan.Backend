using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.API.DTOs.Chat
{
    public class SearchUsersRequest
    {
        [Required]
        public string Query { get; set; } = string.Empty;

        public int Limit { get; set; } = 20;
    }
}
