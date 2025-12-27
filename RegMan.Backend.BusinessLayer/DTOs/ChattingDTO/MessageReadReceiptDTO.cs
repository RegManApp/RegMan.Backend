namespace RegMan.Backend.BusinessLayer.DTOs.ChattingDTO
{
    public class MessageReadReceiptDTO
    {
        public int ConversationId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string ReaderId { get; set; } = string.Empty;
        public List<int> MessageIds { get; set; } = new();
        public DateTime ReadAt { get; set; }
    }
}
