using AutoService.Services.Settings;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="SettingsService"/> and <see cref="GameSettings"/> validation.</summary>
    public sealed class SettingsServiceTests
    {
        private FakeSettingsStore _store;
        private FakeSettingsApplier _applier;
        private GameSettings _defaults;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeSettingsStore();
            _applier = new FakeSettingsApplier();
            _defaults = new GameSettings(0.7f, 0.8f, 2, true, 0, 0);
        }

        [Test]
        public void NothingSaved_UsesDefaultsAndAppliesThemOnInitialize()
        {
            var service = new SettingsService(_store, _applier, _defaults);

            Assert.AreSame(_defaults, service.Current);
            Assert.AreEqual(0, _applier.Applied.Count);

            service.Initialize();

            Assert.AreEqual(1, _applier.Applied.Count);
            Assert.AreSame(_defaults, _applier.Applied[0]);
        }

        [Test]
        public void SavedSettings_OverrideDefaults()
        {
            var saved = new GameSettings(0.1f, 0.2f, 0, false, 1280, 720);
            _store.Stored = saved;

            var service = new SettingsService(_store, _applier, _defaults);

            Assert.AreSame(saved, service.Current);
        }

        [Test]
        public void Set_SavesAppliesAndRaisesChanged()
        {
            var service = new SettingsService(_store, _applier, _defaults);
            GameSettings raised = null;
            service.Changed += settings => raised = settings;
            GameSettings updated = _defaults.WithMusicVolume(0.3f);

            service.Set(updated);

            Assert.AreSame(updated, service.Current);
            Assert.AreEqual(1, _store.SaveCount);
            Assert.AreSame(updated, _store.Stored);
            Assert.AreEqual(1, _applier.Applied.Count);
            Assert.AreSame(updated, _applier.Applied[0]);
            Assert.AreSame(updated, raised);
        }

        [Test]
        public void GameSettings_ClampsVolumesAndInvalidValues()
        {
            var settings = new GameSettings(1.5f, -0.2f, -3, false, 1920, 0);

            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0f, settings.SfxVolume);
            Assert.AreEqual(0, settings.QualityLevel);
            Assert.AreEqual(0, settings.ResolutionWidth);
            Assert.AreEqual(0, settings.ResolutionHeight);
            Assert.AreEqual(0f, settings.WithMusicVolume(float.NaN).MusicVolume);
        }

        [Test]
        public void GameSettings_WithReturnsModifiedCopy()
        {
            GameSettings louder = _defaults.WithSfxVolume(2f);

            Assert.AreNotSame(_defaults, louder);
            Assert.AreEqual(1f, louder.SfxVolume);
            Assert.AreEqual(0.8f, _defaults.SfxVolume);
            Assert.AreEqual(_defaults.MusicVolume, louder.MusicVolume);
            Assert.AreEqual(_defaults.QualityLevel, louder.QualityLevel);
            Assert.AreEqual(_defaults.Fullscreen, louder.Fullscreen);
        }
    }
}
