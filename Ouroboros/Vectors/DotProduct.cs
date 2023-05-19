using System;

namespace Ouroboros.Vectors;

public static class Calculate
{
    public static double DotProduct(double[] v1, double[] v2)
    {
        double val = 0;

        for (var i = 0; i <= v1.Length - 1; i++)
            val += v1[i] * v2[i];

        return val;
    }

    public static double EuclideanDistance(double[] v1, double[] v2)
    {
        if (v1.Length != v2.Length)
            throw new ArgumentException("Vectors must have the same length.");

        var sumOfSquaredDifferences = 0.0;

        for (var i = 0; i < v1.Length; i++)
        {
            var difference = v1[i] - v2[i];
            sumOfSquaredDifferences += difference * difference;
        }

        return Math.Sqrt(sumOfSquaredDifferences);
    }
}