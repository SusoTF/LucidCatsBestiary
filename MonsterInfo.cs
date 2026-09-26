namespace LucidCatsBestiary
{
    /// <summary>
    /// Everything the bestiary shows about each monster, in bestiary order.
    /// </summary>
    internal sealed class MonsterInfo
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int Tier;
        public readonly string Description;

        private MonsterInfo(string id, int tier, string description)
        {
            Id = id;
            DisplayName = id;
            Tier = tier;
            Description = description;
        }

        /// <summary>Name of the monster's prefab inside the game.</summary>
        public string PrefabName => "DreamEnemy_" + Id;

        public static readonly MonsterInfo[] All =
        {
            new MonsterInfo("Eyes", 1,
                "Frozen while anyone is watching it. The moment everyone looks away, it rushes the closest " +
                "player at incredible speed. Keep your eyes on it."),

            new MonsterInfo("Flower Eye", 1,
                "Like Eyes, it only moves when nobody is looking. Slower, but its rapid strikes can drain " +
                "you in seconds. Never let it get close."),

            new MonsterInfo("Smiley", 1,
                "Look at it and it locks you into a staredown. Hold its gaze and don't move for 5 seconds, " +
                "or it will hunt you down."),

            new MonsterInfo("Mask", 1,
                "Harmless while you mind your own business. Look at it up close and it will chase you, " +
                "but it's slower than your sprint."),

            new MonsterInfo("Jaw", 2,
                "Hides beside doorways, waiting for someone to walk through. One vicious bite, then it " +
                "slips away to find another door."),

            new MonsterInfo("Dance Cat", 2,
                "Meet its gaze and you're in a dance-off. Dance in place and keep looking at it for " +
                "5 seconds, or face the consequences."),

            new MonsterInfo("Ghost Cat", 3,
                "Silently follows you, a few steps behind. It won't attack unless you turn around and " +
                "look at it."),

            new MonsterInfo("Listener", 3,
                "Blind, but it hears everything. When it stops to listen, freeze. One sound too close " +
                "and it strikes with deadly force."),
        };
    }
}
