using Memoria.AI.Contracts.Dtos;
using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Api.Endpoints.Admin;

/// <summary>
/// One-shot payload for the admin overview page. Composed at the API edge
/// from MediatR sends across Users / Cards / Reviews / Reminders / AI so
/// the SPA only makes a single HTTP request.
/// </summary>
public sealed record AdminOverviewPayloadDto(
    ActivationFunnelDto ActivationFunnel,
    ActiveUserCountsDto ActiveUsers,
    RetentionCohortsDto Retention,
    RatingDistributionDto GlobalRatings,
    IReadOnlyList<AiCalibrationBucketDto> AiCalibration,
    ReminderSkipRateDto ReminderSkipRate,
    AiSpendTotalsDto AiSpend,
    IReadOnlyList<AiSpendTrendPointDto> AiSpendTrend,
    IReadOnlyList<TopSpenderDto> TopSpenders,
    AiFailureRateDto AiFailureRate,
    decimal CostPerActiveUserUsd);

/// <summary>
/// Activation funnel: signup → Telegram linked → has card → has review.
/// Composed at the API edge from three module-local handlers.
/// </summary>
public sealed record ActivationFunnelDto(
    int Signups,
    int TelegramLinked,
    int HasCard,
    int HasReview);
