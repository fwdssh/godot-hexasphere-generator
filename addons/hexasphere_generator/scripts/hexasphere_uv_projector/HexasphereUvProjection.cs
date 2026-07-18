using Godot;

namespace Godot.Hexasphere
{
    public class HexasphereUvProjector
    {
        public const float PoleThreshold = 0.999f;

        public static Vector2 CalculateUv(Vector3 position)
        {
            float longitude = Mathf.Atan2(position.X, position.Z);
            float radius = position.Length();
            float latitude = Mathf.Asin(radius > 0 ? Mathf.Clamp(position.Y / radius, -1f, 1f) : 0f);

            float u = (longitude + Mathf.Pi) / (2f * Mathf.Pi);

            // Equirectangular projection: latitude maps linearly to v.
            // v = 0 at the north pole, v = 1 at the south pole (matches the
            // orientation the rest of the pipeline - UvToScreen, pole-cap
            // fan rendering, etc. - already expects).
            float v = (latitude + (Mathf.Pi / 2f)) / Mathf.Pi;
            v = 1.0f - v;

            return new Vector2(u, v);
        }

        public static int GetSeamOffset(float u, float referenceU)
        {
            if (u - referenceU > 0.5f) return -1;
            if (u - referenceU < -0.5f) return 1;
            return 0;
        }
    }
}