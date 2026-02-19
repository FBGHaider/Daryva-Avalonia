#!/usr/bin/env python3
"""
Diagnostic script to check SQLite database contents and compare with what should be there.
"""

import sqlite3
import os
from pathlib import Path

# Path to SQLite database
db_path = Path(os.getenv("APPDATA")) / "Daryva" / "Database" / "DaryvaDB.db"

if not db_path.exists():
    print(f"ERROR: Database not found at {db_path}")
    exit(1)

print(f"Connecting to {db_path}")
print("-" * 80)

conn = sqlite3.connect(db_path)
cursor = conn.cursor()

try:
    # Get all tables
    cursor.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;")
    tables = [row[0] for row in cursor.fetchall()]
    print(f"Tables in database: {', '.join(tables)}\n")
    
    # Get table structures
    for table in tables:
        cursor.execute(f"PRAGMA table_info({table})")
        cols = cursor.fetchall()
        print(f"\n{table}:")
        for col in cols:
            print(f"  {col[1]} ({col[2]})")
    
    # Print data counts
    print("\n" + "=" * 80)
    print("DATA COUNTS:")
    print("=" * 80)
    
    for table in tables:
        cursor.execute(f"SELECT COUNT(*) FROM {table}")
        count = cursor.fetchone()[0]
        print(f"{table:30} : {count:5} rows")
    
    # Show sample data for key tables
    print("\n" + "=" * 80)
    print("SAMPLE DATA:")
    print("=" * 80)
    
    # Houses
    print("\nHouses:")
    cursor.execute("SELECT HouseId, Name, AddressLine1, City, Postcode FROM House LIMIT 5")
    cols = [description[0] for description in cursor.description]
    print("  " + " | ".join(f"{col:20}" for col in cols))
    for row in cursor.fetchall():
        print("  " + " | ".join(f"{str(v)[:20]:20}" for v in row))
    
    # Tenants
    print("\nTenants:")
    cursor.execute("SELECT TenantId, FullName, Email, PhoneNumber FROM Tenant LIMIT 10")
    cols = [description[0] for description in cursor.description]
    print("  " + " | ".join(f"{col:15}" for col in cols))
    for row in cursor.fetchall():
        print("  " + " | ".join(f"{str(v)[:15]:15}" for v in row))
    
    # Tenancies
    print("\nTenancies:")
    cursor.execute("""
        SELECT 
            TenancyId, HouseId, TenantId, MoveInDate, MoveOutDate, RentAmountMonthly
        FROM Tenancy 
        LIMIT 10
    """)
    cols = [description[0] for description in cursor.description]
    print("  " + " | ".join(f"{col:15}" for col in cols))
    for row in cursor.fetchall():
        print("  " + " | ".join(f"{str(v)[:15]:15}" for v in row))

finally:
    conn.close()

print("\n" + "=" * 80)
print("Database diagnostic complete.")
