namespace Simplia.BuildingBlocks.Modules.Abstractions;

/// <summary>
/// Associates an HTTP endpoint with the module that owns it.
/// </summary>
public sealed record ModuleEndpointMetadata(string ModuleName);
