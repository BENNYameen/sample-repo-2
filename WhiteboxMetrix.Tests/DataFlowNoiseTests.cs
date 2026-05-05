using NUnit.Framework;
using WhiteboxMetrix.Utils;

namespace WhiteboxMetrix.Tests;

[TestFixture]
public sealed class DataFlowNoiseTests
{
    [Test]
    public void Fuse_runs_on_simple_inputs_without_branch_survey()
    {
        var v = DataFlowNoise.Fuse(1, 2, flag: true, other: false);
        Assert.That(v, Is.Not.EqualTo(int.MinValue));
    }
}
