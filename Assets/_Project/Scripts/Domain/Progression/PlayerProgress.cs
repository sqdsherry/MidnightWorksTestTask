using System;

namespace AutoService.Domain.Progression
{
    /// <summary>
    /// Tracks the player's XP and current level.
    /// </summary>
    public sealed class PlayerProgress
    {
        private readonly LevelTable _table;

        public PlayerProgress(LevelTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            Xp = 0;
            Level = 1;
        }

        public int Xp { get; private set; }
        public int Level { get; private set; }

        public event Action<PlayerProgress> Changed;
        public event Action<PlayerProgress, int> LeveledUp;

        public void AddXp(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "XP amount must be greater than zero.");

            Xp += amount;
            
            while (Level < _table.LevelForXp(Xp))
            {
                Level++;
                LeveledUp?.Invoke(this, Level);
            }
            
            Changed?.Invoke(this);
        }

        public void Restore(int xp)
        {
            if (xp < 0)
                throw new ArgumentOutOfRangeException(nameof(xp), "XP cannot be negative.");

            Xp = xp;
            Level = _table.LevelForXp(Xp);
            
            // Why: Load from save should not trigger popups and rewards for leveled up.
            Changed?.Invoke(this);
        }

        public float LevelProgress01
        {
            get
            {
                int currentLevelXp = _table.XpForLevel(Level);
                int nextLevelXp = _table.XpForLevel(Level + 1);
                
                int xpIntoLevel = Xp - currentLevelXp;
                int xpRequiredForLevel = nextLevelXp - currentLevelXp;
                
                if (xpRequiredForLevel == 0)
                    return 1f;

                return (float)xpIntoLevel / xpRequiredForLevel;
            }
        }

        public int XpToNextLevel
        {
            get
            {
                int nextLevelXp = _table.XpForLevel(Level + 1);
                return nextLevelXp - Xp;
            }
        }
    }
}
