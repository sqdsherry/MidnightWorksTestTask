using AutoService.Domain.Common;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Player;
using AutoService.Presentation.Popups;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Progression;
using AutoService.Services.Save;
using AutoService.Services.Scenes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AutoService.Presentation.Ui
{
    /// <summary>
    /// Debug panel for reviewers, toggled with F1 or the corner button: money, an instant level-up, teleports between
    /// locations and a progress reset. Button labels are set in the scene.
    /// </summary>
    /// <remarks>Ticked by the game loop (F1 polling); works without any of its optional services, just with fewer actions.</remarks>
    public sealed class DebugCheatView : MonoBehaviour, IEscapeHandler, ITickable
    {
        private const long SmallCashAmount = 1000L;
        private const long BigCashAmount = 10000L;

        [Header("Roots")]
        [SerializeField]
        [Tooltip("The panel containing the cheat buttons.")]
        private GameObject _panelRoot;

        [SerializeField]
        [Tooltip("Quick toggle button in the corner of HUD.")]
        private Button _toggleButton;

        [SerializeField]
        [Tooltip("Button inside the panel to close it.")]
        private Button _closeButton;

        [Header("Action Buttons")]
        [SerializeField]
        [Tooltip("Adds $1,000 to the player's wallet.")]
        private Button _add1000Button;

        [SerializeField]
        [Tooltip("Adds $10,000 to the player's wallet.")]
        private Button _add10000Button;

        [SerializeField]
        [Tooltip("Adds enough XP to instantly trigger a Level-Up.")]
        private Button _levelUpButton;

        [SerializeField]
        [Tooltip("Instantly teleports the player to Location 1.")]
        private Button _teleportLoc1Button;

        [SerializeField]
        [Tooltip("Instantly teleports the player to Location 2.")]
        private Button _teleportLoc2Button;

        [SerializeField]
        [Tooltip("Deletes the save and restarts the gameplay scene.")]
        private Button _resetProgressButton;

        private IWalletService _wallet;
        private IProgressionService _progression;
        private PlayerTeleporter _teleporter;
        private ISaveService _saveService;
        private IGameSaver _gameSaver;
        private ISceneLoader _sceneLoader;
        private IPauseService _pauseService;
        private EscapeRouter _escapeRouter;

        /// <summary>True if the cheat panel is open.</summary>
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        /// <summary>Injects the services the actions use; any of them may be null (that action then does nothing).</summary>
        public void Construct(
            IWalletService wallet,
            IProgressionService progression,
            PlayerTeleporter teleporter,
            ISaveService saveService,
            IGameSaver gameSaver,
            ISceneLoader sceneLoader,
            IPauseService pauseService,
            EscapeRouter escapeRouter)
        {
            _wallet = wallet;
            _progression = progression;
            _teleporter = teleporter;
            _saveService = saveService;
            _gameSaver = gameSaver;
            _sceneLoader = sceneLoader;
            _pauseService = pauseService;
            _escapeRouter = escapeRouter;
        }

        private void Awake()
        {
            AddListener(_toggleButton, TogglePanel);
            AddListener(_closeButton, ClosePanel);
            AddListener(_add1000Button, OnAddSmallCash);
            AddListener(_add10000Button, OnAddBigCash);
            AddListener(_levelUpButton, OnLevelUp);
            AddListener(_teleportLoc1Button, OnTeleportLoc1);
            AddListener(_teleportLoc2Button, OnTeleportLoc2);
            AddListener(_resetProgressButton, OnResetProgress);

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            RemoveListener(_toggleButton, TogglePanel);
            RemoveListener(_closeButton, ClosePanel);
            RemoveListener(_add1000Button, OnAddSmallCash);
            RemoveListener(_add10000Button, OnAddBigCash);
            RemoveListener(_levelUpButton, OnLevelUp);
            RemoveListener(_teleportLoc1Button, OnTeleportLoc1);
            RemoveListener(_teleportLoc2Button, OnTeleportLoc2);
            RemoveListener(_resetProgressButton, OnResetProgress);
            _escapeRouter?.Remove(this);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                TogglePanel();
            }
        }

        /// <summary>Toggles panel visibility.</summary>
        public void TogglePanel()
        {
            if (IsOpen)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }

        /// <summary>Opens the panel.</summary>
        public void OpenPanel()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
                _escapeRouter?.Push(this);
            }
        }

        /// <summary>Closes the panel.</summary>
        public void ClosePanel()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
                _escapeRouter?.Remove(this);
            }
        }

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            if (!IsOpen)
            {
                return false;
            }

            ClosePanel();
            return true;
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void OnAddSmallCash() => _wallet?.Add(new Money(SmallCashAmount));

        private void OnAddBigCash() => _wallet?.Add(new Money(BigCashAmount));

        private void OnLevelUp()
        {
            _progression?.AddExperience(Mathf.Max(1, _progression.XpToNextLevel));
        }

        private void OnTeleportLoc1() => _teleporter?.TeleportToLocation(0);

        private void OnTeleportLoc2() => _teleporter?.TeleportToLocation(1);

        private void OnResetProgress()
        {
            // Why: stop the coordinator first, or it would save the current game again while the scene unloads.
            _gameSaver?.ResetProgress();
            _saveService?.Delete();

            // Why: only the game's own flag — PlayerPrefs also hold the player's audio and video settings.
            Location2WelcomePresenter.ResetShownFlag();

            _pauseService?.ResetAll();
            _sceneLoader?.Load(GameScene.Gameplay);
        }
    }
}
