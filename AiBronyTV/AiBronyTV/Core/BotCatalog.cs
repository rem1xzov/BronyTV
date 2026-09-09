namespace AiBronyTV.Core;

/// <summary>
/// Метаданные персонажей-ботов (для UI). Используются и публичным эндпоинтом /api/bots,
/// и админским /api/admin/bots. Свойства названы в нижнем регистре намеренно — так
/// сохраняется прежняя форма JSON-ответа ({ id, name, description }), на которую уже
/// могут опираться клиенты.
/// </summary>
public sealed record BotInfo(string id, string name, string description);

public static class BotCatalog
{
    public static readonly IReadOnlyList<BotInfo> Bots = new[]
    {
        new BotInfo("rainbow", "Рэйнбоу Дэш", "Самая быстрая и дерзкая пегаска Понивилля."),
        new BotInfo("twilight", "Твайлайт Спаркл", "Принцесса дружбы и учёный-книжный червь."),
        new BotInfo("trixie", "Трикси", "Великая и Могущественная иллюзионистка."),
        new BotInfo("pinki", "Пинки Пай", "Неутомимая королева вечеринок и кексов."),
        new BotInfo("fluttershy", "Флаттершай", "Добрая и робкая ценительница животных."),
        new BotInfo("rarity", "Рарити", "Изысканный единорог-модельер из бутика 'Карусель'."),
        new BotInfo("applejack", "Эпплджек", "Надёжная и честная земная пони с фермы."),
        new BotInfo("starlight", "Старлайт Глиммер", "Бывшая злодейка, а теперь ученица Искорки."),
        new BotInfo("sunset", "Сансет Шиммер", "Крутая рок-звезда из мира людей."),
        new BotInfo("celestia", "Принцесса Селестия", "Мудрая правительница Эквестрии, поднимающая солнце."),
        new BotInfo("luna", "Принцесса Луна", "Повелительница снов и ночи, хранительница сновидений."),
        new BotInfo("cadance", "Принцесса Каденс", "Аликорн любви, правительница Кристальной Империи."),
        new BotInfo("applebloom", "Эппл Блум", "Младшая сестра Эпплджек, ищущая свой талант."),
        new BotInfo("sweetiebelle", "Свити Бель", "Сестренка Рарити. Хорошо поет, но часто косячит."),
        new BotInfo("scootaloo", "Скуталу", "Сорвиголова на скутере и фанатка Радуги Дэш."),
        new BotInfo("derpy", "Дерпи", "Добрая почтальонша, которая очень любит маффины."),
        new BotInfo("discord", "Дискорд", "Бывший дух хаоса. Обожает абсурд и розыгрыши."),
        new BotInfo("cozyglow", "Коузи Глоу", "Самая милая пони... с манией величия."),
        new BotInfo("octavia", "Октавия", "Изысканная виолончелистка из Кантерлота."),
        new BotInfo("djpon3", "DJ Pon-3", "Крутая тусовщица, общающаяся на сленге."),
        new BotInfo("shiningarmor", "Шайнинг Армор", "Капитан Королевской Стражи и гик."),
        new BotInfo("narrator", "Рассказчик (RPG)", "Опиши своего персонажа, и Рассказчик создаст для тебя сюжет в Эквестрии!"),
        new BotInfo("adagio", "Адажио Даззл", "Харизматичная сирена и лидер группы Dazzlings."),
        new BotInfo("aria", "Ария Блэйз", "Дерзкая и саркастичная сирена из Dazzlings."),
        new BotInfo("sonata", "Соната Даск", "Наивная и весёлая сирена, обожающая тако."),
        new BotInfo("applejackeg", "Эпплджек (EG)", "Честная и трудолюбивая ученица Canterlot High."),
        new BotInfo("fluttershyeg", "Флаттершай (EG)", "Добрая и застенчивая ученица Canterlot High."),
        new BotInfo("pinkieeg", "Пинки Пай (EG)", "Неутомимая королева вечеринок Canterlot High."),
        new BotInfo("rainboweg", "Рэйнбоу Дэш (EG)", "Спортивная и дерзкая капитан команды Canterlot High."),
        new BotInfo("rarityeg", "Рарити (EG)", "Стильная модница и дизайнер из Canterlot High."),
        new BotInfo("twilighteg", "Твайлайт Спаркл (EG)", "Умная и застенчивая отличница из Canterlot High.")
    };

    /// <summary>Имя персонажа по его id (для подписей реплик в групповом чате).</summary>
    public static string GetName(string characterId)
    {
        var key = characterId?.ToLowerInvariant() ?? string.Empty;
        foreach (var bot in Bots)
        {
            if (string.Equals(bot.id, key, StringComparison.OrdinalIgnoreCase))
            {
                return bot.name;
            }
        }
        return key;
    }
}
