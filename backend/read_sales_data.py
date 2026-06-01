import json
import sys
from sales_contract_engine import ITEM_CODE, ITEM_NAME, safe_string
from database import (
    SALES_SHEET_NAME,
    SALES_USED_COLUMNS,
    START_ROW,
    load_database,
    read_scon,
    row_is_empty,
    safe_cell,
    safe_int,
    safe_number,
)
from sales_inventory_engine import build_sales_summary


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def read_sales_with_rows():
    workbook = load_database()
    try:
        if SALES_SHEET_NAME not in workbook.sheetnames:
            raise ValueError(f"Sheet not found: {SALES_SHEET_NAME}")

        sheet = workbook[SALES_SHEET_NAME]
        sales = []

        for row in range(START_ROW, sheet.max_row + 1):
            if row_is_empty(sheet, row, SALES_USED_COLUMNS):
                continue

            contract_id = safe_string(safe_cell(sheet, row, "C"))
            invoice_number = safe_string(safe_cell(sheet, row, "D"))

            if contract_id == "" or invoice_number == "":
                continue

            sales.append(
                {
                    "row": row,
                    "sl_no": safe_int(safe_cell(sheet, row, "A")),
                    "customer_id": safe_string(safe_cell(sheet, row, "B")),
                    "contract_id": contract_id,
                    "invoice_number": invoice_number,
                    "sales_order_date": safe_cell(sheet, row, "E"),
                    "dispatch_date": safe_cell(sheet, row, "F"),
                    "weight": safe_number(safe_cell(sheet, row, "G")),
                    "gst_percent": safe_number(safe_cell(sheet, row, "H")),
                    "tcs_percent": safe_number(safe_cell(sheet, row, "I")),
                    "loading_charge": safe_number(safe_cell(sheet, row, "J")),
                }
            )

        return sales
    finally:
        workbook.close()


def contract_lookup():
    lookup = {}

    for contract in read_scon():
        contract_id = safe_string(contract.get("contract_id"))
        if contract_id:
            lookup[contract_id.casefold()] = contract

    return lookup


def as_sales_json(record, summary, contract):
    base_rate = safe_number(contract.get("base_price")) if contract else 0.0
    customer_name = safe_string(contract.get("customer_name")) if contract else ""

    row_data = {
        # Legacy / display-friendly keys
        "RowNumber": record["row"],
        "SlNo": record.get("sl_no", 0),
        "CustomerName": customer_name,
        "CustomerID": summary["customer_id"],
        "ContractID": summary["contract_id"],
        "ItemName": ITEM_NAME,
        "ItemCode": ITEM_CODE,
        "InvoiceNumber": summary["invoice_number"],
        "SalesOrderDate": summary["sales_order_date"],
        "DispatchDate": summary["dispatch_date"],
        "Weight": summary["weight"],
        "BaseRate": base_rate,
        "GstPercent": summary["gst_percent"],
        "Tcs194QPercent": summary["tcs_percent"],
        "TaxableAmount": summary["taxable_amount"],
        "LoadingCharge": summary["loading_charge"],
        "GstAmount": summary["gst_amount"],
        "GrossAmount": summary["gross_amount"],
        "TcsAmount": summary["tcs_amount"],
        "NetReceivable": summary["net_receivable"],

        # Lowercase / engine-friendly keys
        "row": record["row"],
        "sl_no": record.get("sl_no", 0),
        "customer_name": customer_name,
        "customer_id": summary["customer_id"],
        "contract_id": summary["contract_id"],
        "item_name": ITEM_NAME,
        "item_code": ITEM_CODE,
        "invoice_number": summary["invoice_number"],
        "sales_order_date": summary["sales_order_date"],
        "dispatch_date": summary["dispatch_date"],
        "weight": summary["weight"],
        "base_rate": base_rate,
        "gst_percent": summary["gst_percent"],
        "tcs_percent": summary["tcs_percent"],
        "taxable_amount": summary["taxable_amount"],
        "loading_charge": summary["loading_charge"],
        "gst_amount": summary["gst_amount"],
        "gross_amount": summary["gross_amount"],
        "tcs_amount": summary["tcs_amount"],
        "net_receivable": summary["net_receivable"],
    }

    return row_data


def read_sales_data():
    sales_records = read_sales_with_rows()
    contracts = contract_lookup()
    output = []

    for record in sales_records:
        contract = contracts.get(safe_string(record.get("contract_id")).casefold())
        base_rate = safe_number(contract.get("base_price")) if contract else 0.0

        summary = build_sales_summary(
            record,
            base_price=base_rate,
        )

        output.append(
            as_sales_json(record, summary, contract)
        )

    return output


try:
    print(json.dumps(read_sales_data(), ensure_ascii=False))
except Exception as error:
    fail(str(error))