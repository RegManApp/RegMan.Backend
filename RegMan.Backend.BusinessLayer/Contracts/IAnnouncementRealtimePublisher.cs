namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IAnnouncementRealtimePublisher
    {
        Task PublishAnnouncementSentAsync(string userId, object payload);
        Task PublishAnnouncementReadAsync(string userId, object payload);
    }
}
