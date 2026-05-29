namespace Memoria.Bot.Localization;

/// <summary>
/// Central place for the bot's user-facing copy. Today these are plain English
/// constants; this is the single seam through which localization will be added
/// later (keyed lookup / IStringLocalizer) without touching call sites. New
/// bot UI text should be added here rather than inlined.
/// </summary>
internal static class BotText
{
    // ── Menu (home) ──────────────────────────────────────────────────────────
    public const string MenuTitle = "🏠 *Memoria*\nWhat would you like to do?";
    public const string BtnAdd = "➕ Add card";
    public const string BtnList = "📋 My cards";
    public const string BtnTags = "🏷 Tags";
    public const string BtnDue = "⏰ Due now";
    public const string BtnSettings = "⚙️ Settings";
    public const string BtnHelp = "❓ Help";
    public const string BtnBack = "‹ Back";

    // ── Due ──────────────────────────────────────────────────────────────────
    public const string DueNothing = "🎉 Nothing to review right now. Reminders arrive on schedule.";
    public const string DueSending = "📨 Sending…";

    // ── Settings ─────────────────────────────────────────────────────────────
    public const string SettingsTitle = "⚙️ *Settings*";
    public const string BtnChangeTimezone = "🕘 Change timezone";
    public const string BtnQuietHours = "🌙 Quiet hours";
    public const string BtnLogin = "🔑 Sign in to web";
    public const string TimezoneLabel = "Timezone";
    public const string QuietHoursLabel = "Quiet hours";
    public const string QuietHoursOff = "off";

    // ── Quiet hours ──────────────────────────────────────────────────────────
    public const string QuietTitle = "🌙 *Quiet hours*\nNo reminders are delivered during this window.";
    public const string BtnQuietOff = "Turn off";
    public const string Saved = "Saved ✓";

    // ── Common ───────────────────────────────────────────────────────────────
    public const string NotLinked = "❌ Not linked. Use /start first.";
    public const string SomethingWrong = "❌ Something went wrong, try again.";
}
