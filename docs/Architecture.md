# RubexOps Architecture

## Layers

### Excel Layer

Stores:

- Purchase Contracts
- Purchase Entries

No calculations are performed here.

### Python Layer

Files:

- database.py
- contract_engine.py
- inventory_engine.py

Responsibilities:

- Reading data
- Validation
- Calculations
- Business rules

### C# Layer

Responsibilities:

- UI
- Navigation
- User interaction

No business calculations should exist here.

## Data Flow

Excel
↓
database.py
↓
contract_engine.py / inventory_engine.py
↓
Python Scripts
↓
C#
↓
User