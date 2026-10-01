using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Panels;
using AutoService.Presentation.Player;
using AutoService.Presentation.Traffic;
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
            LocationLayout location1,
            CarVisualCatalog carVisuals,
            Transform carPoolRoot,
            BalanceView balanceView,
            OfferPanelView buildPanel)
        {
            InputActions = inputActions;
            Camera = camera;
            CameraRig = cameraRig;
            Player = player;
            ClickMarker = clickMarker;
            InteractableMask = interactableMask;
            GroundMask = groundMask;
            OccluderMask = occluderMask;
            Location1 = location1;
            CarVisuals = carVisuals;
            CarPoolRoot = carPoolRoot;
            BalanceView = balanceView;
            BuildPanel = buildPanel;
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

        /// <summary>Markup of location 1.</summary>
        public LocationLayout Location1 { get; }

        /// <summary>Car type id → car prefab.</summary>
        public CarVisualCatalog CarVisuals { get; }

        /// <summary>Parent of pooled car instances (may be null: scene root).</summary>
        public Transform CarPoolRoot { get; }

        /// <summary>Temporary balance label.</summary>
        public BalanceView BalanceView { get; }

        /// <summary>Screen-space build panel.</summary>
        public OfferPanelView BuildPanel { get; }
    }
}
