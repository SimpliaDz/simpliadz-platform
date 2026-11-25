# Production deployment

Simplia uses a stateless API, a separate static web application, SQL Server, and native mobile clients. Build and version API and web images independently; run the migrator as a one-off release job before updating API replicas.

## Runtime configuration

Never put secrets in an image or source control. Supply SIMPLIADZ_TENANTS_FILE, Jwt__SigningKey, and ASPNETCORE_ENVIRONMENT=Production from a protected host file or secret store. The tenant catalog contains connection strings and enabled modules, so protect and back it up like a credential. Use /health/ready for load balancers.

## VM or on-premises Docker deployment

Build versioned images with:

    docker build --target api -t registry.example/simplia/api:1.0.0 .
    docker build --target migrator -t registry.example/simplia/migrator:1.0.0 .

Mount the tenant catalog read-only at /run/secrets/tenants.json and run the migrator once for each tenant before the API:

    docker run --rm --network simplia-network -v /srv/simplia/secrets/tenants.json:/run/secrets/tenants.json:ro -e SIMPLIADZ_TENANTS_FILE=/run/secrets/tenants.json registry.example/simplia/migrator:1.0.0 --tenant your-tenant-id

Run API replicas behind a TLS reverse proxy, keep SQL Server private, use persistent encrypted storage, and test backups. Deploy immutable version tags—not latest. Roll back application images only after checking the migration compatibility and restore plan.
