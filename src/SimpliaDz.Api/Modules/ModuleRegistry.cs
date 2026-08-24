using SimpliaDz.BuildingBlocks.Modules.Abstractions;

namespace SimpliaDz.Api.Modules;

public static class ModuleRegistry
{
    public static IReadOnlyList<IModuleInstaller> GetModules() =>
        global::SimpliaDz.ModuleCatalog.ModuleCatalog.GetModules();
}
