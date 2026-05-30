using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Collapses <paramref name="SourceUserId"/> into <paramref name="TargetUserId"/>:
/// all of Source's cards / reminders / reviews get re-parented to Target,
/// Source's non-conflicting UserIdentity rows are repointed (conflicting ones
/// dropped), Source's refresh tokens and verification codes are purged, and
/// the Source User row is soft-deleted.
///
/// Idempotent — re-running after a partial failure completes any remaining
/// work without duplicating effort.
/// </summary>
public sealed record MergeAccountsCommand(
    Guid SourceUserId,
    Guid TargetUserId) : IRequest<Result<MergeAccountsResultDto>>;
