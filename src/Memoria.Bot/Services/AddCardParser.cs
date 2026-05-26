using Memoria.Shared.Kernel.Results;

namespace Memoria.Bot.Services;

/// <summary>
/// Pure-функция парсинга свободного текста <c>/add</c> в трёхчастную
/// структуру (заголовок, теги, тело). Используется как для one-shot формы
/// (<c>/add &lt;title&gt;\n#tag\nbody</c>), так и для FSM-диалога, где tags
/// строятся отдельно.
/// </summary>
internal sealed record ParsedCard(string Title, IReadOnlyList<string> Tags, string Body);

internal sealed class AddCardParser
{
    public Result<ParsedCard> Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<ParsedCard>.Failure(Error.Validation(
                "bot.add_empty", "Card content is empty."));
        }

        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var titleLine = lines[0];
        var (titleText, titleTags) = ExtractTags(titleLine);
        var title = titleText.Trim();
        if (title.Length == 0)
        {
            return Result<ParsedCard>.Failure(Error.Validation(
                "bot.add_title_empty", "Title is required on the first line."));
        }

        var tags = new List<string>(titleTags);
        var bodyBuilder = new List<string>(capacity: lines.Length);
        for (var i = 1; i < lines.Length; i++)
        {
            var (text, lineTags) = ExtractTags(lines[i]);
            tags.AddRange(lineTags);
            bodyBuilder.Add(text);
        }

        var body = string.Join('\n', bodyBuilder).Trim('\n', ' ', '\t');
        if (body.Length == 0)
        {
            return Result<ParsedCard>.Failure(Error.Validation(
                "bot.add_body_empty", "Body is required."));
        }

        var distinctTags = tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Result<ParsedCard>.Success(new ParsedCard(title, distinctTags, body));
    }

    /// <summary>
    /// Достаёт <c>#tag</c>-токены из строки и возвращает (текст без тегов; теги).
    /// Токеном считается слово, начинающееся с <c>#</c>, любые соседние пробелы
    /// и табы выкидываются вместе с тегом, чтобы тело не превращалось в "  ".
    /// </summary>
    internal static (string Text, IReadOnlyList<string> Tags) ExtractTags(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var tags = new List<string>();
        var remaining = new List<string>();

        foreach (var token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith('#') && token.Length > 1)
            {
                tags.Add(token[1..]);
            }
            else
            {
                remaining.Add(token);
            }
        }

        return (string.Join(' ', remaining), tags);
    }
}
