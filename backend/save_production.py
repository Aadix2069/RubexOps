import sys
from datetime import date

from database import (
    append_production,
    get_purchase_by_invoice,
    production_invoice_exists,
    get_purchase_quantity,
    read_production_by_invoice,
    safe_string,
    safe_number
)
from production_engine import parse_date


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def main(args):
    # Expects exactly 3 arguments passed from the C# application
    if len(args) != 3:
        fail("Expected exactly 3 arguments: production_date, invoice_number, output_weight.")

    prod_date_str = args[0]
    invoice_number = safe_string(args[1])
    output_weight = safe_number(args[2])

    if not invoice_number:
        fail("Invoice number is required.")

    # 1. Ensure the selected invoice actually exists in the purchase records
    purchase = get_purchase_by_invoice(invoice_number)
    if not purchase:
        fail(f"Purchase invoice '{invoice_number}' could not be found.")

    # 2. Block 1-to-1 duplication
    if production_invoice_exists(invoice_number):
        fail(f"Invoice '{invoice_number}' has already been processed into a production batch.")

    # 3. Validate the production date
    prod_date = parse_date(prod_date_str)
    if not prod_date:
        fail("A valid production date is required.")

    purch_date = parse_date(purchase.get("purchase_order_date"))
    if purch_date and prod_date < purch_date:
        fail(f"Production Date ({prod_date.strftime('%d-%m-%Y')}) cannot be earlier than Purchase Date ({purch_date.strftime('%d-%m-%Y')}).")

    if prod_date > date.today():
        fail("Production Date cannot be set in the future.")

    # 4. Enforce mass conservation laws
    if output_weight <= 0:
        fail("Output weight must be greater than zero.")

    input_qty = get_purchase_quantity(purchase)
    if output_weight > input_qty:
        fail(f"Output weight ({output_weight:,.2f} kg) cannot exceed the purchased Input quantity ({input_qty:,.2f} kg).")

    # 5. Build the raw payload (database.py will auto-generate the Batch ID using the contract and sequence logic)
    payload = {
        "invoice_number": invoice_number,
        "production_date": prod_date,
        "output": output_weight
    }

    # 6. Securely append to Excel
    append_production(payload)

    # 7. Fetch the newly assigned Batch ID to display on the success screen
    new_record = read_production_by_invoice(invoice_number)
    batch_id = safe_string(new_record.get("batch_id")) if new_record else "UNKNOWN"

    print(f"Production successfully saved | Batch ID: {batch_id}")


if __name__ == "__main__":
    try:
        main(sys.argv[1:])
    except Exception as error:
        fail(str(error))