using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>
    /// 局外可远程调整的开关（V2.16 的插屏开关等）。缺失时由 <see cref="Default"/> 提供默认值。
    /// </summary>
    [CreateAssetMenu(fileName = "MetaSettings", menuName = "MergeWater/Meta Settings")]
    public sealed class MetaSettings : ScriptableObject
    {
        [SerializeField] private bool interstitialEnabled = true;
        [SerializeField] private bool offlineGrantAll = true;

        public bool InterstitialEnabled => interstitialEnabled;

        /// <summary>无广告适配器时是否允许直接领取（离线演示，决策 D6）。</summary>
        public bool OfflineGrantAll => offlineGrantAll;

        public static MetaSettings CreateDefault()
        {
            return CreateInstance<MetaSettings>();
        }

        /// <summary>编辑器生成工具与测试用：写入开关值。</summary>
        public void Configure(bool interstitial, bool offlineGrant)
        {
            interstitialEnabled = interstitial;
            offlineGrantAll = offlineGrant;
        }
    }

    /// <summary>设置面板开关（R25）。变更即时生效并写入存档。</summary>
    public sealed class SettingsService
    {
        private readonly SaveService _save;

        public SettingsService(SaveService save)
        {
            _save = save;
        }

        public event System.Action Changed;

        public bool SfxEnabled => _save.Data.settingsSfx;

        public bool MusicEnabled => _save.Data.settingsMusic;

        public bool VibrateEnabled => _save.Data.settingsVibrate;

        public void SetSfx(bool enabled)
        {
            if (_save.Data.settingsSfx == enabled)
                return;

            _save.Data.settingsSfx = enabled;
            Persist();
        }

        public void SetMusic(bool enabled)
        {
            if (_save.Data.settingsMusic == enabled)
                return;

            _save.Data.settingsMusic = enabled;
            Persist();
        }

        public void SetVibrate(bool enabled)
        {
            if (_save.Data.settingsVibrate == enabled)
                return;

            _save.Data.settingsVibrate = enabled;
            Persist();
        }

        /// <summary>清除缓存：删除本地存档并回到默认值（R25）。</summary>
        public void ClearCache()
        {
            _save.ClearAll();
            _save.Save();
            Changed?.Invoke();
        }

        private void Persist()
        {
            _save.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>隐私合规门（R20）：同意前不放行埋点与广告。</summary>
    public sealed class PrivacyGate
    {
        private readonly SaveService _save;

        public PrivacyGate(SaveService save)
        {
            _save = save;
        }

        public bool IsAccepted => _save.Data.privacyAccepted;

        public void Accept()
        {
            if (_save.Data.privacyAccepted)
                return;

            _save.Data.privacyAccepted = true;
            _save.Save();
        }

        /// <summary>拒绝时不做任何初始化；游戏停留在隐私面板。</summary>
        public void Decline()
        {
        }
    }
}
