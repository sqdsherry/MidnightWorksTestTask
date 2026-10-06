using System;
using AutoService.Domain.Common;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Player;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Progression;
using AutoService.Services.Save;
using AutoService.Services.Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoService.Presentation.Ui
{
    /// <summary>
    /// Debug cheat panel toggled via F1 or corner button, allowing instant progression, currency additions,
    /// teleportation between locations, and save state reset.
    /// </summary>
    public sealed class DebugCheatView : MonoBehaviour, IEscapeHandler
    {
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
        [Tooltip("Wipes saves and PlayerPrefs, reloading current scene.")]
        private Button _resetProgressButton;

        private IWalletService _wallet;
        private IProgressionService _progression;
        private PlayerView _player;
        private CameraRig _cameraRig;
        private ISaveService _saveService;
        private IGameSaver _gameSaver;
        private ISceneLoader _sceneLoader;
        private IPauseService _pauseService;
        private EscapeRouter _escapeRouter;

        /// <summary>True if the cheat panel is open.</summary>
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        /// <summary>Injects runtime services needed for cheat actions.</summary>
        public void Initialize(
            IWalletService wallet,
            IProgressionService progression,
            PlayerView player,
            CameraRig cameraRig,
            ISaveService saveService = null,
            IGameSaver gameSaver = null,
            ISceneLoader sceneLoader = null,
            IPauseService pauseService = null,
            EscapeRouter escapeRouter = null)
        {
            _wallet = wallet;
            _progression = progression;
            _player = player;
            _cameraRig = cameraRig;
            _saveService = saveService;
            _gameSaver = gameSaver;
            _sceneLoader = sceneLoader;
            _pauseService = pauseService;
            _escapeRouter = escapeRouter;
        }

        private void Awake()
        {
            if (_toggleButton != null)
            {
                _toggleButton.onClick.AddListener(TogglePanel);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(ClosePanel);
            }

            if (_add1000Button != null)
            {
                _add1000Button.onClick.AddListener(OnAdd1000);
            }

            if (_add10000Button != null)
            {
                _add10000Button.onClick.AddListener(OnAdd10000);
            }

            if (_levelUpButton != null)
            {
                _levelUpButton.onClick.AddListener(OnLevelUp);
            }

            if (_teleportLoc1Button != null)
            {
                _teleportLoc1Button.onClick.AddListener(OnTeleportLoc1);
            }

            if (_teleportLoc2Button != null)
            {
                _teleportLoc2Button.onClick.AddListener(OnTeleportLoc2);
            }

            if (_resetProgressButton != null)
            {
                _resetProgressButton.onClick.AddListener(OnResetProgress);
            }

            AdjustButtonLabels();

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
        }

        private void AdjustButtonLabels()
        {
            if (_toggleButton != null)
            {
                TMP_Text t = _toggleButton.GetComponentInChildren<TMP_Text>();
                if (t != null)
                {
                    // Avoid unicode gear icon that causes missing font glyph warning
                    t.text = "F1";
                    t.fontSize = 18f;
                }
            }

            ConfigureButtonLabel(_add1000Button, "+ $1,000");
            ConfigureButtonLabel(_add10000Button, "+ $10,000");
            ConfigureButtonLabel(_levelUpButton, "+ 1 Уровень (Level Up)");
            ConfigureButtonLabel(_teleportLoc1Button, "Телепорт: Локация 1");
            ConfigureButtonLabel(_teleportLoc2Button, "Телепорт: Локация 2");
            ConfigureButtonLabel(_resetProgressButton, "Сброс прогресса");
        }

        private static void ConfigureButtonLabel(Button button, string expectedText)
        {
            if (button == null) return;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label == null) return;

            if (!string.IsNullOrEmpty(expectedText))
            {
                label.text = expectedText;
            }

            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 18f;
            label.fontSize = 16f;
            label.margin = new Vector4(8f, 2f, 8f, 2f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void OnDestroy()
        {
            if (_toggleButton != null)
            {
                _toggleButton.onClick.RemoveListener(TogglePanel);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(ClosePanel);
            }

            if (_add1000Button != null)
            {
                _add1000Button.onClick.RemoveListener(OnAdd1000);
            }

            if (_add10000Button != null)
            {
                _add10000Button.onClick.RemoveListener(OnAdd10000);
            }

            if (_levelUpButton != null)
            {
                _levelUpButton.onClick.RemoveListener(OnLevelUp);
            }

            if (_teleportLoc1Button != null)
            {
                _teleportLoc1Button.onClick.RemoveListener(OnTeleportLoc1);
            }

            if (_teleportLoc2Button != null)
            {
                _teleportLoc2Button.onClick.RemoveListener(OnTeleportLoc2);
            }

            if (_resetProgressButton != null)
            {
                _resetProgressButton.onClick.RemoveListener(OnResetProgress);
            }

            _escapeRouter?.Remove(this);
        }

        private void Update()
        {
            bool f1Pressed = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                f1Pressed = true;
            }

            if (!f1Pressed)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.F1))
                    {
                        f1Pressed = true;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Legacy Input may throw if inactive in project settings
                }
            }

            if (f1Pressed)
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
            if (IsOpen)
            {
                ClosePanel();
                return true;
            }

            return false;
        }

        private void OnAdd1000()
        {
            _wallet?.Add(new Money(1000));
        }

        private void OnAdd10000()
        {
            _wallet?.Add(new Money(10000));
        }

        private void OnLevelUp()
        {
            if (_progression != null)
            {
                int xpNeeded = Mathf.Max(1, _progression.XpToNextLevel);
                _progression.AddExperience(xpNeeded);
            }
        }

        private void OnTeleportLoc1()
        {
            TeleportPlayer(new Vector3(0f, 0f, 0f));
        }

        private void OnTeleportLoc2()
        {
            TeleportPlayer(new Vector3(200f, 0f, 0f));
        }

        private void TeleportPlayer(Vector3 targetPos)
        {
            if (_player != null && _player.Agent != null)
            {
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 20f, NavMesh.AllAreas))
                {
                    _player.Agent.Warp(hit.position);
                }
                else
                {
                    _player.Agent.Warp(targetPos);
                }

                _player.Stop();

                if (_cameraRig != null)
                {
                    bool isLoc2 = targetPos.x > 100f;
                    Vector2 minBounds = isLoc2 ? new Vector2(175f, -25f) : new Vector2(-25f, -25f);
                    Vector2 maxBounds = isLoc2 ? new Vector2(225f, 25f) : new Vector2(25f, 25f);
                    _cameraRig.SnapTo(_player.Agent.transform.position, minBounds, maxBounds);
                }
            }
        }

        private void OnResetProgress()
        {
            // Suspend coordinator from saving old state on unload, then delete disk save
            _gameSaver?.ResetProgress();
            _saveService?.Delete();

            // Clear all player preferences (welcome popup flags, tutorial, etc.)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // Reset pause and unfreeze timescale
            _pauseService?.ResetAll();
            Time.timeScale = 1f;

            // Load fresh gameplay scene via project scene loader so composition root enters properly
            if (_sceneLoader != null)
            {
                _sceneLoader.Load(GameScene.Gameplay);
            }
            else
            {
                Scene activeScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(activeScene.buildIndex);
            }
        }
    }
}
