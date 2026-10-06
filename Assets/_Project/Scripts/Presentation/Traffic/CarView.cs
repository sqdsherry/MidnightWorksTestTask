using System.Collections.Generic;
using AutoService.Domain.Traffic;
using AutoService.Presentation.Traffic.Routing;
using AutoService.Services.Traffic;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Traffic
{
    /// <summary>
    /// A pooled car visual driven by a <see cref="NavMeshAgent"/> along a route of <see cref="RoadNode"/>s: passes through
    /// intermediate nodes, stops on the last one, turns in place to its heading and reports the arrival once.
    /// </summary>
    /// <remarks>
    /// <para>Has no <c>Update</c>: <see cref="CarAgents"/> calls <see cref="TickArrival"/> from the game loop.</para>
    /// <para><b>Merge zones.</b> Before heading for a node whose <see cref="TrafficZone"/> it does not hold yet, the car asks
    /// the zone to let it in; if another car holds it, the car stops and asks again every tick. It releases the zone once it
    /// reaches a node outside of it, so the next car only starts when this one has cleared the merge.</para>
    /// <para>When the agent cannot path (no NavMesh, partial path) the car is snapped to the node with a warning, so a layout
    /// mistake shows up in the Console instead of freezing the whole car flow.</para>
    /// </remarks>
    public sealed class CarView : MonoBehaviour
    {
        private const float FacingToleranceDegrees = 1f;
        private const int InitialPathCapacity = 32;
        private const int NoCar = TrafficZone.NoCar;

        [SerializeField]
        [Tooltip("Agent that moves the car (Agent Type: Car).")]
        private NavMeshAgent _agent;

        [SerializeField, Min(1f)]
        [Tooltip("Turn speed when aligning with the target after arrival (degrees per second).")]
        private float _alignSpeed = 360f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra distance on top of the agent's Stopping Distance at which the final node counts as reached (m).")]
        private float _arrivalTolerance = 0.3f;

        // Why: preallocated and reused for every route; a route never has more nodes than the graph, so it rarely grows.
        private readonly List<RoadNode> _path = new List<RoadNode>(InitialPathCapacity);

        private int _pathIndex;
        private int _carId = NoCar;
        private RoadNode _currentNode;
        private TrafficZone _heldZone;
        private bool _waitingForZone;
        private bool _aligning;
        private bool _snapToNode;

        // Editor-only debugging: where the gizmo label reads the car's plan and state from (may stay null).
        private LocationTraffic _debugTraffic;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
        private static readonly Dictionary<Color, Texture2D> CachedColorTextures = new Dictionary<Color, Texture2D>();
        private static byte[] _baseColormapBytes;

        // Why: painting swaps in a per-car copy of the body material (a property block override was not shown);
        // the originals are kept here and restored when the car returns to the pool.
        private readonly List<Renderer> _paintedRenderers = new List<Renderer>(4);
        private readonly List<Material> _originalMaterials = new List<Material>(4);
        private Texture2D _paintTexture;
        private MaterialPropertyBlock _wheelPropertyBlock;
        private bool _hasCustomColor;
        private Color _customBodyColor;
        private bool _hasCustomWheels;
        private GameObject _customWheelPrefab;
        private readonly List<GameObject> _spawnedWheels = new List<GameObject>(4);
        private readonly List<GameObject> _hiddenOriginalWheels = new List<GameObject>(4);
        private GameObject _customVisual;
        private GameObject _defaultVisual;

        /// <summary>True while the car has a route it has not reported finishing yet.</summary>
        public bool IsDriving => _pathIndex < _path.Count;

        /// <summary>Last node the car has reached (the start node right after <see cref="Place"/>); routes start from it.</summary>
        public RoadNode CurrentNode => _currentNode;

        private bool AgentUsable => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

        private void Awake()
        {
            if (_agent != null)
            {
                _agent.baseOffset = 0f;
            }
        }

        /// <summary>Activates the car (if pooled) and teleports it onto <paramref name="startNode"/>, with no route.</summary>
        /// <param name="carId">Runtime id; the car enters merge zones under it.</param>
        /// <param name="startNode">Node the car appears on (the spawn); its forward is the initial heading.</param>
        public void Place(int carId, RoadNode startNode)
        {
            ReleaseZone();
            _carId = carId;
            ClearRoute();

            Transform start = startNode.transform;
            Vector3 position = new Vector3(start.position.x, 0f, start.position.z);
            Quaternion rotation = YawOnly(start.rotation, transform.rotation);

            // Why: positioned before activation so the agent snaps onto the NavMesh at the spawn point, not at the pool root.
            transform.SetPositionAndRotation(position, rotation);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (_agent != null && _agent.isActiveAndEnabled)
            {
                _agent.baseOffset = 0f;
                _agent.Warp(position);
                transform.rotation = rotation;
            }

            _currentNode = startNode;
        }

        /// <summary>
        /// Starts driving along <paramref name="path"/> (nodes after <see cref="CurrentNode"/>, the last one is the target),
        /// replacing any previous route. The nodes are copied, so the caller may reuse its buffer.
        /// </summary>
        public void FollowPath(IReadOnlyList<RoadNode> path)
        {
            ClearRoute();
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] != null)
                {
                    _path.Add(path[i]);
                }
            }

            if (_path.Count == 0)
            {
                StopAgent();
                return;
            }

            if (!AgentUsable)
            {
                Debug.LogWarning("[CarView] '" + name + "' is not on a NavMesh; it will be snapped along its route.", this);
            }
            else
            {
                _agent.updateRotation = true;
            }

            StartLeg();
        }

        /// <summary>Advances the route, merge-zone waiting, arrival detection and the final turn.</summary>
        /// <param name="deltaTime">Scaled frame time in seconds.</param>
        /// <returns>True exactly once per <see cref="FollowPath"/>: when the car is on the last node and aligned with it.</returns>
        public bool TickArrival(float deltaTime)
        {
            if (!IsDriving)
            {
                return false;
            }

            RoadNode node = _path[_pathIndex];
            if (_waitingForZone)
            {
                if (TryEnterZone(node))
                {
                    _waitingForZone = false;
                    DriveTo(node);
                }

                return false;
            }

            if (!_aligning)
            {
                if (_pathIndex < _path.Count - 1)
                {
                    if (TryPass(node))
                    {
                        ReachNode(node);
                        _pathIndex++;
                        StartLeg();
                    }

                    return false;
                }

                if (!TryReachFinal(node))
                {
                    return false;
                }

                ReachNode(node);
                _aligning = true;

                // Why: during the final turn the view owns the rotation; the agent would otherwise fight it.
                if (_agent != null)
                {
                    _agent.updateRotation = false;
                }
            }

            Quaternion goal = YawOnly(node.transform.rotation, transform.rotation);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, goal, _alignSpeed * deltaTime);
            if (Quaternion.Angle(rotation, goal) > FacingToleranceDegrees)
            {
                transform.rotation = rotation;
                return false;
            }

            transform.rotation = goal;
            ClearRoute();
            return true;
        }

        /// <summary>Teleports the car onto <paramref name="node"/> (position and heading) and drops the route; no arrival is reported.</summary>
        public void SnapTo(RoadNode node)
        {
            ClearRoute();
            StopAgent();
            SnapPosition(node.transform.position);
            transform.rotation = YawOnly(node.transform.rotation, transform.rotation);
            ReachNode(node);
        }

        /// <summary>Drops the current route and stops the agent; no arrival will be reported for it.</summary>
        public void Halt()
        {
            ClearRoute();
            StopAgent();

            // Why: a halted car will not move on by itself; holding a zone would block that merge for everybody forever.
            ReleaseZone();
        }

        /// <summary>
        /// Debug only: lets the Scene view label above the car show its plan and state (<c>#id plan state</c>).
        /// Pass null to show just the id.
        /// </summary>
        public void SetDebugTraffic(LocationTraffic traffic)
        {
            _debugTraffic = traffic;
        }

        /// <summary>Clears all runtime state (route, zone, customization) before the car goes back to the pool.</summary>
        public void ResetForPool()
        {
            Halt();

            // Why: before ResetVisualModel, which destroys the sport visual whose renderers hold painted material copies.
            ClearCustomColor();
            ResetVisualModel();
            ResetCustomWheels();
            _currentNode = null;
            _carId = NoCar;
        }

        /// <summary>Paints the car body with <paramref name="color"/>: the body renderers get a copy of their material with a recolored palette (glass, lights and wheels keep their colors).</summary>
        public void SetBodyColor(Color color)
        {
            _customBodyColor = color;
            _hasCustomColor = true;
            _paintTexture = GetOrCreateTintedPalette(color);
            ApplyPropertyBlock();
        }

        private static Texture2D GetOrCreateTintedPalette(Color targetColor)
        {
            if (CachedColorTextures.TryGetValue(targetColor, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            if (_baseColormapBytes == null)
            {
                TextAsset rawAsset = Resources.Load<TextAsset>("colormap_raw");
                if (rawAsset != null)
                {
                    _baseColormapBytes = rawAsset.bytes;
                }
            }

            if (_baseColormapBytes == null || _baseColormapBytes.Length != 512 * 512 * 4)
            {
                return null;
            }

            Texture2D newTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            newTex.filterMode = FilterMode.Bilinear;
            newTex.wrapMode = TextureWrapMode.Clamp;

            byte[] modifiedBytes = new byte[_baseColormapBytes.Length];
            System.Buffer.BlockCopy(_baseColormapBytes, 0, modifiedBytes, 0, _baseColormapBytes.Length);

            byte targetR = (byte)Mathf.Clamp(Mathf.RoundToInt(targetColor.r * 255f), 0, 255);
            byte targetG = (byte)Mathf.Clamp(Mathf.RoundToInt(targetColor.g * 255f), 0, 255);
            byte targetB = (byte)Mathf.Clamp(Mathf.RoundToInt(targetColor.b * 255f), 0, 255);

            // Why: only the body cells of the Kenney colormap are repainted — red/orange (sedan, sport) and green
            // (SUV). Glass, trim, lights and the orange racing rims fall outside both ranges and keep their color.
            for (int i = 0; i < modifiedBytes.Length; i += 4)
            {
                byte r = modifiedBytes[i];
                byte g = modifiedBytes[i + 1];
                byte b = modifiedBytes[i + 2];

                bool redBody = r > 180 && g < 155 && b < 100 && (r - g) > 35 && (r - b) > 55;
                bool greenBody = g > 140 && r < 110 && b < 150 && (g - r) > 50 && (g - b) > 30;
                if (redBody || greenBody)
                {
                    float factor = Mathf.Max(r, g) / 255f;
                    modifiedBytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(targetR * factor), 0, 255);
                    modifiedBytes[i + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt(targetG * factor), 0, 255);
                    modifiedBytes[i + 2] = (byte)Mathf.Clamp(Mathf.RoundToInt(targetB * factor), 0, 255);
                }
            }

            newTex.LoadRawTextureData(modifiedBytes);
            newTex.Apply(false, true);

            CachedColorTextures[targetColor] = newTex;
            return newTex;
        }

        /// <summary>Removes any custom MaterialPropertyBlock override.</summary>
        public void ClearCustomColor()
        {
            for (int i = 0; i < _paintedRenderers.Count; i++)
            {
                Renderer renderer = _paintedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material copy = renderer.sharedMaterial;
                renderer.sharedMaterial = _originalMaterials[i];
                if (copy != null && copy != _originalMaterials[i])
                {
                    Destroy(copy);
                }
            }

            _paintedRenderers.Clear();
            _originalMaterials.Clear();
            _hasCustomColor = false;
            _paintTexture = null;
        }

        /// <summary>Replaces the car's visual mesh with the sport model visual from <paramref name="sportPrefab"/>.</summary>
        public void SetSportModel(CarView sportPrefab)
        {
            if (sportPrefab == null)
            {
                return;
            }

            if (_defaultVisual == null)
            {
                Transform v = transform.Find("Visual");
                if (v != null)
                {
                    _defaultVisual = v.gameObject;
                }
            }

            if (_customVisual != null)
            {
                Destroy(_customVisual);
                _customVisual = null;
            }

            Transform sportVisual = sportPrefab.transform.Find("Visual");
            if (sportVisual != null)
            {
                if (_defaultVisual != null)
                {
                    _defaultVisual.SetActive(false);
                }

                _customVisual = Instantiate(sportVisual.gameObject, transform);
                _customVisual.name = "Visual_Sport";
                _customVisual.transform.localPosition = sportVisual.localPosition;
                _customVisual.transform.localRotation = sportVisual.localRotation;
                _customVisual.transform.localScale = sportVisual.localScale;
                _customVisual.SetActive(true);

                if (_hasCustomWheels)
                {
                    ApplyWheelVisuals();
                }

                if (_hasCustomColor)
                {
                    ApplyPropertyBlock();
                }
            }
        }

        /// <summary>Restores the default visual mesh if it was swapped.</summary>
        public void ResetVisualModel()
        {
            if (_customVisual != null)
            {
                Destroy(_customVisual);
                _customVisual = null;
            }

            if (_defaultVisual != null)
            {
                _defaultVisual.SetActive(true);
            }
        }

        /// <summary>Applies tires upgrade by swapping wheels with dark/racing wheel prefabs or applying dark sport rim material block.</summary>
        public void ApplyTiresUpgrade(GameObject customWheelPrefab = null)
        {
            _hasCustomWheels = true;
            _customWheelPrefab = customWheelPrefab;
            ApplyWheelVisuals();
        }

        public void ResetCustomWheels()
        {
            _hasCustomWheels = false;
            _customWheelPrefab = null;

            for (int i = 0; i < _spawnedWheels.Count; i++)
            {
                if (_spawnedWheels[i] != null)
                {
                    Destroy(_spawnedWheels[i]);
                }
            }
            _spawnedWheels.Clear();

            for (int i = 0; i < _hiddenOriginalWheels.Count; i++)
            {
                if (_hiddenOriginalWheels[i] != null)
                {
                    _hiddenOriginalWheels[i].SetActive(true);
                }
            }
            _hiddenOriginalWheels.Clear();

            if (_wheelPropertyBlock != null)
            {
                Transform activeVisual = _customVisual != null && _customVisual.activeSelf
                    ? _customVisual.transform
                    : (_defaultVisual != null ? _defaultVisual.transform : transform.Find("Visual"));

                if (activeVisual != null)
                {
                    MeshRenderer[] renderers = activeVisual.GetComponentsInChildren<MeshRenderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i].gameObject.name.ToLowerInvariant().Contains("wheel"))
                        {
                            renderers[i].SetPropertyBlock(null);
                        }
                    }
                }
            }
        }

        private void ApplyWheelVisuals()
        {
            Transform activeVisual = _customVisual != null && _customVisual.activeSelf
                ? _customVisual.transform
                : (_defaultVisual != null ? _defaultVisual.transform : transform.Find("Visual"));

            if (activeVisual == null) return;

            // Clear previously spawned wheels
            for (int i = 0; i < _spawnedWheels.Count; i++)
            {
                if (_spawnedWheels[i] != null)
                {
                    Destroy(_spawnedWheels[i]);
                }
            }
            _spawnedWheels.Clear();
            _hiddenOriginalWheels.Clear();

            List<Transform> wheelNodes = new List<Transform>();
            FindWheelTransforms(activeVisual, wheelNodes);

            if (_customWheelPrefab != null && wheelNodes.Count > 0)
            {
                foreach (Transform wheelNode in wheelNodes)
                {
                    wheelNode.gameObject.SetActive(false);
                    _hiddenOriginalWheels.Add(wheelNode.gameObject);

                    GameObject newWheel = Instantiate(_customWheelPrefab, wheelNode.parent);
                    newWheel.name = "CustomWheel_" + wheelNode.name;
                    newWheel.transform.localPosition = wheelNode.localPosition;
                    newWheel.transform.localRotation = wheelNode.localRotation;
                    newWheel.transform.localScale = wheelNode.localScale;
                    newWheel.SetActive(true);
                    _spawnedWheels.Add(newWheel);
                }
            }
            else
            {
                // Fallback / alternate styling: dark metallic rims via MaterialPropertyBlock
                if (_wheelPropertyBlock == null)
                {
                    _wheelPropertyBlock = new MaterialPropertyBlock();
                }
                Color darkRimColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                _wheelPropertyBlock.SetColor(BaseColorPropertyId, darkRimColor);
                _wheelPropertyBlock.SetColor(ColorPropertyId, darkRimColor);

                foreach (Transform wheelNode in wheelNodes)
                {
                    MeshRenderer mr = wheelNode.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.SetPropertyBlock(_wheelPropertyBlock);
                    }
                }
            }
        }

        private static void FindWheelTransforms(Transform parent, List<Transform> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.ToLowerInvariant().Contains("wheel"))
                {
                    results.Add(child);
                }
                else
                {
                    FindWheelTransforms(child, results);
                }
            }
        }

        private void ApplyPropertyBlock()
        {
            if (!_hasCustomColor)
            {
                return;
            }

            Transform activeVisual = _customVisual != null && _customVisual.activeSelf
                ? _customVisual.transform
                : (_defaultVisual != null ? _defaultVisual.transform : transform.Find("Visual"));

            if (activeVisual == null) return;

            MeshRenderer[] renderers = activeVisual.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                // Why: only the body is painted — wheels keep their own (possibly upgraded) look.
                if (renderers[i].gameObject.name.ToLowerInvariant().Contains("wheel"))
                {
                    continue;
                }

                Material copy;
                int index = _paintedRenderers.IndexOf(renderers[i]);
                if (index >= 0)
                {
                    copy = renderers[i].sharedMaterial;
                }
                else
                {
                    Material original = renderers[i].sharedMaterial;
                    if (original == null)
                    {
                        continue;
                    }

                    copy = new Material(original);
                    _paintedRenderers.Add(renderers[i]);
                    _originalMaterials.Add(original);
                    renderers[i].sharedMaterial = copy;
                }

                if (_paintTexture != null)
                {
                    if (copy.HasProperty(BaseMapPropertyId))
                    {
                        copy.SetTexture(BaseMapPropertyId, _paintTexture);
                    }

                    if (copy.HasProperty(MainTexPropertyId))
                    {
                        copy.SetTexture(MainTexPropertyId, _paintTexture);
                    }
                }
                else if (copy.HasProperty(BaseColorPropertyId))
                {
                    copy.SetColor(BaseColorPropertyId, _customBodyColor);
                }
            }
        }

        private void StartLeg()
        {
            RoadNode node = _path[_pathIndex];
            if (!TryEnterZone(node))
            {
                _waitingForZone = true;
                PauseAgent();
                return;
            }

            DriveTo(node);
        }

        private void DriveTo(RoadNode node)
        {
            _snapToNode = false;
            if (!AgentUsable)
            {
                _snapToNode = true;
                return;
            }

            _agent.isStopped = false;

            // Why: after a failed SetDestination the agent still reports the previous path's status and distance,
            // which could fake an instant arrival; snapping is explicit instead.
            if (!_agent.SetDestination(node.transform.position))
            {
                Debug.LogWarning("[CarView] SetDestination failed for '" + name + "'; it will be snapped to '" + node.name + "'.", this);
                _snapToNode = true;
            }
        }

        /// <returns>True once a passing car is close enough to switch to the next node.</returns>
        private bool TryPass(RoadNode node)
        {
            Vector3 target = node.transform.position;
            if (_snapToNode || !AgentUsable || IsPathBroken(node))
            {
                SnapPosition(target);
                return true;
            }

            Vector3 offset = target - transform.position;
            offset.y = 0f;
            float radius = node.PassRadius;
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <returns>True once the car stands on the last node (the alignment phase begins).</returns>
        private bool TryReachFinal(RoadNode node)
        {
            if (_snapToNode || !AgentUsable || IsPathBroken(node))
            {
                SnapPosition(node.transform.position);
                return true;
            }

            if (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + _arrivalTolerance)
            {
                return false;
            }

            StopAgent();
            return true;
        }

        // Why: a partial path would stop the car short of the node forever and stall every car behind it.
        private bool IsPathBroken(RoadNode node)
        {
            if (_agent.pathPending || _agent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            Debug.LogWarning("[CarView] No complete path for '" + name + "' to '" + node.name + "' (" + _agent.pathStatus + "); snapping it.", this);
            return true;
        }

        private void ReachNode(RoadNode node)
        {
            _currentNode = node;
            if (_heldZone != null && node.Zone != _heldZone)
            {
                ReleaseZone();
            }
        }

        /// <returns>True when the node is outside any zone or its zone now belongs to this car.</returns>
        private bool TryEnterZone(RoadNode node)
        {
            TrafficZone zone = node.Zone;
            if (zone == null || zone == _heldZone)
            {
                return true;
            }

            if (!zone.TryEnter(_carId))
            {
                return false;
            }

            // Why: one zone at a time — layouts keep a plain node between two zones, so this only triggers on a layout
            // where zones touch, and there the old one is already behind the car.
            ReleaseZone();
            _heldZone = zone;
            return true;
        }

        private void ReleaseZone()
        {
            if (_heldZone == null)
            {
                return;
            }

            _heldZone.Exit(_carId);
            _heldZone = null;
        }

        private void ClearRoute()
        {
            _path.Clear();
            _pathIndex = 0;
            _waitingForZone = false;
            _aligning = false;
            _snapToNode = false;
        }

        private void SnapPosition(Vector3 position)
        {
            position.y = 0f;
            if (AgentUsable)
            {
                _agent.baseOffset = 0f;
                _agent.Warp(position);
                return;
            }

            transform.position = position;
        }

        private void PauseAgent()
        {
            if (AgentUsable)
            {
                // Why: the car is already near the node before the zone; braking smoothly would roll it into the merge.
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }

        private void StopAgent()
        {
            if (AgentUsable)
            {
                _agent.ResetPath();
            }
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (_carId == NoCar)
            {
                return;
            }

            // Why: editor-only gizmo, so building the string every repaint is acceptable (never runs in a build or a tick).
            string label = "#" + _carId;
            if (_debugTraffic != null && _debugTraffic.TryGetCar(_carId, out Car car))
            {
                label += " " + car.Plan + " " + car.State;
            }

            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, label);
#endif
        }

        // Why: nodes may be tilted in the scene; a car only ever rotates around the vertical axis.
        private static Quaternion YawOnly(Quaternion rotation, Quaternion fallback)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : fallback;
        }
    }
}
