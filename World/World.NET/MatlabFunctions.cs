using System;
using World.NET.Utils;

namespace World.NET;

public static class MatlabFunctions
{
    /// <summary>
    /// fftshift() swaps the left and right halves of input vector.
    /// http://www.mathworks.com/help/matlab/ref/fftshift.html
    /// </summary>
    /// <param name="x">Input vector</param>
    /// <param name="x_length">Length of x</param>
    /// <param name="y">Swapped vector x</param>
    public static void fftshift(Span<double> x, int x_length, Span<double> y)
    {
        for (int i = 0; i < x_length / 2; ++i)
        {
            y[i] = x[i + x_length / 2];
            y[i + x_length / 2] = x[i];
        }
    }

    /// <summary>
    /// histc() counts the number of values in vector x that fall between the elements in the edges vector (which must contain monotonically nondecreasing values).
    /// n is a length(edges) vector containing these counts.
    /// No elements of x can be complex.
    /// http://www.mathworks.co.jp/help/techdoc/ref/histc.html
    /// </summary>
    /// <param name="x">Input vector</param>
    /// <param name="x_length">Length of x</param>
    /// <param name="edges">Input matrix (1-dimension)</param>
    /// <param name="edges_length">Length of edges</param>
    /// <param name="index">Result counted in vector x</param>
    public static void histc(Span<double> x, int x_length, Span<double> edges,
        int edges_length, Span<int> index)
    {
        int count = 1;

        int i = 0;
        for (; i < edges_length; ++i)
        {
            index[i] = 1;
            if (edges[i] >= x[0]) break;
        }
        for (; i < edges_length; ++i)
        {
            if (edges[i] < x[count])
            {
                index[i] = count;
            }
            else
            {
                index[i--] = count++;
            }
            if (count == x_length) break;
        }
        count--;
        for (i++; i < edges_length; ++i) index[i] = count;
    }

    /// <summary>
    /// interp1() interpolates to find yi, the values of the underlying function Y at the points in the vector or array xi. x must be a vector.
    /// http://www.mathworks.co.jp/help/techdoc/ref/interp1.html
    /// </summary>
    /// <param name="x">Input vector (Time axis)</param>
    /// <param name="y">Values at x[n]</param>
    /// <param name="x_length">Length of x (Length of y must be the same)</param>
    /// <param name="xi">Length of xi (Length of yi must be the same)</param>
    /// <param name="xi_length">Required vector</param>
    /// <param name="yi">Interpolated vector</param>
    public static void interp1(Span<double> x, Span<double> y, int x_length, Span<double> xi,
        int xi_length, Span<double> yi)
    {
        double[] h = new double[x_length - 1];
        int[] k = new int[xi_length];

        for (int i = 0; i < x_length - 1; ++i) h[i] = x[i + 1] - x[i];
        for (int i = 0; i < xi_length; ++i)
        {
            k[i] = 0;
        }

        histc(x, x_length, xi, xi_length, k);

        for (int i = 0; i < xi_length; ++i)
        {
            double s = (xi[i] - x[k[i] - 1]) / h[k[i] - 1];
            yi[i] = y[k[i] - 1] + s * (y[k[i]] - y[k[i] - 1]);
        }

        //ArrayUtil.Return(ref k);
        //ArrayUtil.Return(ref h);
    }

    // You must not use these variables.
    // Note:
    // I have no idea to implement the randn() and randn_reseed() without the
    // global variables. If you have a good idea, please give me the information.
    private static uint g_randn_x = 123456789;
    private static uint g_randn_y = 362436069;
    private static uint g_randn_z = 521288629;
    private static uint g_randn_w = 88675123;

    /// <summary>
    /// randn_reseed() forces to seed the pseudorandom generator using initial values.
    /// </summary>
    public static void randn_reseed()
    {
        g_randn_x = 123456789;
        g_randn_y = 362436069;
        g_randn_z = 521288629;
        g_randn_w = 88675123;
    }

    /// <summary>
    /// randn() generates pseudorandom numbers based on xorshift method.
    /// </summary>
    /// <returns>A generated pseudorandom number</returns>
    public static double randn()
    {
        uint t;
        t = g_randn_x ^ (g_randn_x << 11);
        g_randn_x = g_randn_y;
        g_randn_y = g_randn_z;
        g_randn_z = g_randn_w;
        g_randn_w = (g_randn_w ^ (g_randn_w >> 19)) ^ (t ^ (t >> 8));

        uint tmp = g_randn_w >> 4;
        for (int i = 0; i < 11; ++i)
        {
            t = g_randn_x ^ (g_randn_x << 11);
            g_randn_x = g_randn_y;
            g_randn_y = g_randn_z;
            g_randn_z = g_randn_w;
            g_randn_w = (g_randn_w ^ (g_randn_w >> 19)) ^ (t ^ (t >> 8));
            tmp += g_randn_w >> 4;
        }
        return tmp / 268435456.0 - 6.0;
    }
}
