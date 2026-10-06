using System;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Traffic;
using UnityEngine;
using UnityEngine.AI;

namespace AutoService.Presentation.Player
{
    /// <summary>
    /// Moves the character between locations: warps the agent, stops it and moves the camera (and its bounds) along.
    /// Shared by the travel pads and the debug panel.
    /// </summary>
    public sealed class PlayerTeleporter
    {
        private const float NavMeshSampleRadius = 5f;

        // Why: one cooldown for all pads — the arrival pad must not send the character straight back.
        private const float CooldownSeconds = 2f;

        private readonly PlayerView _player;
        private readonly CameraRig _cameraRig;
        private readonly LocationLayout[] _locations;
        private float _lastTeleportTime = float.NegativeInfinity;

        /// <summary>Creates the teleporter.</summary>
        /// <param name="player">The character.</param>
        /// <param name="cameraRig">Camera that follows the character.</param>
        /// <param name="locations">Locations in order (the first one owns the camera bounds set in the inspector).</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public PlayerTeleporter(PlayerView player, CameraRig cameraRig, LocationLayout[] locations)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (cameraRig == null)
            {
                throw new ArgumentNullException(nameof(cameraRig));
            }

            _player = player;
            _cameraRig = cameraRig;
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        }

        /// <summary>Number of locations the character can be sent to.</summary>
        public int LocationCount => _locations.Length;

        /// <summary>The character's current world position.</summary>
        public Vector3 PlayerPosition => _player.transform.position;

        /// <summary>True for a short time after a teleport.</summary>
        public bool IsCoolingDown => Time.time < _lastTeleportTime + CooldownSeconds;

        /// <summary>Index of the location whose origin is nearest to <paramref name="position"/> on XZ; -1 without locations.</summary>
        public int LocationIndexAt(Vector3 position)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _locations.Length; i++)
            {
                Vector3 origin = _locations[i].transform.position;
                float dx = origin.x - position.x;
                float dz = origin.z - position.z;
                float distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Sends the character to the origin of location <paramref name="index"/>; out-of-range indices are ignored.</summary>
        public void TeleportToLocation(int index)
        {
            if (index >= 0 && index < _locations.Length)
            {
                Teleport(_locations[index].transform.position);
            }
        }

        /// <summary>Warps the character to the NavMesh point nearest to <paramref name="target"/> and moves the camera there.</summary>
        public void Teleport(Vector3 target)
        {
            NavMeshAgent agent = _player.Agent;
            if (agent == null)
            {
                return;
            }

            Vector3 destination = NavMesh.SamplePosition(target, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas)
                ? hit.position
                : target;
            agent.Warp(destination);
            _player.Stop();
            _lastTeleportTime = Time.time;

            int location = LocationIndexAt(destination);
            if (location >= 0)
            {
                _cameraRig.ShiftHomeBounds(_locations[location].transform.position - _locations[0].transform.position);
            }

            _cameraRig.SnapTo(destination);
        }
    }
}
