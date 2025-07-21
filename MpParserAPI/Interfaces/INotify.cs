namespace MpParserAPI.Interfaces
{
    public interface INotify
    {
        Task SendNotifyToBotAboutReceivedMessageAsync(Guid parserId, string message, string LinkToMessageInTg);
        Task SendSimpleNotify(Guid parserId, string message);
    }
}
