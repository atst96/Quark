using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using World.NET.Utils;

namespace World.NET;

public static unsafe class MatlabFunctions
{
    /// <summary>
    /// fftshift() swaps the left and right halves of input vector.
    /// http://www.mathworks.com/help/matlab/ref/fftshift.html
    /// </summary>
    /// <param name="x">Input vector</param>
    /// <param name="x_length">Length of x</param>
    /// <param name="y">Swapped vector x</param>
    public static void fftshift(double[] x, int x_length, double[] y)
    {
        int x_data_length = x_length * sizeof(double);
        int x_data_half = x_data_length / 2;

        //for (int i = 0; i < x_length / 2; ++i)
        //{
        //    y[i] = x[i + l];
        //    y[i + l] = x[i];
        //}

        Buffer.BlockCopy(x, x_data_half, y, 0, x_data_half);
        Buffer.BlockCopy(x, 0, y, x_data_half, x_data_half);
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
    public unsafe static void histc(double* x, int x_length, double* edges,
        int edges_length, int* index)
    {
        int count = 1;

        double x0 = x[0];

        int i = 0;
        for (; i < edges_length; ++i)
        {
            index[i] = 1;
            //if (edges[i] >= x[0]) break;
            if (edges[i] >= x0) break;
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
        // for (i++; i < edges_length; ++i) index[i] = count;
        new Span<double>(index, edges_length)[i..].Fill(count);
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
    public unsafe static void interp1(double* x, double* y, int x_length, double* xi,
        int xi_length, double* yi)
    {
        //double[] h = new double[x_length - 1];
        //int[] k = new int[xi_length];

        double[] h = ArrayUtil.Rent<double>(x_length - 1);
        int[] k = ArrayUtil.Rent<int>(xi_length);

        // for (int i = 0; i < x_length - 1; ++i) h[i] = x[i + 1] - x[i];
        if (x_length > 31)
            VectorUtil.Diff(new Span<double>(x, x_length)[1..], new Span<double>(x, x_length), h);
        else
            for (int i = 0; i < x_length - 1; ++i) h[i] = x[i + 1] - x[i];
        //for (int i = 0; i < xi_length; ++i)
        //{
        //    k[i] = 0;
        //}
        k.AsSpan(0..xi_length).Clear();

        fixed (int* p_k = k)
        fixed (double* p_h = h)
        {
            histc(x, x_length, xi, xi_length, p_k);

            for (int i = 0; i < xi_length; ++i)
            {
                double s = (xi[i] - x[k[i] - 1]) / p_h[k[i] - 1];
                yi[i] = y[k[i] - 1] + s * (y[p_k[i]] - y[p_k[i] - 1]); ;
            }
        }

        //delete[] k;
        //delete[] h;
        ArrayUtil.Return(ref k);
        ArrayUtil.Return(ref h);
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
