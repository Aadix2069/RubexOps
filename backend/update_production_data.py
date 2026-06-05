import sys
from datetime import date
from typing import List

from database import (
    load_database,
    save_database,
    _get_sheet_by_candidates,
    PRODUCTION_SHEET_CANDIDATES,
    get_purchase_by_invoice,
    get_purchase_quantity,
    safe_string,
    safe_number
)
from production_engine import parse_date


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def main(args: List[str]) -> None:
    # Expects exactly 3 arguments passed from the C# application
    if len(args) != 3:
        fail("Expected exactly 3 arguments: row_number, production_date, output_weight.")

    try:
        row_target = int(args[0])
    except ValueError:
        fail("Invalid row index provided by application.")

    new_prod_date_str = args[1]
    new_output_weight = safe_number(args[2])

    if row_target < 5:
        fail("Critical error: Target row falls outside data boundaries.")

    # 1. Load Excel and verify the row actually holds data
    wb = load_database()
    try:
        sheet = _get_sheet_by_candidates(wb, PRODUCTION_SHEET_CANDIDATES, "production")

        # Column mapping based on new schema
        # Col B = Batch ID, Col C = Prod Date, Col D = Invoice, Col E = Output
        batch_id = safe_string(sheet[f"B{row_target}"].value)
        invoice_number = safe_string(sheet[f"D{row_target}"].value)

        if not batch_id or not invoice_number:
            fail("Cannot edit empty or corrupted row.")

        # 2. Re-evaluate against the Purchase Logic bounds
        purchase = get_purchase_by_invoice(invoice_number)
        if not purchase:
            fail(f"Safety Block: The source invoice ({invoice_number}) no longer exists in purchase records.")

        # 3. Date Integrity Checks
        prod_date = parse_date(new_prod_date_str)
        if not prod_date:
            fail("A valid production date is required.")

        purch_date = parse_date(purchase.get("purchase_order_date"))
        if purch_date and prod_date < purch_date:
            fail(f"Production Date ({prod_date.strftime('%d-%m-%Y')}) cannot be earlier than Purchase Date ({purch_date.strftime('%d-%m-%Y')}).")

        if prod_date > date.today():
            fail("Production Date cannot be set in the future.")

        # 4. Mass Conservation Checks
        if new_output_weight <= 0:
            fail("Output weight must be greater than zero.")

        input_qty = get_purchase_quantity(purchase)
        if new_output_weight > input_qty:
            fail(f"Output ({new_output_weight:,.2f} kg) cannot exceed purchased Input quantity ({input_qty:,.2f} kg).")

        # 5. Chemistry Reality Check
        if input_qty > 0:
            new_yield = (new_output_weight / input_qty) * 100.0
            if new_yield > 100.0:
                fail(f"Calculation Error: Yield cannot exceed 100% (Current: {new_yield:.2f}%). Check weights.")

        # 6. Perform Surgical Write
        # Write Date to Col C, Weight to Col E
        sheet[f"C{row_target}"] = prod_date.strftime("%d-%m-%Y")
        sheet[f"E{row_target}"] = new_output_weight

        save_database(wb)
        print(f"Batch {batch_id} updated successfully.")

    except Exception as e:
        fail(f"Database write operation failed: {str(e)}")
    finally:
        wb.close()


if __name__ == "__main__":
    try:
        main(sys.argv[1:])
    except Exception as error:
        fail(str(error))