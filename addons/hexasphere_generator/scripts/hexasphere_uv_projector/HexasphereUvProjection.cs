using Godot;

namespace Godot.Hexasphere
{
    /// <summary>
    /// Provides equirectangular UV coordinate calculations for hexasphere tiles,
    /// used by the UV projector to map 3D tile positions onto a 2D texture.
    /// </summary>
    public class HexasphereUvProjector
    {
        /// <summary>Threshold for detecting pole-cap tiles based on Y-axis proximity.</summary>
        public const float PoleThreshold = 0.999f;

        /// <summary>
        /// Calculates the equirectangular UV coordinate for a given 3D position on the sphere.
        /// </summary>
        /// <param name="position">The 3D position on the sphere surface.</param>
        /// <returns>UV coordinate in [0,1] range where U=longitude, V=latitude (0=north, 1=south).</returns>
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

        /// <summary>
        /// Computes the integer seam offset needed to unwrap UV coordinates across the longitude
        /// seam, so vertices of the same tile are in a consistent UV domain.
        /// </summary>
        /// <param name="u">The U coordinate to check.</param>
        /// <param name="referenceU">The reference U coordinate (e.g. tile center).</param>
        /// <returns>-1, 0, or 1 indicating the offset to apply.</returns>
        public static int GetSeamOffset(float u, float referenceU)
        {
            if (u - referenceU > 0.5f) return -1;
            if (u - referenceU < -0.5f) return 1;
            return 0;
        }
    }
}