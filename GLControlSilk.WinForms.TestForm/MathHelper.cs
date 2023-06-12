using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GLControlSilk.WinForms.TestForm
{
    public static class MathHelper
    {
        //
        // Summary:
        //     Defines the value of Pi divided by four as a System.Single.
        public const float PiOver4 = MathF.PI / 4f;

        //
        // Summary:
        //     Convert degrees to radians.
        //
        // Parameters:
        //   degrees:
        //     An angle in degrees.
        //
        // Returns:
        //     The angle expressed in radians.
        public static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180f);
        }

        //
        // Summary:
        //     Convert radians to degrees.
        //
        // Parameters:
        //   radians:
        //     An angle in radians.
        //
        // Returns:
        //     The angle expressed in degrees.
        public static float RadiansToDegrees(float radians)
        {
            return radians * (180f / MathF.PI);
        }

        //
        // Summary:
        //     Convert degrees to radians.
        //
        // Parameters:
        //   degrees:
        //     An angle in degrees.
        //
        // Returns:
        //     The angle expressed in radians.
        public static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        //
        // Summary:
        //     Convert radians to degrees.
        //
        // Parameters:
        //   radians:
        //     An angle in radians.
        //
        // Returns:
        //     The angle expressed in degrees.
        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
