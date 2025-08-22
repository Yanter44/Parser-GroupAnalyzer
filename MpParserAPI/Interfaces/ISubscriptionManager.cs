using MpParserAPI.Models;

namespace MpParserAPI.Interfaces
{
    public interface ISubscriptionManager
    {
        bool CanStartParsing(ParserData parser, out TimeSpan allowedDuration);
        TimeSpan GetTotalParsingTime(ParserData parser);
        TimeSpan GetTotalParsingTime(Guid parserId);
        TimeSpan GetRemainingParsingTime(Guid parserId);
    }
}
