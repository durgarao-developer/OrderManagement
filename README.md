# Order Management System

A learning project demonstrating cloud-ready backend patterns with ASP.NET Core and Azure.
Customers place orders through an API; orders are validated, persisted, and (in later phases)
processed asynchronously with notifications.

## Current status — Week 1: Core API (local)

- ASP.NET Core Web API (.NET 10) with EF Core + SQL Server
- Entities: `Customer`, `Product`, `Order`, `OrderItem`
- CRUD endpoints for Customers and Products
- `POST /api/orders` validates stock, decrements it, and saves the order in a transaction
- Swagger/OpenAPI UI

## Running locally

Requires the .NET SDK and SQL Server LocalDB (ships with Visual Studio; or edit the
connection string in `appsettings.json` to point at any SQL Server / Docker instance).

```bash
cd OrderManagement.Api
dotnet run
```

Then open the Swagger UI at the HTTPS URL shown in the console (e.g. `https://localhost:xxxx/swagger`).

The database and schema are created automatically on first run (migrations are applied at startup).

### EF Core migrations

```bash
cd OrderManagement.Api
dotnet ef migrations add <Name>
dotnet ef database update
```

## API endpoints (v1)

| Method | Route | Purpose |
|---|---|---|
| GET/POST | `/api/customers` | List / create customers |
| GET/PUT/DELETE | `/api/customers/{id}` | Read / update / delete a customer |
| GET/POST | `/api/products` | List / create products |
| GET/PUT/DELETE | `/api/products/{id}` | Read / update / delete a product |
| GET | `/api/orders` | List orders |
| GET | `/api/orders/{id}` | Get one order |
| POST | `/api/orders` | Create an order (validates stock) |

### Create order request body

```json
{
  "customerId": 1,
  "items": [
    { "productId": 1, "quantity": 2 },
    { "productId": 3, "quantity": 1 }
  ]
}
```

## Roadmap

- **Week 2** — Deploy to Azure App Service + Azure SQL
- **Week 3** — Service Bus queue for async order processing
- **Week 4** — Azure Functions: invoice PDF (Blob Storage) + email notifications
- **Week 5** — Key Vault + Managed Identity for secrets
- **Week 6** — Application Insights, structured logging, health checks
- **Week 7** — Unit/integration tests + CI pipeline
- **Week 8** — Architecture diagram, docs polish

## What I built, what broke, how I fixed it

_(Keep a running log here as the project grows — great interview material.)_
