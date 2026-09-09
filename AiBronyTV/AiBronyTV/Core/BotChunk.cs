namespace AiBronyTV.Core;

/// <summary>
/// A single streamed chunk from the bot. When <see cref="IsLimit"/> is true the message
/// is an in-character "daily limit reached" reply which the frontend renders more prominently.
///
/// <see cref="UserMessageId"/> — реальный Id сохранённого сообщения пользователя (нужен для
/// редактирования в одиночном чате). <see cref="CharacterId"/> — какой бот говорит (для
/// группового чата). <see cref="Event"/> — служебные события группового чата
/// ("bot_start" / "bot_end").
/// </summary>
public record BotChunk(
    string Text,
    bool IsLimit,
    int? UserMessageId = null,
    string? CharacterId = null,
    string? Event = null);
