param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$previousIntegrationConnection = [Environment]::GetEnvironmentVariable('SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING')
Push-Location $repositoryRoot

try {
    docker compose up -d
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not start the SQL Server container.'
    }

    $compose = docker compose config --format json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read Docker Compose configuration.'
    }
    $password = $compose.services.sqlserver.environment.MSSQL_SA_PASSWORD
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw 'SQL Server password is missing from Docker Compose configuration.'
    }

    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec -e "SQLCMDPASSWORD=$password" simplia-wear-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q 'SELECT 1' -b *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        throw 'SQL Server did not become ready within 30 seconds.'
    }

    $env:SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING = "Server=localhost,1433;Database=master;User Id=sa;Password=$password;TrustServerCertificate=True"

    $testArguments = @('test', 'src/SimpliaWear.sln', '-m:1', '--logger', 'console;verbosity=minimal')
    if ($NoBuild) {
        $testArguments += @('--no-build', '--no-restore')
    }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Functional test suite failed.'
    }
}
finally {
    if ($null -eq $previousIntegrationConnection) {
        Remove-Item Env:SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING -ErrorAction SilentlyContinue
    }
    else {
        $env:SIMPLIAWEAR_INTEGRATION_CONNECTIONSTRING = $previousIntegrationConnection
    }
    Pop-Location
}
