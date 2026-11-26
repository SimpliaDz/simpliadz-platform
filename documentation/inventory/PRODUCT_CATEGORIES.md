# Product Categories

## Purpose

Product categories provide a tenant-specific hierarchy for organizing the product catalog. A category may be a root or may reference one parent:

```text
Men
├── Pants
└── Shirts
Women
└── Dresses
```

The implementation extends the existing Inventory `ProductCategory` entity, so existing category IDs and product references remain valid.

## Domain model

| Property | Purpose |
|---|---|
| `Id` | Stable category identity. |
| `Name` | Required display name, maximum 150 characters. |
| `ParentCategoryId` | Optional self-reference. `null` identifies a root category. |
| `IsActive` | Whether the category is active for new catalog assignments. |
| `UpdatedAt` | Timestamp of the most recent change. |

`ParentCategory` and `Children` are navigation properties used by persistence. API hierarchy responses expose children recursively.

## Business rules

- Name is required.
- Category names are unique among siblings. The same name may be used below different parents, such as `Men → Shirts` and `Women → Shirts`.
- A selected parent must exist in the current tenant database.
- A category cannot be its own parent.
- A category cannot be moved under any descendant. Before an update, the repository walks the proposed parent's ancestry and rejects direct and indirect cycles.
- Deactivation changes only `IsActive`; it does not deactivate descendants and does not modify or delete products.
- Existing products may retain a deactivated category. Product updates that keep the same category remain valid, while a new assignment must reference an active category.
- The self-referencing foreign key uses restrictive deletion. Categories are not hard-deleted by this workflow.
- Tenant isolation follows the database-per-tenant design through `ITenantContext` and `InventoryDbContext`.

## HTTP API

All endpoints require the Inventory module for the current tenant and the listed permission.

| Method and route | Permission | Behavior |
|---|---|---|
| `GET /categories` | `categories.view` | Returns all categories as recursively nested root nodes. |
| `GET /categories/{id}` | `categories.view` | Returns the selected category and its descendant subtree. |
| `POST /categories` | `categories.create` | Creates a root or child category. |
| `PUT /categories/{id}` | `categories.edit` | Renames and/or reparents a category after cycle validation. |
| `PUT /categories/{id}/deactivate` | `categories.edit` | Deactivates the category without deleting relationships. |

Create and update body:

```json
{
  "name": "Shirts",
  "parentCategoryId": "00000000-0000-0000-0000-000000000000"
}
```

Use `null` for `parentCategoryId` to create or move a category to the root.

Hierarchy response shape:

```json
[
  {
    "id": "...",
    "name": "Men",
    "parentCategoryId": null,
    "isActive": true,
    "updatedAt": "...",
    "children": [
      {
        "id": "...",
        "name": "Shirts",
        "parentCategoryId": "...",
        "isActive": true,
        "updatedAt": "...",
        "children": []
      }
    ]
  }
]
```

## Errors

- `400 ValidationError` when the name is empty or exceeds 150 characters.
- `404 NotFound` when the category or selected parent does not exist.
- `409 Conflict` for a duplicate sibling name or a circular relationship.
- `401/403` when authentication or the required permission is missing.

## Persistence migration

`Inventory_ProductCategories` adds nullable `ParentCategoryId`, creates a restrictive self-referencing foreign key, and replaces global name uniqueness with a unique `(ParentCategoryId, Name)` index. Existing categories migrate as roots because the new parent column is nullable.

## Tests

`tests/SimpliaWear.Inventory.Tests/CategoryTests.cs` covers validation, root/child creation, sibling uniqueness, reparenting, indirect-cycle prevention, hierarchical listing, permissions, and history-preserving deactivation. Product tests also verify that an update can retain a category that became inactive after assignment.
