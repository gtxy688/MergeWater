using System;
using System.IO;
using System.Text.RegularExpressions;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>内存存档实现（测试与落盘失败时的降级）。</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private string _json;
        private string _backup;

        public InMemorySaveStore(string initialJson = null)
        {
            _json = initialJson;
        }

        public bool Exists => !string.IsNullOrEmpty(_json);

        public string BackupJson => _backup;

        public int WriteCount { get; private set; }

        public bool TryRead(out string json)
        {
            json = _json;
            return !string.IsNullOrEmpty(json);
        }

        public bool Write(string json)
        {
            _json = json;
            WriteCount++;
            return true;
        }

        public bool MoveToBackup()
        {
            if (string.IsNullOrEmpty(_json))
                return false;

            _backup = _json;
            _json = null;
            return true;
        }

        public bool Delete()
        {
            _json = null;
            return true;
        }
    }

    /// <summary>落盘实现：persistentDataPath 下的 JSON 文件，损坏时保留 .bak。</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private const string FileName = "mwater_save.json";
        private const string BackupSuffix = ".bak";

        public FileSaveStore(string fileName = FileName)
        {
            FilePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        }

        public string FilePath { get; }

        public bool Exists => File.Exists(FilePath);

        public bool TryRead(out string json)
        {
            json = null;
            try
            {
                if (!File.Exists(FilePath))
                    return false;

                json = File.ReadAllText(FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileSaveStore] 读取存档失败：{e.Message}");
                return false;
            }
        }

        public bool Write(string json)
        {
            try
            {
                File.WriteAllText(FilePath, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileSaveStore] 写入存档失败：{e.Message}");
                return false;
            }
        }

        public bool MoveToBackup()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return false;

                File.Copy(FilePath, FilePath + BackupSuffix, true);
                File.Delete(FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileSaveStore] 备份存档失败：{e.Message}");
                return false;
            }
        }

        public bool Delete()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileSaveStore] 删除存档失败：{e.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 存档加载/保存与版本迁移。Load 总能返回可用对象：损坏、未来版本、缺字段都会被
    /// 迁移或回退为默认值，绝不抛异常。
    /// </summary>
    public sealed class SaveService
    {
        private static readonly Regex DayKeyPattern = new Regex(@"^\d{4}-\d{2}-\d{2}$");

        private readonly ISaveStore _store;
        private readonly IClock _clock;

        public SaveService(ISaveStore store, IClock clock)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public SaveData Data { get; private set; }

        public bool Loaded { get; private set; }

        public bool LastLoadWasCorrupt { get; private set; }

        public bool LastLoadWasFutureSchema { get; private set; }

        public SaveData Load()
        {
            LastLoadWasCorrupt = false;
            LastLoadWasFutureSchema = false;

            if (!_store.TryRead(out var json) || string.IsNullOrEmpty(json))
            {
                Data = SaveData.CreateDefault();
                Loaded = true;
                return Data;
            }

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] 存档 JSON 损坏，已备份并回退默认值：{e.Message}");
                _store.MoveToBackup();
                LastLoadWasCorrupt = true;
                Data = SaveData.CreateDefault();
                Loaded = true;
                return Data;
            }

            if (parsed == null)
            {
                _store.MoveToBackup();
                LastLoadWasCorrupt = true;
                Data = SaveData.CreateDefault();
                Loaded = true;
                return Data;
            }

            if (parsed.schemaVersion > SaveData.CurrentSchemaVersion)
            {
                Debug.LogWarning(
                    $"[SaveService] 存档版本 {parsed.schemaVersion} 高于当前 {SaveData.CurrentSchemaVersion}，按默认值处理。");
                _store.MoveToBackup();
                LastLoadWasFutureSchema = true;
                Data = SaveData.CreateDefault();
                Loaded = true;
                return Data;
            }

            Data = Migrate(parsed);
            Validate(Data);
            Loaded = true;
            return Data;
        }

        public bool Save()
        {
            if (Data == null)
                Data = SaveData.CreateDefault();

            Data.schemaVersion = SaveData.CurrentSchemaVersion;
            return _store.Write(JsonUtility.ToJson(Data));
        }

        /// <summary>清除本地存档（设置页「清除缓存」）。</summary>
        public void ClearAll()
        {
            _store.Delete();
            Data = SaveData.CreateDefault();
        }

        private SaveData Migrate(SaveData data)
        {
            if (data.schemaVersion < 1)
            {
                // v0 → v1：早期原型没有列表字段，补齐后再进入后续迁移。
                data.leaderboard ??= new System.Collections.Generic.List<LeaderboardRecord>();
                data.schemaVersion = 1;
            }

            if (data.schemaVersion < 2)
            {
                // v1 → v2：引入每日领取记录与引导标记（缺字段时保持默认值即可）。
                data.schemaVersion = 2;
            }

            return data;
        }

        private void Validate(SaveData data)
        {
            data.bestScore = Mathf.Max(0, data.bestScore);
            data.bestCombo = Mathf.Max(0, data.bestCombo);
            data.gamesPlayed = Mathf.Max(0, data.gamesPlayed);
            data.tutorialRoundsSeen = Mathf.Max(0, data.tutorialRoundsSeen);

            data.undoCount = ClampItem(data.undoCount);
            data.bombCount = ClampItem(data.bombCount);
            data.hammerCount = ClampItem(data.hammerCount);
            data.shakeCount = ClampItem(data.shakeCount);

            data.undoGrantedToday = ClampItem(data.undoGrantedToday);
            data.bombGrantedToday = ClampItem(data.bombGrantedToday);
            data.hammerGrantedToday = ClampItem(data.hammerGrantedToday);
            data.shakeGrantedToday = ClampItem(data.shakeGrantedToday);
            data.giftGrantedToday = ClampItem(data.giftGrantedToday);

            if (data.leaderboard == null)
                data.leaderboard = new System.Collections.Generic.List<LeaderboardRecord>();

            for (var i = data.leaderboard.Count - 1; i >= 0; i--)
            {
                var record = data.leaderboard[i];
                if (record.score <= 0 || string.IsNullOrEmpty(record.name))
                    data.leaderboard.RemoveAt(i);
            }

            if (!DayKeyPattern.IsMatch(data.dailyKey ?? string.Empty))
                data.dailyKey = _clock.Today;

            if (data.lastReviveAdUtcTicks < 0)
                data.lastReviveAdUtcTicks = 0;

            if (data.lastShareUtcTicks < 0)
                data.lastShareUtcTicks = 0;
        }

        private static int ClampItem(int value) => Mathf.Clamp(value, 0, SaveData.MaxItemCount);
    }
}
