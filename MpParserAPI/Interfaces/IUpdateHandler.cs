namespace MpParserAPI.Interfaces
{
    public interface IUpdateHandler
    {
        Task HandleAsync(Guid parserId,UpdateData update);
    }
}
