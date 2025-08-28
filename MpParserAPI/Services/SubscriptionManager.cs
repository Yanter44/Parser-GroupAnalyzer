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

            if (!parser.SubscriptionEndDate.HasValue || now >= parser.SubscriptionEndDate.Value)
                return false;

            if (parser.SubscriptionType == SubscriptionType.Premium)
            {
                allowedDuration = parser.SubscriptionEndDate.Value - now;
            }
            else
            {
                var remainingSubscription = parser.SubscriptionEndDate.Value - now;
                allowedDuration = remainingSubscription.TotalHours > 24
                    ? TimeSpan.FromHours(24)
                    : remainingSubscription;
            }

            return allowedDuration > TimeSpan.Zero;
        }

        public TimeSpan GetTotalParsingTime(ParserData parser)
        {
            var now = DateTime.UtcNow;

            if (parser.SubscriptionType == SubscriptionType.Test)
            {
                return parser.TotalParsingTime;
            }
            if (!parser.SubscriptionEndDate.HasValue)
                return TimeSpan.Zero;

            var remainingTime = parser.SubscriptionEndDate.Value - now;
            return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        }

        public TimeSpan GetTotalParsingTime(Guid parserId)
        {
            var now = DateTime.UtcNow;
            var existParser = _parserDataStorage.GetParser(parserId);

            if (existParser.SubscriptionType == SubscriptionType.Test)
            {
                return existParser.TotalParsingTime;
            }

            if (!existParser.SubscriptionEndDate.HasValue)
                return TimeSpan.Zero;

            var remainingTime = existParser.SubscriptionEndDate.Value - now;
            return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
        }
        public TimeSpan GetRemainingParsingTime(Guid parserId)
        {
            var existParser = _parserDataStorage.GetParser(parserId);
            var now = DateTime.UtcNow;

            if (!existParser.SubscriptionEndDate.HasValue || now >= existParser.SubscriptionEndDate.Value)
                return TimeSpan.Zero;

            if (!existParser.IsParsingStarted || !existParser.ParsingStartedAt.HasValue)
            {
                return GetAvailableParsingTime(existParser);
            }

            var elapsedTime = now - existParser.ParsingStartedAt.Value;

            if (existParser.SubscriptionType == SubscriptionType.Premium)
            {
                var totalRemaining = existParser.SubscriptionEndDate.Value - now;
                return totalRemaining > TimeSpan.Zero ? totalRemaining : TimeSpan.Zero;
            }
            else
            {
                var maxSessionTime = TimeSpan.FromHours(24);
                var sessionRemaining = maxSessionTime - elapsedTime;

                var subscriptionRemaining = existParser.SubscriptionEndDate.Value - now;

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

            if (!parser.SubscriptionEndDate.HasValue || now >= parser.SubscriptionEndDate.Value)
                return TimeSpan.Zero;

            if (parser.SubscriptionType == SubscriptionType.Premium)
            {
                return parser.SubscriptionEndDate.Value - now;
            }
            else
            {
                var remainingSubscription = parser.SubscriptionEndDate.Value - now;
                return remainingSubscription.TotalHours > 24
                    ? TimeSpan.FromHours(24)
                    : remainingSubscription;
            }
        }
    }

}
