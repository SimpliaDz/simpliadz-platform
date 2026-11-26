using Simplia.BuildingBlocks.Modules.Abstractions;

namespace Simplia.Api.Modules;

public static class ModuleRegistry
{
    public static IReadOnlyList<IModuleInstaller> GetModules() =>
        global::Simplia.ModuleCatalog.ModuleCatalog.GetModules();
}
