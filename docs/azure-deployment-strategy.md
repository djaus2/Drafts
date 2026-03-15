# Azure Deployment Strategy

## Overview
Clean deployment approach that ensures database schema alignment by recreating the database on fresh installations.

## Strategy: Fresh Database on Deploy

### ✅ **How It Works**
- **Database recreation** happens automatically on fresh deployments
- **No migration complexity** - clean slate every time
- **Perfect schema alignment** - database always matches current model
- **Simple and reliable** - no migration conflicts or schema drift

### 🚀 **Deployment Process**

#### **For Azure App Service:**
1. **Deploy new version** of the application
2. **Delete existing database file** (if exists)
3. **Application starts** → `DbSeeder` detects missing database
4. **Database created** automatically with current schema
5. **Default data seeded** (Admin user, settings, groups structure)

#### **For Local Development:**
1. **Delete `auth.db`** file from project root
2. **Run application** → database recreated automatically
3. **Fresh start** with latest schema and features

## Technical Implementation

### **DbSeeder Logic**
```csharp
public static async Task EnsureSeededAsync(AppDbContext db)
{
    // Only seed if database doesn't exist (fresh installation)
    if (await db.Database.EnsureCreatedAsync())
    {
        // Create schema with all current fields
        // Seed default data (Admin, settings, etc.)
        // Database is guaranteed to be fresh
    }
    // If database exists, do nothing (preserve existing data)
}
```

### **Key Benefits**
- **Zero migration complexity** - no ALTER TABLE statements needed
- **Always current schema** - no missing columns or outdated structure
- **Predictable behavior** - same result every time
- **Easy testing** - fresh database for each test run
- **Simple rollback** - just redeploy previous version

## When to Use This Strategy

### ✅ **Perfect For:**
- **Development environments** - frequent schema changes
- **Testing/Staging** - need clean data each time
- **Applications with disposable data** - user data can be recreated
- **Multi-tenant SaaS** - each tenant gets fresh setup
- **Prototype/MVP applications** - data preservation not critical

### ⚠️ **Consider Alternatives For:**
- **Production systems with critical data** - need data preservation
- **Large databases** - recreation takes too long
- **Systems with user-generated content** - data loss unacceptable
- **Compliance requirements** - data retention policies

## Azure Deployment Steps

### **Option 1: Manual Database Recreation**
```bash
# In Azure Portal or using Azure CLI
# 1. Stop the app service
# 2. Delete the auth.db file (via FTP or Kudu)
# 3. Restart the app service
# 4. Database recreated automatically
```

### **Option 2: Automated Deployment Script**
```bash
# Add to your deployment pipeline
az webapp stop --resource-group MyResourceGroup --name MyAppService
# Delete database file (via FTP/Kudu/Storage)
az webapp start --resource-group MyResourceGroup --name MyAppService
```

### **Option 3: Startup Check (Recommended)**
```csharp
// In Program.cs - add version check
if (app.Environment.IsProduction())
{
    // Check if database version matches app version
    // If not, log warning for manual intervention
}
```

## Data Seeding Strategy

### **What Gets Created:**
- **Admin user** (PIN: 1371, Roles: Admin,Player)
- **Default settings** (including GameInitiatorGoesFirst = true)
- **Database schema** (Users, Groups, GroupMembers, Settings tables)
- **Indexes and constraints** (proper relationships and uniqueness)

### **What Gets Preserved:**
- **Existing user accounts** (if database exists)
- **Group memberships** (if database exists)
- **Game history** (if database exists)
- **Custom settings** (if database exists)

## Rollback Strategy

### **If Deployment Fails:**
1. **Stop the app service**
2. **Restore previous version** (via deployment slot swap)
3. **Database remains compatible** (same schema as previous version)
4. **Restart with previous version**

### **Data Backup Considerations:**
- **Export database** before major deployments
- **Use Azure Storage** for database backups
- **Implement backup schedule** for production data
- **Test restore process** regularly

## Migration Alternative (Future)

If you later need to preserve data, you can implement proper EF Core migrations:

```csharp
// Enable migrations
dotnet ef migrations add InitialCreate
dotnet ef migrations add AddGameInitiatorSetting
dotnet ef database update
```

But for now, the fresh database approach provides the cleanest, most reliable deployment experience.

## Monitoring and Logging

### **Add Logging for Deployment:**
```csharp
// In DbSeeder
if (await db.Database.EnsureCreatedAsync())
{
    Console.WriteLine("🆕 Fresh database created - seeding initial data");
    // ... seeding logic
    Console.WriteLine("✅ Database seeding completed successfully");
}
else
{
    Console.WriteLine("📁 Existing database detected - no seeding needed");
}
```

### **Health Check Endpoint:**
```csharp
app.MapGet("/health/db", async (AppDbContext db) =>
{
    try
    {
        var settings = await db.Settings.FirstOrDefaultAsync();
        var admin = await db.Users.FirstOrDefaultAsync(x => x.Name == "Admin");
        
        return Results.Json(new { 
            databaseConnected = true,
            hasSettings = settings != null,
            hasAdmin = admin != null,
            gameInitiatorSetting = settings?.GameInitiatorGoesFirst
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { 
            databaseConnected = false,
            error = ex.Message 
        });
    }
});
```

## Summary

This strategy provides:
- ✅ **Zero deployment complexity**
- ✅ **Perfect schema alignment**  
- ✅ **Predictable behavior**
- ✅ **Easy testing and development**
- ✅ **Simple rollback process**

**Trade-off:** Fresh database on each deployment (data loss acceptable for this use case).

## Implementation Status: ✅ COMPLETE

The DbSeeder has been updated to only run on fresh database creation, making this deployment strategy ready for use.
