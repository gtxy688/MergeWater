using System;
using System.Collections.Generic;

namespace MergeWater.Meta
{

    /// <summary>
    /// 本地存档数据。字段为 public 以便 JsonUtility 序列化；schema 版本用于迁移。
    /// 运行时可变状态只存在这里，不写回 ScriptableObject 资产。
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 2;
        public const int MaxItemCount = 999;

        /// <summary>0 表示来源更早（缺少版本字段）的存档，必须迁移。</summary>
        public int schemaVersion;

        public int bestScore;
        public int bestCombo;
        public int gamesPlayed;
        public bool privacyAccepted;

        public string dailyKey = string.Empty;
        public int undoGrantedToday;
        public int bombGrantedToday;
        public int hammerGrantedToday;
        public int shakeGrantedToday;
        public int giftGrantedToday;

        public int undoCount;
        public int bombCount;
        public int hammerCount;
        public int shakeCount;

        public int interstitialLastShownGame = -999;

        public long lastReviveAdUtcTicks;
        public long lastShareUtcTicks;

        public int tutorialRoundsSeen;
        public bool tutorialDropHintSeen;
        public bool tutorialMergeHighlightSeen;

        public bool settingsSfx = true;
        public bool settingsMusic = true;
        public bool settingsVibrate = true;

        /// <summary>
        /// 音量条（0..1，2026-09-12 补回设置页音量条）。用字段默认值保证旧存档缺字段时按 100% 处理，
        /// 不需要新增 schema 迁移步骤（JsonUtility 缺字段保留字段初始值）。
        /// </summary>
        public float settingsSfxVolume = 1f;

        public float settingsMusicVolume = 1f;


        public static SaveData CreateDefault()
        {
            return new SaveData { schemaVersion = CurrentSchemaVersion };
        }
    }
}
