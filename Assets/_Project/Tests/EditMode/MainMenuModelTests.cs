using AutoService.Services.Menu;
using AutoService.Services.Save;
using AutoService.Services.Scenes;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="MainMenuModel"/>.</summary>
    public sealed class MainMenuModelTests
    {
        private FakeSaveService _save;
        private FakeSceneLoader _scenes;
        private MainMenuModel _model;

        [SetUp]
        public void SetUp()
        {
            _save = new FakeSaveService();
            _scenes = new FakeSceneLoader();
            _model = new MainMenuModel(_save, _scenes);
        }

        [Test]
        public void CanContinue_FollowsHasSave()
        {
            Assert.IsFalse(_model.CanContinue);

            _save.Stored = SaveData.CreateEmpty();

            Assert.IsTrue(_model.CanContinue);
        }

        [Test]
        public void Continue_LoadsGameplay()
        {
            _save.Stored = SaveData.CreateEmpty();

            _model.Continue();

            CollectionAssert.AreEqual(new[] { GameScene.Gameplay }, _scenes.Loaded);
            Assert.AreEqual(0, _save.DeleteCount);
        }

        [Test]
        public void NewGame_WithoutSave_LoadsGameplay()
        {
            NewGameResult result = _model.NewGame();

            Assert.AreEqual(NewGameResult.Started, result);
            CollectionAssert.AreEqual(new[] { GameScene.Gameplay }, _scenes.Loaded);
        }

        [Test]
        public void NewGame_WithSave_NeedsConfirmationAndChangesNothing()
        {
            var saved = SaveData.CreateEmpty();
            _save.Stored = saved;

            NewGameResult result = _model.NewGame();

            Assert.AreEqual(NewGameResult.NeedsConfirmation, result);
            Assert.IsEmpty(_scenes.Loaded);
            Assert.AreEqual(0, _save.DeleteCount);
            Assert.AreSame(saved, _save.Stored);
        }

        [Test]
        public void ConfirmNewGame_DeletesSaveAndLoadsGameplay()
        {
            _save.Stored = SaveData.CreateEmpty();
            _model.NewGame();

            _model.ConfirmNewGame();

            Assert.AreEqual(1, _save.DeleteCount);
            Assert.IsFalse(_save.HasSave);
            CollectionAssert.AreEqual(new[] { GameScene.Gameplay }, _scenes.Loaded);
        }
    }
}
