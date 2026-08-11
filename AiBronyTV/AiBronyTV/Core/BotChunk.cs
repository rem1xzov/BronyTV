namespace AiBronyTV.Core;

/// <summary>
/// A single streamed chunk from the bot. When <see cref="IsLimit"/> is true the message
/// is an in-character "daily limit reached" reply which the frontend renders more prominently.
/// </summary>
public record BotChunk(string Text, bool IsLimit);
