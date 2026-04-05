using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace DeltaBuild.Tests;

public class ModuleInit
{
    [ModuleInitializer]
    public static void Initialize()
    {
        MSBuildLocator.RegisterDefaults();
    }
}