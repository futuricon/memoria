using System.Globalization;
using System.Text.RegularExpressions;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Services;

/// <summary>
/// Чистая функциональная нормализация тегов: trim → lowercase →
/// пробелы в дефисы. Валидация символов и длины.
/// </summary>
internal sealed class TagNormalizer
{
    private const int MinLength = 2;
    private const int MaxLength = 30;

    private static readonly Regex AllowedCharsRegex = new(
        @"^[a-zA-Z0-9\s\-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Нормализует и валидирует один тег. Возвращает либо нормализованную строку,
    /// либо <see cref="Error.Validation"/> с описанием.
    /// </summary>
    public Result<string> Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<string>.Failure(Error.Validation(
                "cards.tag_empty", "Tag must not be empty."));
        }

        var trimmed = raw.Trim();
        if (!AllowedCharsRegex.IsMatch(trimmed))
        {
            return Result<string>.Failure(Error.Validation(
                "cards.tag_invalid_chars",
                $"Tag '{raw}' contains invalid characters. Allowed: letters, digits, dash, space."));
        }

        var lowered = trimmed.ToLower(CultureInfo.InvariantCulture);
        var normalized = CollapseWhitespaceToDashes(lowered);

        if (normalized.Length < MinLength)
        {
            return Result<string>.Failure(Error.Validation(
                "cards.tag_too_short",
                $"Tag '{raw}' is too short after normalization (min {MinLength} chars)."));
        }
        if (normalized.Length > MaxLength)
        {
            return Result<string>.Failure(Error.Validation(
                "cards.tag_too_long",
                $"Tag '{raw}' is too long after normalization (max {MaxLength} chars)."));
        }

        return Result<string>.Success(normalized);
    }

    /// <summary>
    /// Нормализует список тегов с дедупликацией. При любой ошибке нормализации
    /// одного тега возвращает Failure для всей пачки.
    /// </summary>
    public Result<IReadOnlyList<string>> NormalizeMany(IEnumerable<string> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var tag in raw)
        {
            var normalized = Normalize(tag);
            if (normalized.IsFailure)
            {
                return Result<IReadOnlyList<string>>.Failure(normalized.Error!);
            }
            if (seen.Add(normalized.Value!))
            {
                result.Add(normalized.Value!);
            }
        }

        return Result<IReadOnlyList<string>>.Success(result);
    }

    private static string CollapseWhitespaceToDashes(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        var prevWasDash = false;
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch) || ch == '-')
            {
                if (!prevWasDash)
                {
                    sb.Append('-');
                    prevWasDash = true;
                }
            }
            else
            {
                sb.Append(ch);
                prevWasDash = false;
            }
        }
        return sb.ToString().Trim('-');
    }
}
