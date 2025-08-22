using MpParserAPI.Enums;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;

namespace MpParserAPI.Services
{
    public class SubscriptionManager : ISubscriptionManager
    {
        private readonly IParserDataStorage _parserDataStorage;
        public SubscriptionManager(IParserDataStorage parserDataStorage)
        {
            _parserDataStorage = parserDataStorage;
        }
        public bool CanStartParsing(ParserData parser, out TimeSpan allowedDuration)
        {
            allowedDuration = TimeSpan.Zero;
            var now = DateTime.UtcNow;

            if (now >= parser.SubscriptionEndDate)
                return false;

            if (parser.SubscriptionType == SubscriptionType.Premium)
            {
                allowedDuration = parser.SubscriptionEndDate - now;
            }
            else
            {
                var remainingSubscription = parser.SubscriptionEndDate - now;
                allowedDuration = remainingSubscription.TotalHours > 24
                    ? TimeSpan.FromHours(24)
                    : remainingSubscription;
            }

            return allowedDuration > TimeSpan.Zero;
        }
        public TimeSpan GetTotalParsingTime(ParserData parser)
        {
            var now = DateTime.UtcNow;         
            var remainingTime = parser.SubscriptionEndDate - now;
            return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        }
        public TimeSpan GetTotalParsingTime(Guid parserId)
        {
            var now = DateTime.UtcNow;
            var existParser = _parserDataStorage.GetParser(parserId);
            var remainingTime = existParser.SubscriptionEndDate - now;
            return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        }
        public TimeSpan GetRemainingParsingTime(Guid parserId)
        {
            var existParser = _parserDataStorage.GetParser(parserId);
            var now = DateTime.UtcNow;

            if (now >= existParser.SubscriptionEndDate)
                return TimeSpan.Zero;

            if (!existParser.IsParsingStarted || !existParser.ParsingStartedAt.HasValue)
            {
                return GetAvailableParsingTime(existParser);
            }
            var elapsedTime = now - existParser.ParsingStartedAt.Value;

            if (existParser.SubscriptionType == SubscriptionType.Premium)
            {
                var totalRemaining = existParser.SubscriptionEndDate - now;
                return totalRemaining > TimeSpan.Zero ? totalRemaining : TimeSpan.Zero;
            }
            else
            {
                var maxSessionTime = TimeSpan.FromHours(24);
                var sessionRemaining = maxSessionTime - elapsedTime;

                var subscriptionRemaining = existParser.SubscriptionEndDate - now;

                return TimeSpan.FromMinutes(
                    Math.Min(
                        sessionRemaining.TotalMinutes,
                        subscriptionRemaining.TotalMinutes
                    )
                );
            }
        }
        private TimeSpan GetAvailableParsingTime(ParserData parser)
        {
            var now = DateTime.UtcNow;

            if (now >= parser.SubscriptionEndDate)
                return TimeSpan.Zero;

            if (parser.SubscriptionType == SubscriptionType.Premium)
            {
                return parser.SubscriptionEndDate - now;
            }
            else
            {
                var remainingSubscription = parser.SubscriptionEndDate - now;
                return remainingSubscription.TotalHours > 24
                    ? TimeSpan.FromHours(24)
                    : remainingSubscription;
            }
        }

    }
}
