using System.Collections.Generic;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Presentation.Points;
using AutoService.Domain.Common;
using UnityEngine;
using System;
using AutoService.Services.Formatting;
using AutoService.Presentation.Traffic;

namespace AutoService.Presentation.Ui.FloatingText
{
    public class FloatingTextSpawner : MonoBehaviour, IDisposable
    {
        [SerializeField] private FloatingText _prefab;
        [SerializeField] private LocationLayout _location;

        private IEventBus _eventBus;
        private Action<OrderAcceptedEvent> _onOrderAccepted;
        
        private readonly Queue<FloatingText> _pool = new Queue<FloatingText>();
        private readonly List<FloatingText> _active = new List<FloatingText>();

        public void Initialize(IEventBus eventBus, LocationLayout location)
        {
            _eventBus = eventBus;
            _location = location;
            _onOrderAccepted = OnOrderAccepted;
            _eventBus.Subscribe(_onOrderAccepted);
        }

        public void Dispose()
        {
            if (_eventBus != null && _onOrderAccepted != null)
            {
                _eventBus.Unsubscribe(_onOrderAccepted);
                _onOrderAccepted = null;
            }
        }

        private void OnOrderAccepted(OrderAcceptedEvent evt)
        {
            if (evt.Price.Amount <= 0) return;

            Vector3 position = GetPointPosition(evt.PointId) + Vector3.up * 2f; // Offset slightly above
            Spawn($"+{MoneyFormatter.Format(evt.Price)}", position);
        }

        private Vector3 GetPointPosition(string pointId)
        {
            if (_location != null)
            {
                foreach (var point in _location.ServicePoints)
                {
                    if (point.PointId == pointId)
                    {
                        return point.transform.position;
                    }
                }
            }
            return Vector3.zero;
        }

        private void Spawn(string text, Vector3 position)
        {
            if (_prefab == null)
            {
                _prefab = Resources.Load<FloatingText>("FloatingTextPrefab");
                if (_prefab == null)
                {
                    // Fallback to creating a new one if not found in resources
                    var go = new GameObject("FloatingTextFallback");
                    var tmp = go.AddComponent<TMPro.TextMeshPro>();
                    tmp.fontSize = 5;
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    _prefab = go.AddComponent<FloatingText>();
                    go.SetActive(false);
                }
            }

            FloatingText instance;
            if (_pool.Count > 0)
            {
                instance = _pool.Dequeue();
            }
            else
            {
                instance = Instantiate(_prefab, transform);
            }

            instance.gameObject.SetActive(true);
            _active.Add(instance);
            instance.Play(text, position, OnTextComplete);
        }

        private void OnTextComplete(FloatingText instance)
        {
            instance.gameObject.SetActive(false);
            _active.Remove(instance);
            _pool.Enqueue(instance);
        }
    }
}
