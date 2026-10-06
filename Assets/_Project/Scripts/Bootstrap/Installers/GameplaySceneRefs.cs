using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Panels;
using AutoService.Presentation.Pause;
using AutoService.Presentation.Player;
using AutoService.Presentation.Points.Panel;
using AutoService.Presentation.Popups;
using AutoService.Presentation.Settings;
using AutoService.Presentation.Staff;
using AutoService.Presentation.Supplies;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Ui;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Immutable snapshot of the scene references assigned on <see cref="GameplayEntryPoint"/>, handed to the installers.
    /// </summary>
    /// <remarks>
    /// Why a plain class built in <c>Enter</c> instead of a serialized nested class: the fields stay on the entry point
    /// where the scene already references them, so splitting the composition root into installers needs no re-assignment
    /// in the inspector. Any reference may be null; each installer reports what it is missing.
    /// </remarks>
    internal sealed class GameplaySceneRefs
    {
        /// <summary>Creates the snapshot.</summary>
        public GameplaySceneRefs(
            InputActionAsset inputActions,
            Camera camera,
            CameraRig cameraRig,
            PlayerView player,
            ClickMarkerView clickMarker,
            LayerMask interactableMask,
            LayerMask groundMask,
            LayerMask occluderMask,
            LocationLayout[] locations,
            CarVisualCatalog carVisuals,
            Transform carPoolRoot,
            BalanceView balanceView,
            ProgressionView progressionView,
            OfferPanelView buildPanel,
            PointPanelView pointPanel,
            OfferPanelView storekeeperPanel,
            StaffView staffPrefab,
            StaffVisualCatalog staffVisuals,
            SupplyVisualCatalog supplyVisuals,
            Transform staffRoot,
            PlayerCarryView playerCarry,
            PauseMenuView pauseMenu,
            PauseButtonView pauseButton,
            SettingsView settingsPanel,
            LevelUpPopupView levelUpPopup = null,
            Location2WelcomePopupView loc2WelcomePopup = null,
            DebugCheatView debugCheatView = null)
        {
            InputActions = inputActions;
            Camera = camera;
            CameraRig = cameraRig;
            Player = player;
            ClickMarker = clickMarker;
            InteractableMask = interactableMask;
            GroundMask = groundMask;
            OccluderMask = occluderMask;
            Locations = locations ?? System.Array.Empty<LocationLayout>();
            CarVisuals = carVisuals;
            CarPoolRoot = carPoolRoot;
            BalanceView = balanceView;
            ProgressionView = progressionView;
            BuildPanel = buildPanel;
            PointPanel = pointPanel;
            StorekeeperPanel = storekeeperPanel;
            StaffPrefab = staffPrefab;
            StaffVisuals = staffVisuals;
            SupplyVisuals = supplyVisuals;
            StaffRoot = staffRoot;
            PlayerCarry = playerCarry;
            PauseMenu = pauseMenu;
            PauseButton = pauseButton;
            SettingsPanel = settingsPanel;
            LevelUpPopup = levelUpPopup;
            Loc2WelcomePopup = loc2WelcomePopup;
            DebugCheatView = debugCheatView;
        }

        /// <summary>GameControls asset with the 'Gameplay' action map.</summary>
        public InputActionAsset InputActions { get; }

        /// <summary>Gameplay camera (pointer raycasts, panels next to objects).</summary>
        public Camera Camera { get; }

        /// <summary>Camera rig following the character.</summary>
        public CameraRig CameraRig { get; }

        /// <summary>The player character.</summary>
        public PlayerView Player { get; }

        /// <summary>Optional ground click feedback.</summary>
        public ClickMarkerView ClickMarker { get; }

        /// <summary>Layers of clickable interactable objects.</summary>
        public LayerMask InteractableMask { get; }

        /// <summary>Layers of walkable ground.</summary>
        public LayerMask GroundMask { get; }

        /// <summary>Layers that block pointer raycasts without being clickable.</summary>
        public LayerMask OccluderMask { get; }

        /// <summary>Markup of all locations on the scene.</summary>
        public LocationLayout[] Locations { get; }

        /// <summary>Car type id → car prefab.</summary>
        public CarVisualCatalog CarVisuals { get; }

        /// <summary>Parent of pooled car instances (may be null: scene root).</summary>
        public Transform CarPoolRoot { get; }

        /// <summary>Temporary balance label.</summary>
        public BalanceView BalanceView { get; }

        /// <summary>Player progression (level and XP) view in HUD.</summary>
        public ProgressionView ProgressionView { get; }

        /// <summary>Screen-space build panel.</summary>
        public OfferPanelView BuildPanel { get; }

        /// <summary>Screen-space management panel of a point.</summary>
        public PointPanelView PointPanel { get; }

        /// <summary>Screen-space storekeeper offer (second offer panel).</summary>
        public OfferPanelView StorekeeperPanel { get; }

        /// <summary>Body prefab of hired NPCs.</summary>
        public StaffView StaffPrefab { get; }

        /// <summary>Staff role → body material.</summary>
        public StaffVisualCatalog StaffVisuals { get; }

        /// <summary>Consumable → box color.</summary>
        public SupplyVisualCatalog SupplyVisuals { get; }

        /// <summary>Parent of NPC instances (may be null: scene root).</summary>
        public Transform StaffRoot { get; }

        /// <summary>The box in the player's hands.</summary>
        public PlayerCarryView PlayerCarry { get; }

        /// <summary>The pause menu.</summary>
        public PauseMenuView PauseMenu { get; }

        /// <summary>The HUD's pause button.</summary>
        public PauseButtonView PauseButton { get; }

        /// <summary>Settings screen opened from the pause menu.</summary>
        public SettingsView SettingsPanel { get; }

        /// <summary>Modal level-up celebratory popup.</summary>
        public LevelUpPopupView LevelUpPopup { get; }

        /// <summary>Modal welcome window for Location 2.</summary>
        public Location2WelcomePopupView Loc2WelcomePopup { get; }

        /// <summary>F1 / toggle debug cheat window.</summary>
        public DebugCheatView DebugCheatView { get; }
    }
}
