using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Commands;

/// <summary>
/// Bulk re-parents every review owned by <paramref name="SourceUserId"/> to
/// <paramref name="TargetUserId"/>. Review rows are append-only with no
/// UNIQUE involving UserId, so this is a straight update. Used by the
/// account-merge flow; idempotent on re-run.
/// </summary>
public sealed record ReassignReviewsOwnerCommand(
    Guid SourceUserId,
    Guid TargetUserId) : IRequest<Result<int>>;
