using AutoService.Presentation.Player;
using AutoService.Presentation.Popups;
using NUnit.Framework;
using UnityEngine;

namespace AutoService.Tests.EditMode
{
    public sealed class Location2WelcomePresenterTests
    {
        private GameObject _playerGo;
        private PlayerView _player;
        private GameObject _viewGo;
        private Location2WelcomePopupView _view;
        private Location2WelcomePresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(Location2WelcomePresenter.Loc2WelcomeShownKey);

            _playerGo = new GameObject("PlayerTest");
            _player = _playerGo.AddComponent<PlayerView>();

            _viewGo = new GameObject("WelcomePopupTest");
            _view = _viewGo.AddComponent<Location2WelcomePopupView>();

            // Why: a popup starts hidden in the scene; a new GameObject is active by default.
            _viewGo.SetActive(false);

            _presenter = new Location2WelcomePresenter(_player, _view, Vector3.zero, new Vector3(200f, 0f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(Location2WelcomePresenter.Loc2WelcomeShownKey);

            if (_playerGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_playerGo);
            }

            if (_viewGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_viewGo);
            }
        }

        [Test]
        public void PlayerOnLocation1_DoesNotTriggerWelcome()
        {
            _playerGo.transform.position = new Vector3(0f, 0f, 0f);
            _presenter.Tick(0.1f);

            Assert.IsFalse(_view.IsOpen);
            Assert.AreEqual(0, PlayerPrefs.GetInt(Location2WelcomePresenter.Loc2WelcomeShownKey, 0));
        }

        [Test]
        public void PlayerEntersLocation2_TriggersWelcomeAndSavesPref()
        {
            _playerGo.transform.position = new Vector3(150f, 0f, 0f);
            _presenter.Tick(0.1f);

            Assert.IsTrue(_view.IsOpen);
            Assert.AreEqual(1, PlayerPrefs.GetInt(Location2WelcomePresenter.Loc2WelcomeShownKey, 0));
        }

        [Test]
        public void WelcomeOnlyShownOnce()
        {
            _playerGo.transform.position = new Vector3(150f, 0f, 0f);
            _presenter.Tick(0.1f);
            Assert.IsTrue(_view.IsOpen);

            _view.Hide();
            Assert.IsFalse(_view.IsOpen);

            // Next tick should not reopen
            _presenter.Tick(0.1f);
            Assert.IsFalse(_view.IsOpen);
        }
    }
}
