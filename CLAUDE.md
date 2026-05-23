# Memoria — Instructions for Claude Code

This file is read automatically at the start of every session. Follow these rules
for any changes in the codebase. If a rule conflicts with `PROJECT_BRIEF.md` or
`PROJECT_BRIEF_ADDENDUM.md` — ask, don't stay silent.

---

## File Organization: MANDATORY RULES

### Each type goes into a folder by its purpose

**DO NOT** put all files flat in the project root. Use folders that reflect the
purpose of files. This is a requirement, not a recommendation.

**Wrong:**

```
Memoria.Shared.Kernel/
├─ Result.cs
├─ ResultOfT.cs
├─ Error.cs
├─ ErrorType.cs
├─ Entity.cs
└─ ValueObject.cs
```

**Correct:**

```
Memoria.Shared.Kernel/
├─ Results/
│  ├─ Result.cs
│  ├─ ResultOfT.cs
│  ├─ Error.cs
│  └─ ErrorType.cs
├─ Domain/
│  ├─ Entity.cs
│  ├─ AggregateRoot.cs
│  └─ ValueObject.cs
├─ ValueObjects/
│  ├─ EmailAddress.cs
│  └─ TelegramId.cs
└─ Abstractions/
   └─ IDateTimeProvider.cs
```

### Vertical slice inside modules

For modules (`Memoria.Cards`, `Memoria.Users`, ...) — organize by features:

```
Memoria.Cards/
├─ Features/
│  ├─ AddCard/
│  │  ├─ AddCardCommand.cs
│  │  ├─ AddCardCommandHandler.cs
│  │  ├─ AddCardCommandValidator.cs
│  │  └─ AddCardEndpoint.cs           # if there's an API endpoint
│  ├─ DeleteCard/
│  │  ├─ SoftDeleteCardCommand.cs
│  │  └─ SoftDeleteCardCommandHandler.cs
│  └─ ListCards/
│     ├─ ListCardsQuery.cs
│     └─ ListCardsQueryHandler.cs
├─ Domain/
│  ├─ Card.cs
│  ├─ Tag.cs
│  └─ CardTag.cs
├─ Persistence/
│  ├─ CardsDbContext.cs
│  ├─ Configurations/
│  │  ├─ CardConfiguration.cs
│  │  └─ TagConfiguration.cs
│  └─ Migrations/
│     └─ ...
├─ Services/
│  └─ TagNormalizer.cs
└─ DependencyInjection.cs              # AddCardsModule extension
```

### Contracts projects

For `*.Contracts` — organize by contract type:

```
Memoria.Cards.Contracts/
├─ Commands/
│  ├─ AddCardCommand.cs
│  └─ SoftDeleteCardCommand.cs
├─ Queries/
│  └─ GetCardByIdQuery.cs
├─ Events/
│  ├─ CardCreatedEvent.cs
│  └─ CardSoftDeletedEvent.cs
└─ Dtos/
   ├─ CardDto.cs
   └─ TagDto.cs
```

### Test projects

```
Memoria.Cards.UnitTests/
├─ Features/
│  ├─ AddCard/
│  │  └─ AddCardCommandHandlerTests.cs
│  └─ DeleteCard/
│     └─ SoftDeleteCardCommandHandlerTests.cs
└─ Services/
   └─ TagNormalizerTests.cs
```

Test structure **mirrors** the structure of the project under test.

---

## One type per file

One public type = one file with a matching name.

**Exceptions** (allowed in a single file):

- Small related types: an enum and a record that uses it.
- Generic and non-generic versions of the same class (`Result` and `Result<T>`).
- Internal types used only inside one public class.

**When in doubt — split.**

---

## Namespace matches the folder

Namespace **must** reflect the physical folder structure:

```csharp
// File: Memoria.Cards/Features/AddCard/AddCardCommand.cs
namespace Memoria.Cards.Features.AddCard;
```

```csharp
// File: Memoria.Shared.Kernel/Results/Error.cs
namespace Memoria.Shared.Kernel.Results;
```

Use **file-scoped namespaces** (`namespace X;`), not block-scoped (`namespace X { ... }`).

---

## Naming conventions

- **Folders** — PascalCase. Singular for technical concepts ("Domain", "Persistence"),
  plural for collections of homogeneous types ("Commands", "Queries", "Events",
  "Configurations", "Features").
- **Files** — PascalCase, matching the primary public type name.
- **Test classes** — `<ClassUnderTest>Tests.cs`.
- **Test methods** — `Method_WhenCondition_ShouldExpectedBehavior` or with
  underscores like `Should_return_error_when_card_not_found`.

---

## Before creating a file: check if there's a matching folder

Before creating a new `.cs` file:

1. Determine the **category** of the file (Entity? Command? Validator? DbContext?).
2. Check if a folder for this category already exists in the current project.
3. If yes — put the file there.
4. If no, but conventions above say it should exist — create the folder and put
   the file inside.
5. **Never** put files in the project root, except for:
   - `DependencyInjection.cs` (extension methods for module registration)
   - `<ProjectName>AssemblyMarker.cs` (marker for architecture tests)
   - `GlobalUsings.cs` (if explicit global usings are needed)

---

## After changes

After each work stage (creating a module, adding a feature):

1. Run `dotnet build` — must be green.
2. Run `dotnet test` — must be green.
3. If architecture tests fail — **do not disable them**, fix the structure.
4. Summarize briefly: what was done, what files were created, what tests were added.

---

## What NOT to do

- Don't create files in the project root (except for the exceptions above).
- Don't put multiple unrelated types in one file.
- Don't use a namespace that doesn't match the folder.
- Don't create folders containing a single file "just in case" — a folder should
  contain **at least 2 files** or have an explicit purpose (e.g., `Migrations/`
  with a single file is fine).
- Don't stay silent if brief rules conflict with each other — ask.

---

## Quick-access commands

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/Memoria.Cards.UnitTests

# Apply migrations (example for Users)
dotnet ef database update \
    --project src/Modules/Users/Memoria.Users \
    --startup-project src/Memoria.Host \
    --context UsersDbContext

# Run the application
dotnet run --project src/Memoria.Host

# PostgreSQL locally
docker compose up -d postgres
```

---

## Document priority

If rules contradict each other:

1. `CLAUDE.md` (this file) — style and organization.
2. `PROJECT_BRIEF_ADDENDUM.md` — brief refinements.
3. `PROJECT_BRIEF.md` — main brief.
4. Explicit user request in chat — **wins everything** if it contradicts.

In case of an unresolvable conflict — ask the user.
