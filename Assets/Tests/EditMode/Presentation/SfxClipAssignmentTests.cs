using MergeWater.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>
    /// M5：音效来源契约（需求方 2026-09-13：合成音改用真实素材 `Assets/Audios/pop.ogg`）。
    ///
    /// 在此之前 `AudioDirector` **没有**任何「SfxId → 真实 AudioClip」的指派入口，所有音效都现场
    /// 合成占位音（`PlaceholderAudioFactory`），因此把素材放进工程也听不到它。本用例固定两条不变量：
    /// ① 显式指派的真实素材必须优先于占位音（否则「拖进去了没反应」，且没有任何报错）；
    /// ② 未指派的 SfxId 仍回退占位音（不能因为引入指派表就把「无素材也能听到反馈」的降级砍掉）。
    /// </summary>
    public sealed class SfxClipAssignmentTests
    {
        private AudioClip _assigned;
        private GameObject _root;
        private SfxClipAssignmentHolder _holder;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SfxClipAssignmentTest");
            _holder = ScriptableObject.CreateInstance<SfxClipAssignmentHolder>();
            _assigned = AudioClip.Create("test_pop", 128, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);

            if (_holder != null)
                Object.DestroyImmediate(_holder);

            if (_assigned != null)
                Object.DestroyImmediate(_assigned);

            _root = null;
            _holder = null;
            _assigned = null;
        }

        /// <summary>把指派表写进序列化字段——**只能**这样注入，因为音频槽位是场景序列化数据。</summary>
        private AudioDirector CreateDirector(params SfxClipEntry[] entries)
        {
            var director = _root.AddComponent<AudioDirector>();
            _holder.entries = entries;

            var serialized = new UnityEditor.SerializedObject(director);
            serialized.FindProperty("sfxClips").arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = serialized.FindProperty("sfxClips").GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").enumValueIndex = (int)entries[i].id;
                element.FindPropertyRelative("clip").objectReferenceValue = entries[i].clip;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        [Test]
        public void GetClip_WithAssignedClip_ReturnsTheAssignedAssetInsteadOfPlaceholder()
        {
            var director = CreateDirector(new SfxClipEntry { id = SfxId.Merge, clip = _assigned });

            var clip = director.GetClip(SfxId.Merge);

            Assert.That(clip, Is.SameAs(_assigned),
                "指派了真实素材就必须用它；返回占位音 = 素材拖进工程也听不到，且不报错");
            Assert.That(clip.name, Is.EqualTo("test_pop"));
        }

        [Test]
        public void GetClip_WithComboUpSharingTheMergeAsset_ResolvesBothIds()
        {
            // V2.24 连击音阶靠 pitch 实现，因此 ComboUp 与 Merge 共用同一素材是预期用法。
            var director = CreateDirector(
                new SfxClipEntry { id = SfxId.Merge, clip = _assigned },
                new SfxClipEntry { id = SfxId.ComboUp, clip = _assigned });

            Assert.That(director.GetClip(SfxId.Merge), Is.SameAs(_assigned));
            Assert.That(director.GetClip(SfxId.ComboUp), Is.SameAs(_assigned));
        }

        [Test]
        public void GetClip_ForUnassignedId_StillFallsBackToPlaceholder()
        {
            var director = CreateDirector(new SfxClipEntry { id = SfxId.Merge, clip = _assigned });

            var placeholder = director.GetClip(SfxId.Fail);

            Assert.That(placeholder, Is.Not.Null, "未指派的音效必须有占位音兜底");
            Assert.That(placeholder, Is.Not.SameAs(_assigned), "兜底不能张冠李戴到已指派的素材上");
            Assert.That(placeholder.samples, Is.GreaterThan(0));
        }

        [Test]
        public void GetClip_WithNullEntry_DoesNotThrowAndFallsBack()
        {
            // 数组里留空槽位（Inspector 里删了素材、或只填了 id）不能把整条音效链路打崩。
            var director = CreateDirector(new SfxClipEntry { id = SfxId.Merge, clip = null });

            Assert.DoesNotThrow(() => director.GetClip(SfxId.Merge));
            Assert.That(director.GetClip(SfxId.Merge), Is.Not.Null, "空槽位仍应有占位音");
        }

        [Test]
        public void OnDestroy_DoesNotDestroyClipsThatComeFromProjectAssets()
        {
            // 回归：占位音是运行时 new 出来的，必须随组件销毁释放；而**工程资产**（pop.ogg / BGM）
            // 绝不能销毁——那会连带把 .ogg 资产本身删掉。
            var director = CreateDirector(new SfxClipEntry { id = SfxId.Merge, clip = _assigned });

            Object.DestroyImmediate(_root);
            _root = null;

            Assert.That(_assigned == null, Is.False, "随组件销毁工程资产 = 把 pop.ogg 本体删掉");
            Assert.That(_assigned.samples, Is.GreaterThan(0));
        }
    }

    /// <summary>为用例提供可序列化的指派表容器（`SfxClipEntry[]` 无法直接序列化在测试类上）。</summary>
    internal sealed class SfxClipAssignmentHolder : ScriptableObject
    {
        public SfxClipEntry[] entries;
    }
}
