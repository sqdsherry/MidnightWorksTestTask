using AutoService.Domain.Points;
using UnityEngine;

namespace AutoService.Presentation.Points
{
    public abstract class ServiceFx : MonoBehaviour
    {
        public abstract void Render(ServicePointState state, float progress, float dt);
    }

}
