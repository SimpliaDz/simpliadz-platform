/*
Purpose

one central place that returns all module installers
avoids scattering new InventoryModuleInstaller() around
*/
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Identity.Infrastructure;
using SimpliaDz.Inventory.Infrastructure;

namespace SimpliaDz.Api.Modules;

public static class ModuleRegistry
{
    public static IReadOnlyList<IModuleInstaller> GetModules()
    {
        return
        [
            new IdentityModuleInstaller(),
            new InventoryModuleInstaller()
        ];
    }
}