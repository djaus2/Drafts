# Database Export Summary

## Export Information
- **Source File:** `C:\Users\david\Downloads\auth_backup_20260316_021641.db`
- **Export Date:** 2026-03-16T03:10:33.486822Z
- **Output File:** `database_export.json`
- **Format:** JSON with full schema and data

## Database Structure

### 📊 **Tables Exported:**
1. **GroupMembers** - Group membership relationships
2. **Groups** - Group definitions and ownership
3. **Settings** - Application configuration
4. **Users** - User accounts and credentials

## Data Overview

### 👥 **Users Table (6 records)**
| Id | Name | Roles | PIN Status |
|----|------|-------|------------|
| 1 | Admin | Admin,Player | ✅ Secured |
| 2 | Bob | Player | ✅ Secured |
| 3 | Carol | Player | ✅ Secured |
| 4 | Tad | Player | ✅ Secured |
| 5 | Alice | Player | ✅ Secured |
| 6 | Fred | Player | ✅ Secured |

**Notes:**
- All users have secure PIN hashes and salts
- PIN salts and hashes are Base64 encoded in JSON
- TTS preferences are null for all users

### 🏷️ **Groups Table (2 records)**
| Id | Name | Description | OwnerUserId |
|----|------|-------------|-------------|
| 1 | Bob-Carol | Bob and Carol's group | 2 (Bob) |
| 2 | Ted-Alice | Ted and Alice's group | 5 (Alice) |

**Notes:**
- Groups have proper ownership relationships
- Descriptions stored with escaped characters in JSON

### 🔗 **GroupMembers Table (4 records)**
| Id | GroupId | UserId | JoinedAtUtc |
|----|---------|--------|-------------|
| 1 | 1 | 2 (Bob) | 2026-03-16 00:54:59 |
| 2 | 1 | 3 (Carol) | 2026-03-16 00:54:59 |
| 3 | 2 | 5 (Alice) | 2026-03-16 00:54:59 |
| 4 | 2 | 4 (Tad) | 2026-03-16 00:54:59 |

**Notes:**
- Bob owns Bob-Carol group and is a member
- Carol is member of Bob-Carol group
- Alice owns Ted-Alice group and is a member
- Tad is member of Ted-Alice group
- Fred is not a member of any group (as expected)

### ⚙️ **Settings Table (1 record)**
| Property | Value |
|----------|-------|
| Id | 1 |
| MaxTimeoutMins | 30 |
| ReaperPeriodSeconds | 30 |
| LastMoveHighlightColor | rgba(255,0,0,0.85) |
| EntrapmentMode | 1 (true) |
| MultiJumpGraceSeconds | 1.5 |
| GameInitiatorGoesFirst | 1 (true) |

**Notes:**
- All expected settings are present
- GameInitiatorGoesFirst is properly set to true
- Boolean values stored as integers (0/1)

## JSON Structure

### **Format Details:**
```json
{
  "exportDate": "2026-03-16T03:10:33.486822Z",
  "sourceFile": "C:\\Users\\david\\Downloads\\auth_backup_20260316_021641.db",
  "tables": {
    "TableName": {
      "tableName": "TableName",
      "columns": [
        {
          "name": "ColumnName",
          "type": "DataType",
          "notNull": boolean,
          "defaultValue": value|null,
          "isPrimaryKey": boolean
        }
      ],
      "rows": [
        {
          "ColumnName": value,
          ...
        }
      ]
    }
  }
}
```

### **Data Type Handling:**
- **TEXT** → JSON strings
- **INTEGER** → JSON numbers
- **REAL** → JSON numbers with decimals
- **BLOB** → Base64 encoded strings
- **DATETIME** → ISO 8601 formatted strings
- **NULL** → JSON null

### **Character Encoding:**
- Special characters are properly escaped (e.g., apostrophes as \u0027)
- Unicode characters preserved correctly
- Base64 encoding for binary data (PIN hashes/salts)

## Security Considerations

### 🔐 **Sensitive Data:**
- **PIN hashes** are included but cryptographically secure
- **PIN salts** are included but random per user
- **No plain text passwords** or PINs stored

### 📋 **Recommendations:**
- **Treat JSON file as sensitive** - contains credential hashes
- **Store securely** - same security level as database file
- **Don't commit to version control** - exclude via .gitignore
- **Delete after use** - if not needed for long-term storage

## Usage Scenarios

### **Development/Testing:**
- **Database seeding reference** - replicate exact data structure
- **Test data validation** - ensure proper relationships
- **Migration testing** - verify data integrity

### **Backup/Recovery:**
- **Data snapshot** - point-in-time database state
- **Migration source** - import to new database
- **Analysis** - inspect data relationships

### **Documentation:**
- **Schema reference** - complete table definitions
- **Data examples** - sample records for testing
- **Configuration backup** - settings preservation

## Import Capability

The JSON format is structured to enable potential re-import:
- **Schema preservation** - column definitions maintained
- **Data integrity** - relationships and constraints preserved
- **Type safety** - data types properly serialized

## File Locations

- **Source Database:** `C:\Users\david\Downloads\auth_backup_20260316_021641.db`
- **JSON Export:** `c:\Users\david\source\repos\Drafts\database_export.json`
- **Documentation:** `c:\Users\david\source\repos\Drafts\docs\database-export-summary.md`

## Verification Checklist

### ✅ **Data Completeness:**
- [x] All 4 tables exported
- [x] All user accounts present (6 total)
- [x] All groups present (2 total)
- [x] All group memberships present (4 total)
- [x] Settings record present

### ✅ **Data Integrity:**
- [x] Foreign key relationships preserved
- [x] Primary key constraints maintained
- [x] Data types correctly converted
- [x] Special characters properly escaped

### ✅ **Security:**
- [x] No plain text credentials exposed
- [x] Hashes and salts properly encoded
- [x] File location noted for security

## Status: ✅ COMPLETE

Database successfully exported to JSON format with complete schema and data preservation. The export includes all users, groups, memberships, and settings from the specified SQLite database file.
