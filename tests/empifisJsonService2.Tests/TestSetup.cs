using System.Runtime.CompilerServices;

namespace empifisJsonService2.Tests;

internal static class TestSetup
{
    // The service's nlog.config is copied next to the tests and writes to C:\Altera\Log; replace it with
    // an empty configuration so test runs don't end up in the real service log.
    [ModuleInitializer]
    internal static void DisableServiceLogging() => NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();
}
