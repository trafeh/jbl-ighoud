using UnityEngine;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Persistent player profile (PlayerPrefs): name, level, XP, objective progress and last position.
    /// START GAME wipes it and requests the intro cutscene; CONTINUE just loads it.
    /// </summary>
    public static class JbelPlayerProfile
    {
        private const string K_HAS = "JBEL_PROFILE_HAS";
        private const string K_NAME = "JBEL_PROFILE_NAME";
        private const string K_LEVEL = "JBEL_PROFILE_LEVEL";
        private const string K_XP = "JBEL_PROFILE_XP";
        private const string K_OBJ = "JBEL_PROFILE_OBJECTIVE";
        private const string K_OBJ_PROG = "JBEL_PROFILE_OBJECTIVE_PROGRESS";
        private const string K_INTRO = "JBEL_PROFILE_INTRO_DONE";
        private const string K_POS_HAS = "JBEL_PROFILE_POS_HAS";
        private const string K_POS_X = "JBEL_PROFILE_POS_X";
        private const string K_POS_Y = "JBEL_PROFILE_POS_Y";
        private const string K_POS_Z = "JBEL_PROFILE_POS_Z";
        private const string K_POS_YAW = "JBEL_PROFILE_POS_YAW";

        public const string DefaultName = "WANDERER";
        public const int MaxNameLength = 14;

        /// <summary>Set by the start menu right before loading the game scene.</summary>
        public static bool PendingNewGame;

        public static bool HasProfile => PlayerPrefs.GetInt(K_HAS, 0) == 1;
        public static bool IntroDone => PlayerPrefs.GetInt(K_INTRO, 0) == 1;

        public static string PlayerName
        {
            get => PlayerPrefs.GetString(K_NAME, DefaultName);
            set { PlayerPrefs.SetString(K_NAME, SanitizeName(value)); Touch(); }
        }

        public static int Level => PlayerPrefs.GetInt(K_LEVEL, 0);
        public static int XP => PlayerPrefs.GetInt(K_XP, 0);
        public static int ObjectiveIndex => PlayerPrefs.GetInt(K_OBJ, 0);
        public static int ObjectiveProgress => PlayerPrefs.GetInt(K_OBJ_PROG, 0);

        /// <summary>XP needed to go from <paramref name="level"/> to level+1.</summary>
        public static int XPToNext(int level) => 100 + 50 * Mathf.Max(0, level);

        public static float XPProgress01 => Mathf.Clamp01(XP / (float)XPToNext(Level));

        public static void StartNewGame()
        {
            PlayerPrefs.SetInt(K_HAS, 1);
            PlayerPrefs.SetString(K_NAME, DefaultName);
            PlayerPrefs.SetInt(K_LEVEL, 0);
            PlayerPrefs.SetInt(K_XP, 0);
            PlayerPrefs.SetInt(K_OBJ, 0);
            PlayerPrefs.SetInt(K_OBJ_PROG, 0);
            PlayerPrefs.SetInt(K_INTRO, 0);
            PlayerPrefs.SetInt(K_POS_HAS, 0);
            PlayerPrefs.Save();
            PendingNewGame = true;
        }

        public static void MarkIntroDone()
        {
            PlayerPrefs.SetInt(K_INTRO, 1);
            Touch();
        }

        /// <summary>Adds XP and handles level-ups. Returns how many levels were gained.</summary>
        public static int AddXP(int amount)
        {
            int level = Level, xp = XP + Mathf.Max(0, amount), gained = 0;
            while (xp >= XPToNext(level))
            {
                xp -= XPToNext(level);
                level++;
                gained++;
            }
            PlayerPrefs.SetInt(K_LEVEL, level);
            PlayerPrefs.SetInt(K_XP, xp);
            Touch();
            return gained;
        }

        public static void SetObjective(int index, int progress)
        {
            PlayerPrefs.SetInt(K_OBJ, index);
            PlayerPrefs.SetInt(K_OBJ_PROG, progress);
            Touch();
        }

        public static void SavePosition(Vector3 pos, float yaw)
        {
            PlayerPrefs.SetInt(K_POS_HAS, 1);
            PlayerPrefs.SetFloat(K_POS_X, pos.x);
            PlayerPrefs.SetFloat(K_POS_Y, pos.y);
            PlayerPrefs.SetFloat(K_POS_Z, pos.z);
            PlayerPrefs.SetFloat(K_POS_YAW, yaw);
            PlayerPrefs.Save();
        }

        public static bool TryGetPosition(out Vector3 pos, out float yaw)
        {
            pos = new Vector3(PlayerPrefs.GetFloat(K_POS_X), PlayerPrefs.GetFloat(K_POS_Y), PlayerPrefs.GetFloat(K_POS_Z));
            yaw = PlayerPrefs.GetFloat(K_POS_YAW);
            return PlayerPrefs.GetInt(K_POS_HAS, 0) == 1;
        }

        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DefaultName;
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'') sb.Append(c);
                if (sb.Length >= MaxNameLength) break;
            }
            string s = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " {2,}", " ").Trim();
            return s.Length == 0 ? DefaultName : s.ToUpperInvariant();
        }

        private static void Touch()
        {
            PlayerPrefs.SetInt(K_HAS, 1);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Jbel/Reset Player Profile (replay intro)")]
        private static void EditorReset()
        {
            StartNewGame();
            PendingNewGame = false;
            Debug.Log("[JbelProfile] Profile reset. Press Play in SampleScene to see the intro again.");
        }
#endif
    }
}
