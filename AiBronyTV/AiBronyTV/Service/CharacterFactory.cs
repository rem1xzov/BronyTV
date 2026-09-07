using RainbowDashAI.Core;

namespace AiBronyTV.Service;

public class CharacterFactory
{
    public static string GetSystemPrompt(string characterId)
    {
        var persona = characterId.ToLower() switch
        {
            "rainbow" => SystemPrompts.RainbowDashPersona, 
            "twilight" => SystemPrompts.TwilightPersona,
            "trixie" => SystemPrompts.TrixiePersona,
            "pinki" => SystemPrompts.PinkiePiePersona,
            "fluttershy" => SystemPrompts.FluttershyPersona,
            "rarity" => SystemPrompts.RarityPersona,
            "applejack" => SystemPrompts.ApplejackPersona,
            "starlight" => SystemPrompts.StarlightPersona,
            "sunset" => SystemPrompts.SunsetPersona,
            "celestia" => SystemPrompts.CelestiaPersona,
            "luna" => SystemPrompts.LunaPersona,
            "cadance" => SystemPrompts.CadancePersona,
            "applebloom" => SystemPrompts.AppleBloomPersona,
            "sweetiebelle" => SystemPrompts.SweetieBellePersona,
            "scootaloo" => SystemPrompts.ScootalooPersona,
            "derpy" => SystemPrompts.DerpyPersona,
            "discord" => SystemPrompts.DiscordPersona,
            "cozyglow" => SystemPrompts.CozyGlowPersona,
            "octavia" => SystemPrompts.OctaviaPersona,
            "djpon3" => SystemPrompts.DjPon3Persona,
            "shiningarmor" => SystemPrompts.ShiningArmorPersona,
            "narrator" => SystemPrompts.NarratorPersona,
            "adagio" => SystemPrompts.AdagioDazzlePersona,
            "aria" => SystemPrompts.AriaBlazePersona,
            "sonata" => SystemPrompts.SonataDuskPersona,
            "applejackeg" => SystemPrompts.ApplejackEGPersona,
            "fluttershyeg" => SystemPrompts.FluttershyEGPersona,
            "pinkieeg" => SystemPrompts.PinkiePieEGPersona,
            "rainboweg" => SystemPrompts.RainbowDashEGPersona,
            "rarityeg" => SystemPrompts.RarityEGPersona,
            "twilighteg" => SystemPrompts.TwilightSparkleEGPersona,
            _ => throw new ArgumentException($"Персонаж с ID '{characterId}' не найден!")
        };
        return persona + "\n\n" + SystemPrompts.UniversalRpRules;
    }
}