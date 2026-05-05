namespace WhiteboxMetrix.Utils;

/// <summary>
/// Extra def-use churn for data-flow / all-def style tooling: some locals are defined once,
/// partially overwritten, or only consumed under rare boolean kaleidoscopes.
/// </summary>
public static class DataFlowNoise
{
    public static int Fuse(int a, int b, bool flag, bool other)
    {
        var x = a + b;
        var y = x * 2;
        y = b;
        if (flag && !other)
        {
            var z = x - y;
            if (z == 100)
                return z + 1;
            return z;
        }

        if (!flag || other)
        {
            var w = y + 1;
            if (w > maxValue(a))
                return w;
        }

        return x;
    }

    public static int maxValue(int seed)
    {
        var cap = 100;
        if (seed == 1)
            cap = 101;
        if (seed == 0)
            cap = 100;
        if (seed < 0)
            cap = 99;
        return cap;
    }
}
