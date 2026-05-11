namespace SwtorDailyTool;

/// <summary>
/// Curated list of verified SWTOR / Star Wars lore quotes and gameplay tips.
/// Each entry cites its actual canonical source — film, in-game text, or game mechanic.
/// Avoid adding entries you cannot directly source; misattributed quotes spread.
/// </summary>
public static class SwtorLoreQuotes
{
    public static readonly (string Quote, string Source)[] Quotes =
    [
        // ── The Sith Code (verbatim from SWTOR / Tales of the Jedi)
        ("Peace is a lie, there is only passion.", "The Sith Code"),
        ("Through passion, I gain strength.", "The Sith Code"),
        ("Through strength, I gain power.", "The Sith Code"),
        ("Through power, I gain victory.", "The Sith Code"),
        ("Through victory, my chains are broken. The Force shall free me.", "The Sith Code"),

        // ── The Jedi Code (verbatim from Tales of the Jedi / KOTOR / SWTOR)
        ("There is no emotion, there is peace.", "The Jedi Code"),
        ("There is no ignorance, there is knowledge.", "The Jedi Code"),
        ("There is no passion, there is serenity.", "The Jedi Code"),
        ("There is no chaos, there is harmony.", "The Jedi Code"),
        ("There is no death, there is the Force.", "The Jedi Code"),

        // ── Verified Star Wars film quotes (well-established canon)
        ("Do or do not. There is no try.", "Yoda — The Empire Strikes Back"),
        ("Fear leads to anger. Anger leads to hate. Hate leads to suffering.", "Yoda — The Phantom Menace"),
        ("The Force will be with you. Always.", "Obi-Wan Kenobi — A New Hope"),
        ("In my experience, there is no such thing as luck.", "Obi-Wan Kenobi — A New Hope"),
        ("These aren't the droids you're looking for.", "Obi-Wan Kenobi — A New Hope"),
        ("I find your lack of faith disturbing.", "Darth Vader — A New Hope"),
        ("The ability to destroy a planet is insignificant next to the power of the Force.", "Darth Vader — A New Hope"),
        ("Once you start down the dark path, forever will it dominate your destiny.", "Yoda — The Empire Strikes Back"),

        // ── Verified KOTOR / SWTOR character lines (well-known canon)
        ("Statement: I am ready to serve, master. I have many useful skills.", "HK-47 — KOTOR"),
        ("HK-51 model assassin droids: built by Czerka Corporation, restored by independent operatives.", "SWTOR lore — HK-51 chain"),

        // ── Verified SWTOR lore (in-game codex / class storylines)
        ("Tython is the legendary world where the Jedi Order was founded — abandoned for millennia, rediscovered by Master Satele Shan.", "SWTOR codex — Tython"),
        ("Korriban's Valley of the Dark Lords holds the tombs of fallen Sith Lords — the Sith Academy stands at its mouth.", "SWTOR codex — Korriban"),
        ("Vitiate, the Sith Emperor, performed a ritual on Nathema that drained all life from the world and granted him near-immortality.", "SWTOR lore — Sith Emperor"),
        ("The Eternal Empire of Zakuul rose from the unknown regions and toppled both the Republic and the Sith Empire.", "SWTOR — Knights of the Fallen Empire"),
        ("Revan founded the Order of Revan and walked the line between Light and Dark side for centuries.", "SWTOR — Foundry / Maelstrom Prison / Shadow of Revan"),
        ("The Great Hunt is a galaxy-wide bounty hunter contest sanctioned by the Mandalorian clans.", "SWTOR — Bounty Hunter class story"),
        ("The Voss Mystics are seers whose visions can shape the fate of their people. Their will is considered absolute.", "SWTOR codex — Voss"),
        ("Nar Shaddaa, moon of Hutta, is known as the Smuggler's Moon — under control of the Hutt Cartel.", "SWTOR codex — Nar Shaddaa"),
        ("The Esh-kha were imprisoned on Belsavis in cryogenic stasis for thousands of years before the Republic prison breach freed them.", "SWTOR codex — Belsavis"),
        ("Master Satele Shan led the Republic forces during the Battle of Alderaan and later became Grand Master of the Jedi Order.", "SWTOR lore — Satele Shan"),
        ("The Sacking of Coruscant occurred when the resurgent Sith Empire devastated the Republic capital, ending the Great Galactic War.", "SWTOR lore — Treaty of Coruscant"),
        ("The Hutt Cartel is a neutral third faction in SWTOR — they sided with neither Republic nor Empire.", "SWTOR — Rise of the Hutt Cartel"),

        // ── Verified Holotracker / SWTOR gameplay tips
        ("Companion Influence ranks 1–50 reduce crew skill mission time and increase critical-success rate.", "SWTOR mechanic"),
        ("Crew skill missions come in Standard, Bountiful, Rich, and Abundant grades — higher grades give more or rarer materials.", "SWTOR mechanic"),
        ("A critical success on a crew skill mission can yield bonus materials, schematics, or augments.", "SWTOR mechanic"),
        ("Datacrons grant a permanent stat increase or Matrix Shard to the character who collects them. Legacy datacron achievements unlock perks for the whole account.", "SWTOR mechanic"),
        ("Conquest objectives reset every Tuesday at 5 AM Pacific. Plan your weekly target before the cutoff.", "SWTOR mechanic"),
        ("The combat log file is opt-in: enable it in Preferences → User Interface → Enable Combat Logging.", "SWTOR setting"),
        ("Each class has its own personal storyline — eight in total — and they can all be played by both Republic and Empire factions through their advanced classes.", "SWTOR mechanic"),
        ("Strongholds give per-character Conquest bonuses based on prestige (decoration count) — fully decorating a stronghold can multiply your Conquest gains substantially.", "SWTOR mechanic"),
    ];
}
