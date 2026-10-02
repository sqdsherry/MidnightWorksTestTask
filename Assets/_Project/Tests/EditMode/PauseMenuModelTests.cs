using AutoService.Services.Menu;
using AutoService.Services.Scenes;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="PauseMenuModel"/>.</summary>
    public sealed class PauseMenuModelTests
    {
        private FakePauseService _pause;
        private FakeSceneLoader _scenes;
        private PauseMenuModel _model;

        [SetUp]
        public void SetUp()
        {
            _pause = new FakePauseService();
            _scenes = new FakeSceneLoader();
            _model = new PauseMenuModel(_pause, _scenes, null);
        }

        [Test]
        public void OpenClose_PushAndPopExactlyOnce()
        {
            _model.Open();

            Assert.IsTrue(_model.IsOpen);
            Assert.IsTrue(_pause.IsPaused);
            Assert.AreEqual(1, _pause.PushCount);

            _model.Close();

            Assert.IsFalse(_model.IsOpen);
            Assert.IsFalse(_pause.IsPaused);
            Assert.AreEqual(1, _pause.PopCount);
        }

        [Test]
        public void OpenTwice_PushesOnce()
        {
            _model.Open();
            _model.Open();

            Assert.AreEqual(1, _pause.PushCount);
        }

        [Test]
        public void CloseTwice_PopsOnce()
        {
            _model.Open();

            _model.Close();
            _model.Close();

            Assert.AreEqual(1, _pause.PopCount);
            Assert.IsFalse(_pause.IsPaused);
        }

        [Test]
        public void CloseWithoutOpen_DoesNotPop()
        {
            _model.Close();

            Assert.AreEqual(0, _pause.PopCount);
        }

        [Test]
        public void ToMainMenu_ReleasesPauseAndLoadsMenu()
        {
            _model.Open();

            _model.ToMainMenu();

            Assert.IsFalse(_model.IsOpen);
            Assert.IsFalse(_pause.IsPaused);
            Assert.AreEqual(1, _pause.PopCount);
            CollectionAssert.AreEqual(new[] { GameScene.MainMenu }, _scenes.Loaded);
        }

        [Test]
        public void ToMainMenu_SavesBeforeLoadingTheMenu()
        {
            int loadedWhenSaved = -1;
            var saver = new FakeGameSaver(() => loadedWhenSaved = _scenes.Loaded.Count);
            var model = new PauseMenuModel(_pause, _scenes, saver);
            model.Open();

            model.ToMainMenu();

            Assert.AreEqual(1, saver.SaveCount);
            Assert.AreEqual(0, loadedWhenSaved, "Saved while the gameplay scene was still loaded.");
            CollectionAssert.AreEqual(new[] { GameScene.MainMenu }, _scenes.Loaded);
        }

        [Test]
        public void SaveProgress_SavesOnce_AndWorksWithoutSaver()
        {
            var saver = new FakeGameSaver(null);
            new PauseMenuModel(_pause, _scenes, saver).SaveProgress();

            Assert.AreEqual(1, saver.SaveCount);
            Assert.DoesNotThrow(() => _model.SaveProgress());
        }
    }
}
