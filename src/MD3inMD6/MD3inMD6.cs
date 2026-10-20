// MD3-in-MD6
// BepInEx 5 / Harmony plugin that swaps MDVI's versus attack and refill rules for the
// ones used by Magical Drop III (Neo Geo, magdrop3). Values come from the MD3 68k code:
//   attack level   0x159FE / 0x15972
//   line tables    0x175C6 (pointers) -> 0x175D2 / 0x17612 / 0x17652, character map 0x17692
//   delivery/merge 0x15702-0x157EA
//   refill check   0x12A0A-0x12A1C (field total < 20 drops -> push now)
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MD3inMD6
{
    [BepInPlugin(Guid, "MD3 in MD6", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "muki.md6.md3inmd6";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> Md3Attacks;
        internal static ConfigEntry<bool> MergePendingAttacks;
        internal static ConfigEntry<string> ForceTable;
        internal static ConfigEntry<bool> AutoRefill;
        internal static ConfigEntry<int> RefillThreshold;
        internal static ConfigEntry<bool> RefillInSurvival;
        internal static ConfigEntry<bool> RefillInCaravan;
        internal static ConfigEntry<bool> ApplyOnline;
        internal static ConfigEntry<bool> DebugLog;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Master switch for every change below.");
            ApplyOnline = Config.Bind("General", "ApplyOnline", false,
                "Also use these rules in online matches. Both players must run the same mod and settings, or the two sides will disagree about attacks.");
            DebugLog = Config.Bind("General", "DebugLog", false, "Write every attack and refill decision to the BepInEx log.");

            Md3Attacks = Config.Bind("Attacks", "Md3Attacks", true,
                "Lines sent = MD3 table[level], level = (orbs-3)/4 + 4*(chain-1), capped at 31. Big single clears (7+ orbs) attack too, and attacks reach 8 lines.");
            MergePendingAttacks = Config.Bind("Attacks", "MergePendingAttacks", true,
                "MD3 behaviour: if an attack arrives while an earlier one is still waiting to drop, the newer attacks add their LEVELS together (max 31) into one second attack instead of stacking lines.");
            ForceTable = Config.Bind("Attacks", "ForceTable", "Character",
                new ConfigDescription("Which MD3 line table to use. Character = the character's MDVI attack group (Low=Weak, Middle=Medium, High=Strong).",
                    new AcceptableValueList<string>("Character", "Weak", "Medium", "Strong")));

            AutoRefill = Config.Bind("Refill", "AutoRefill", true,
                "MD3 behaviour: while the field holds fewer than RefillThreshold drops, push a new line right away instead of waiting for the timer. Not done while any column is one row from the bottom.");
            RefillThreshold = Config.Bind("Refill", "RefillThreshold", 20,
                new ConfigDescription("Drops on a 7-column field below which the field refills (MD3 uses 20). Scaled for other widths.",
                    new AcceptableValueRange<int>(0, 70)));
            RefillInSurvival = Config.Bind("Refill", "RefillInSurvival", false, "Also refill this way in Survival (Endless).");
            RefillInCaravan = Config.Bind("Refill", "RefillInCaravan", false, "Also refill this way in Caravan.");

            var h = new Harmony(Guid);
            h.Patch(AccessTools.Method(typeof(PatternController), "PrepareAttack"),
                prefix: new HarmonyMethod(typeof(AttackPatches), nameof(AttackPatches.PrepareAttackPrefix)));
            h.Patch(AccessTools.Method(typeof(PatternController), "CalculateLinesSend"),
                prefix: new HarmonyMethod(typeof(AttackPatches), nameof(AttackPatches.CalculateLinesSendPrefix)));
            h.Patch(AccessTools.Method(typeof(GridMechanics), "ReceiveLinesFromOpponent"),
                prefix: new HarmonyMethod(typeof(AttackPatches), nameof(AttackPatches.ReceiveLinesPrefix)));
            h.Patch(AccessTools.Method(typeof(GridMechanics), "Initialize"),
                postfix: new HarmonyMethod(typeof(AttackPatches), nameof(AttackPatches.ResetPostfix)));
            h.Patch(AccessTools.Method(typeof(GridMechanics), "InitialSpawn"),
                postfix: new HarmonyMethod(typeof(AttackPatches), nameof(AttackPatches.ResetPostfix)));
            h.Patch(AccessTools.Method(typeof(GridMechanics), "GamemodeBehaviour"),
                postfix: new HarmonyMethod(typeof(RefillPatches), nameof(RefillPatches.GamemodeBehaviourPostfix)));

            Log.LogInfo("MD3-in-MD6 " + Version + " loaded.");
        }

        internal static bool IsOnline(GridMechanics g)
        {
            try { return g != null && g.GridController != null && g.GridController.GridSceneController != null && g.GridController.GridSceneController.IsOnline; }
            catch { return false; }
        }

        internal static bool Active(GridMechanics g)
        {
            if (!Enabled.Value) return false;
            if (!ApplyOnline.Value && IsOnline(g)) return false;
            return true;
        }
    }

    internal static class Md3
    {
        // Lines per attack level 0..31, low bytes of the MD3 tables.
        internal static readonly int[] Weak =   { 0,1,1,2,2,3,3,3,3,4,4,4,4,4,4,5,5,5,5,5,6,6,6,6,6,7,7,7,7,7,8,8 };
        internal static readonly int[] Medium = { 0,2,2,3,3,3,4,4,4,4,5,5,5,5,5,6,6,6,6,6,7,7,7,7,7,7,8,8,8,8,8,8 };
        internal static readonly int[] Strong = { 0,2,3,3,4,4,4,4,5,5,5,5,5,6,6,6,6,6,6,7,7,7,7,7,8,8,8,8,8,8,8,8 };

        internal static int Level(int chain, int orbs)
        {
            int lv = orbs - 3;
            if (lv <= 0) return 0;
            lv >>= 2;
            if (chain >= 2) lv += 4 * (chain - 1);
            return lv > 31 ? 31 : lv;
        }

        internal static int[] Table(LineQuantityGroupID group)
        {
            switch (Plugin.ForceTable.Value)
            {
                case "Weak": return Weak;
                case "Medium": return Medium;
                case "Strong": return Strong;
            }
            switch (group)
            {
                case LineQuantityGroupID.Low: return Weak;
                case LineQuantityGroupID.High: return Strong;
                default: return Medium;
            }
        }

        // MDVI only hands CalculateLinesSend the combined score, so recover the orb count:
        // score = orbs*2 + chainBonus(chain)  (PlayerScoreGeneral.GetCalculatedScore).
        internal static int OrbsFromScore(int chain, int score)
        {
            int bonus = PlayerScoreGeneral.GetCalculatedScore(chain, 0);
            int orbs = (score - bonus) / 2;
            return orbs < 0 ? 0 : orbs;
        }
    }

    internal class PendingState
    {
        public List<PatternRow> First = new List<PatternRow>();
        public List<PatternRow> Second = new List<PatternRow>();
        public int SecondLevel;
    }

    internal static class AttackPatches
    {
        // Set while one attack is being prepared, read when it lands on the opponent.
        private static CharacterDataAsset lastAttacker;
        private static int lastLevel = -1;
        private static int forcedLines = -1;
        private static PatternController currentController;

        private static readonly Dictionary<GridMechanics, PendingState> pending = new Dictionary<GridMechanics, PendingState>();

        public static void PrepareAttackPrefix(PatternController __instance, CharacterDataAsset characterData)
        {
            lastAttacker = characterData;
            currentController = __instance;
        }

        public static bool CalculateLinesSendPrefix(LineQuantityGroupID quantityGroupID, int chain, int score, ref int __result)
        {
            if (forcedLines >= 0)
            {
                __result = forcedLines;
                return false;
            }
            if (!Plugin.Enabled.Value || !Plugin.Md3Attacks.Value) { lastLevel = -1; return true; }
            GridMechanics owner = OwnerOf(currentController);
            if (owner != null && !Plugin.Active(owner)) { lastLevel = -1; return true; }

            int orbs = Md3.OrbsFromScore(chain, score);
            int level = Md3.Level(chain, orbs);
            __result = Md3.Table(quantityGroupID)[level];
            lastLevel = level;
            if (Plugin.DebugLog.Value)
                Plugin.Log.LogInfo(string.Format("attack: chain {0}, orbs {1}, group {2} -> level {3}, {4} lines", chain, orbs, quantityGroupID, level, __result));
            return false;
        }

        public static bool ReceiveLinesPrefix(GridMechanics __instance, PatternRow[] lines)
        {
            int level = lastLevel;
            lastLevel = -1;
            if (!Plugin.Active(__instance) || !Plugin.Md3Attacks.Value || !Plugin.MergePendingAttacks.Value) return true;
            if (lines == null || lines.Length == 0 || level < 0 || lastAttacker == null) return true;

            PendingState st;
            if (!pending.TryGetValue(__instance, out st)) { st = new PendingState(); pending[__instance] = st; }

            Queue<PatternRow> q = __instance._spawnQueue;
            var inQueue = new HashSet<PatternRow>(ReferenceComparer.Instance);
            foreach (PatternRow r in q) inQueue.Add(r);
            st.First.RemoveAll(r => !inQueue.Contains(r));
            st.Second.RemoveAll(r => !inQueue.Contains(r));
            if (st.First.Count == 0)
            {
                // First attack has fully dropped: the merged second one becomes the first.
                st.First = st.Second;
                st.Second = new List<PatternRow>();
                st.SecondLevel = 0;
            }

            if (st.First.Count == 0)
            {
                foreach (PatternRow r in lines) q.Enqueue(r);
                st.First = new List<PatternRow>(lines);
                if (Plugin.DebugLog.Value) Plugin.Log.LogInfo("grid " + __instance.GridController.GridID + ": attack queued, " + lines.Length + " lines");
                return false;
            }

            // An attack is still waiting: add levels into the second attack (MD3 0x15764).
            st.SecondLevel = Math.Min(31, st.SecondLevel + level);
            int n = Md3.Table(lastAttacker.LineQuantityGroup)[st.SecondLevel];

            if (st.Second.Count > 0)
            {
                var drop = new HashSet<PatternRow>(st.Second, ReferenceComparer.Instance);
                var keep = new List<PatternRow>(q.Count);
                foreach (PatternRow r in q) if (!drop.Contains(r)) keep.Add(r);
                q.Clear();
                foreach (PatternRow r in keep) q.Enqueue(r);
            }

            PatternRow[] rows = BuildRows(__instance._patternController, lastAttacker, n);
            st.Second = new List<PatternRow>(rows);
            foreach (PatternRow r in rows) q.Enqueue(r);
            if (Plugin.DebugLog.Value)
                Plugin.Log.LogInfo("grid " + __instance.GridController.GridID + ": merged into pending attack, level " + st.SecondLevel + " -> " + n + " lines");
            return false;
        }

        public static void ResetPostfix(GridMechanics __instance)
        {
            pending.Remove(__instance);
            if (__instance._patternController != null) owners[__instance._patternController] = __instance;
            var dead = new List<GridMechanics>();
            foreach (var k in pending.Keys) if (!k) dead.Add(k);
            foreach (var k in dead) pending.Remove(k);
            var deadPc = new List<PatternController>();
            foreach (var kv in owners) if (!kv.Key || !kv.Value) deadPc.Add(kv.Key);
            foreach (var k in deadPc) owners.Remove(k);
        }

        // Reuse the game's own row builder (character patterns, competitive mask) with a fixed count.
        private static PatternRow[] BuildRows(PatternController pc, CharacterDataAsset attacker, int n)
        {
            if (n <= 0) return new PatternRow[0];
            forcedLines = n;
            try { pc.PrepareAttack(attacker, 2, 0); }
            finally { forcedLines = -1; }
            return pc.AttackPattern ?? new PatternRow[0];
        }

        private static readonly Dictionary<PatternController, GridMechanics> owners = new Dictionary<PatternController, GridMechanics>();

        private static GridMechanics OwnerOf(PatternController pc)
        {
            GridMechanics g;
            return pc != null && owners.TryGetValue(pc, out g) ? g : null;
        }
    }

    internal static class RefillPatches
    {
        private static readonly AccessTools.FieldRef<GridMechanics, GameModeScript.GameModeGridBehaviour> behaviourRef =
            AccessTools.FieldRefAccess<GridMechanics, GameModeScript.GameModeGridBehaviour>("_GameModeGridBehaviour");

        private static bool ModeAllowed(GameModeScript.GameModeGridBehaviour b)
        {
            if (b is VersusOnlineGameMode.VersusOnlineGridBehaviour) return Plugin.ApplyOnline.Value;
            if (b is VersusGameMode.VersusGridBehaviour) return true;
            if (b is MatchVersusAIGameMode.MatchVersusAiGridBehaviour) return true;
            if (b is SurvivalGameMode.SurvivalGridBehaviour) return Plugin.RefillInSurvival.Value;
            if (b is CaravanGameMode.CaravanGridBehaviour) return Plugin.RefillInCaravan.Value;
            return false;
        }

        public static void GamemodeBehaviourPostfix(GridMechanics __instance)
        {
            if (!Plugin.Active(__instance) || !Plugin.AutoRefill.Value) return;
            if (!ModeAllowed(behaviourRef(__instance))) return;
            if (__instance._spawnQueue.Count > 0) return;

            Drop[,] g = __instance.Grid;
            int w = g.GetLength(0);
            if (g.GetLength(1) < 3) return;
            for (int x = 0; x < w; x++)
                if ((object)g[x, 1] != null) return; // a column is one row from the bottom (MD3: height >= 11)

            int threshold = (Plugin.RefillThreshold.Value * w + 3) / 7;
            if (__instance.CurrentDropCount >= threshold) return;

            GridScoreController score = __instance.Score;
            if (score.spawnTimer > 0f)
            {
                score.spawnTimer = 0f; // the mode spawns its next line on the next idle frame
                if (Plugin.DebugLog.Value)
                    Plugin.Log.LogInfo("grid " + __instance.GridController.GridID + ": refill, " + __instance.CurrentDropCount + " drops < " + threshold);
            }
        }
    }

    internal sealed class ReferenceComparer : IEqualityComparer<PatternRow>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();
        public bool Equals(PatternRow a, PatternRow b) { return ReferenceEquals(a, b); }
        public int GetHashCode(PatternRow o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
    }
}
