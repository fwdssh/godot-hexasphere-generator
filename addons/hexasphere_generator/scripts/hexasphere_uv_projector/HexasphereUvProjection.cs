using Godot;

namespace Godot.Hexasphere
{
    public class HexasphereUvProjector
    {
        public const float PoleThreshold = 0.999f;

        public static Vector2 CalculateUv(Vector3 position)
        {
            float longitude = Mathf.Atan2(position.Z, position.X);
            float radius = position.Length();
            float latitude = Mathf.Asin(radius > 0 ? position.Y / radius : 0f);

            float u = (longitude + Mathf.Pi) / (2f * Mathf.Pi);
            float v = (latitude + (Mathf.Pi / 2f)) / Mathf.Pi;

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