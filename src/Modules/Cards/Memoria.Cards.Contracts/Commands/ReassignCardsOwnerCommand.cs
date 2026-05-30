using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Re-parents every card owned by <paramref name="SourceUserId"/> to
/// <paramref name="TargetUserId"/>. Also dedupes tags by
/// <c>NormalizedName</c>: a Source tag whose name already exists on Target
/// is collapsed onto the Target tag (its <c>CardTag</c> rows repointed),
/// otherwise the Source tag is handed over wholesale.
///
/// Returns the number of cards moved. Used by the account-merge flow.
/// </summary>
public sealed record ReassignCardsOwnerCommand(
    Guid SourceUserId,
    Guid TargetUserId) : IRequest<Result<int>>;
