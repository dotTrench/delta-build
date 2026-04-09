using TUnit.Core.Interfaces;

namespace DeltaBuild.Tests;

public class DeltaBuildParallelLimit : IParallelLimit
{
    public int Limit => 4;
}
