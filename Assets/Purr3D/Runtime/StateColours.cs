using Purr3D.Core;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// One colour per state group, shared by the room's floor zones and the HUD's state chip so they match.
    /// </summary>
    public static class StateColours
    {
        public static Color For(StateGroup? group, float brightness)
        {
            float hue, saturation;
            switch (group)
            {
                case StateGroup.Backlog: hue = 0f; saturation = 0f; break;          // grey
                case StateGroup.Unstarted: hue = 0.6f; saturation = 0.45f; break;   // blue
                case StateGroup.Started: hue = 0.11f; saturation = 0.6f; break;     // amber
                case StateGroup.Completed: hue = 0.33f; saturation = 0.45f; break;  // green
                case StateGroup.Cancelled: hue = 0f; saturation = 0.45f; break;     // dull red
                default: hue = 0.8f; saturation = 0.5f; break;                       // purple: unknown state
            }
            return Color.HSVToRGB(hue, saturation, brightness);
        }
    }
}
