using System;
using MergeWater.Meta;
using NUnit.Framework;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M6 验收 A1–A3：存档加载、损坏回退与版本迁移（V3）。</summary>
    public sealed class SaveServiceTests
    {
        private MetaTestContext _context;

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            _context = null;
        }

        [Test]
        public void Load_WhenNoFile_ReturnsDefaultWithCurrentSchema()
        {
            _context = new MetaTestContext();

            var data = _context.Save.Load();

            Assert.That(_context.Save.Loaded, Is.True);
            Assert.That(data, Is.Not.Null);
            Assert.That(data.schemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion));
            Assert.That(data.bestScore, Is.EqualTo(0));
            Assert.That(data.settingsSfx, Is.True);
            Assert.That(data.settingsMusic, Is.True);
            Assert.That(data.settingsVibrate, Is.True);
            Assert.That(_context.Save.LastLoadWasCorrupt, Is.False);
        }

        [Test]
        public void Load_CorruptedJson_BacksUpAndReturnsDefault()
        {
            const string corrupted = "{ this is not valid json";
            _context = new MetaTestContext(corrupted);

            var data = _context.Save.Load();

            Assert.That(_context.Save.LastLoadWasCorrupt, Is.True);
            Assert.That(data.bestScore, Is.EqualTo(0));
            Assert.That(_context.Store.BackupJson, Is.EqualTo(corrupted), "损坏存档应被保留为备份");
            Assert.That(_context.Store.Exists, Is.False, "损坏存档应被移走");
        }

        [Test]
        public void Load_OldSchema_MigratesAndClampsOutOfRangeValues()
        {
            const string legacy = "{\"schemaVersion\":1,\"bestScore\":-50,\"bestCombo\":-3," +
                                  "\"gamesPlayed\":-9,\"undoCount\":99999,\"bombCount\":-20," +
                                  "\"shakeCount\":5,\"privacyAccepted\":true}";
            _context = new MetaTestContext(legacy);

            var data = _context.Save.Load();

            Assert.That(data.schemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion), "旧版本应迁移到当前 schema");
            Assert.That(data.bestScore, Is.EqualTo(0), "负分被夹到 0");
            Assert.That(data.bestCombo, Is.EqualTo(0));
            Assert.That(data.gamesPlayed, Is.EqualTo(0));
            Assert.That(data.undoCount, Is.EqualTo(SaveData.MaxItemCount), "超上限被夹到上限");
            Assert.That(data.bombCount, Is.EqualTo(0));
            Assert.That(data.shakeCount, Is.EqualTo(5));
            Assert.That(data.privacyAccepted, Is.True, "已同意状态应保留");
            Assert.That(data.dailyKey, Is.EqualTo(_context.Clock.Today), "非法日期键应修正为今天");
        }

        [Test]
        public void Load_FutureSchema_FallsBackToDefaultAndKeepsBackup()
        {
            const string future = "{\"schemaVersion\":99,\"bestScore\":1234}";
            _context = new MetaTestContext(future);

            var data = _context.Save.Load();

            Assert.That(_context.Save.LastLoadWasFutureSchema, Is.True);
            Assert.That(data.bestScore, Is.EqualTo(0), "未来版本按默认值处理，不崩溃");
            Assert.That(_context.Store.BackupJson, Is.EqualTo(future));
        }

        [Test]
        public void Save_WritesCurrentSchemaVersionAndRoundTrips()
        {
            _context = new MetaTestContext();
            var data = _context.Save.Load();
            data.bestScore = 777;
            data.undoCount = 2;

            Assert.That(_context.Save.Save(), Is.True);

            var reloaded = new SaveService(_context.Store, _context.Clock).Load();
            Assert.That(reloaded.bestScore, Is.EqualTo(777));
            Assert.That(reloaded.undoCount, Is.EqualTo(2));
            Assert.That(reloaded.schemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion));
        }

        [Test]
        public void ClearAll_DeletesFileAndResetsToDefault()
        {
            _context = new MetaTestContext();
            var data = _context.Save.Load();
            data.bestScore = 500;
            _context.Save.Save();

            _context.Save.ClearAll();

            Assert.That(_context.Store.Exists, Is.False);
            Assert.That(_context.Save.Data.bestScore, Is.EqualTo(0));
            Assert.That(_context.Save.Data.schemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion));
        }

    }
}
