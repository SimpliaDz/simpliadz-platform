param(
    [string]$TenantId = 'postman',
    [int]$Port = 5288,
    [switch]$NoBuild,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$saved = @{}
$environment = @{
    'ASPNETCORE_ENVIRONMENT' = 'Development'
    'ASPNETCORE_URLS' = "http://localhost:$Port"
    'Logging__LogLevel__Default' = 'Warning'
    'Tenancy__ActiveTenantId' = $TenantId
    'SIMPLIAWEAR_TENANTS_FILE' = ''
    "Tenancy__Tenants__${TenantId}__Name" = 'Postman functional test tenant'
}

Push-Location $repositoryRoot
try {
    docker compose up -d
    if ($LASTEXITCODE -ne 0) { throw 'Could not start SQL Server.' }

    $compose = docker compose config --format json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Docker Compose configuration.' }
    $password = $compose.services.sqlserver.environment.MSSQL_SA_PASSWORD
    if ([string]::IsNullOrWhiteSpace($password)) { throw 'SQL Server password is missing.' }
    $database = 'SimpliaWear_Postman_' + ($TenantId -replace '[^A-Za-z0-9_]', '_')
    $environment["Tenancy__Tenants__${TenantId}__ConnectionString"] =
        "Server=localhost,1433;Database=$database;User Id=sa;Password=$password;TrustServerCertificate=True"
    foreach ($module in @('Identity', 'Inventory', 'Organization', 'Sales', 'Purchasing',
        'Accounting', 'Notifications', 'Reporting', 'Audit', 'Crm')) {
        $environment["Tenancy__Tenants__${TenantId}__Modules__$module"] = 'true'
    }

    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec -e "SQLCMDPASSWORD=$password" simplia-wear-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q 'SELECT 1' -b *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'SQL Server did not become ready within 30 seconds.' }

    foreach ($name in $environment.Keys) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
    }

    Write-Host "Migrating dedicated $TenantId tenant..."
    $migratorArgs = @('run', '--project', 'src/SimpliaWear.DatabaseMigrator', '-c', $Configuration)
    if ($NoBuild) { $migratorArgs += '--no-build' }
    $migratorArgs += @('--', '--tenant', $TenantId)
    & dotnet @migratorArgs
    if ($LASTEXITCODE -ne 0) { throw 'Tenant migration failed.' }

    Write-Host "Starting the Postman API at http://localhost:$Port. Press Ctrl+C to stop."
    $apiArgs = @('run', '--project', 'src/SimpliaWear.Api', '-c', $Configuration, '--no-launch-profile')
    if ($NoBuild) { $apiArgs += '--no-build' }
    & dotnet @apiArgs
    if ($LASTEXITCODE -ne 0) { throw 'API exited with an error.' }
}
finally {
    foreach ($name in $environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
    }
    Pop-Location
}
