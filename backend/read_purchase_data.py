import json
import sys

from contract_engine import ITEM_CODE, ITEM_NAME, safe_string
from database import (
    PURCHASE_SHEET_NAME,
    PURCHASE_USED_COLUMNS,
    START_ROW,
    load_database,
    read_pcon,
    row_is_empty,
    safe_cell,
    safe_int,
    safe_number,
)
from inventory_engine import build_purchase_summary


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def read_purchase_with_rows():
    workbook = load_database()
    try:
        if PURCHASE_SHEET_NAME not in workbook.sheetnames:
            raise ValueError(f"Sheet not found: {PURCHASE_SHEET_NAME}")

        sheet = workbook[PURCHASE_SHEET_NAME]
        purchases = []

        for row in range(START_ROW, sheet.max_row + 1):
            if row_is_empty(sheet, row, PURCHASE_USED_COLUMNS):
                continue

            contract_id = safe_string(safe_cell(sheet, row, "C"))
            invoice_number = safe_string(safe_cell(sheet, row, "D"))

            if contract_id == "" or invoice_number == "":
                continue

            purchases.append(
                {
                    "row": row,
                    "sl_no": safe_int(safe_cell(sheet, row, "A")),
                    "vendor_id": safe_string(safe_cell(sheet, row, "B")),
                    "contract_id": contract_id,
                    "invoice_number": invoice_number,
                    "purchase_order_date": safe_cell(sheet, row, "E"),
                    "delivery_date": safe_cell(sheet, row, "F"),
                    "invoice_weight": safe_number(safe_cell(sheet, row, "G")),
                    "before_unloading": safe_number(safe_cell(sheet, row, "H")),
                    "carrier_weight": safe_number(safe_cell(sheet, row, "I")),
                    "number_of_bags": safe_number(safe_cell(sheet, row, "J")),
                    "calculated_drc_percent": safe_number(safe_cell(sheet, row, "K")),
                    "gst_percent": safe_number(safe_cell(sheet, row, "L")),
                    "tds_percent": safe_number(safe_cell(sheet, row, "M")),
                    "unloading_charge": safe_number(safe_cell(sheet, row, "N")),
                }
            )

        return purchases
    finally:
        workbook.close()


def contract_lookup():
    lookup = {}
    for contract in read_pcon():
        contract_id = safe_string(contract.get("contract_id"))
        if contract_id:
            lookup[contract_id.casefold()] = contract
    return lookup


def as_purchase_json(record, summary, contract):
    base_rate = safe_number(contract.get("base_price")) if contract else 0.0
    vendor_name = safe_string(contract.get("vendor_name")) if contract else ""

    row_data = {
        "RowNumber": record["row"],
        "SlNo": record.get("sl_no", 0),
        "VendorName": vendor_name,
        "VendorID": summary["vendor_id"],
        "ContractID": summary["contract_id"],
        "ItemName": ITEM_NAME,
        "ItemCode": ITEM_CODE,
        "InvoiceNumber": summary["invoice_number"],
        "PurchaseOrderDate": summary["purchase_order_date"],
        "DeliveryDate": summary["delivery_date"],
        "InvoiceWeight": summary["invoice_weight"],
        "BeforeUnloading": summary["before_unloading"],
        "CarrierWeight": summary["carrier_weight"],
        "ReceivedWeight": summary["received_weight"],
        "NoOfBags": summary["number_of_bags"],
        "NetWeight": summary["net_weight"],
        "CalculatedDrc": summary["calculated_drc_percent"],
        "DrcWeight": summary["drc_weight"],
        "BaseRate": base_rate,
        "AdjustedRate": summary["adjusted_rate"],
        "GstPercent": summary["gst_percent"],
        "Tds194QPercent": summary["tds_percent"],
        "TaxableAmount": summary["taxable_amount"],
        "UnloadingCharge": summary["unloading_charge"],
        "GstAmount": summary["gst_amount"],
        "GrossAmount": summary["gross_amount"],
        "TdsAmount": summary["tds_amount"],
        "NetPayable": summary["net_payable"],
    }

    row_data.update(
        {
            "row": record["row"],
            "sl_no": record.get("sl_no", 0),
            "vendor_name": vendor_name,
            "vendor_id": summary["vendor_id"],
            "contract_id": summary["contract_id"],
            "item_name": ITEM_NAME,
            "item_code": ITEM_CODE,
            "invoice_number": summary["invoice_number"],
            "purchase_order_date": summary["purchase_order_date"],
            "delivery_date": summary["delivery_date"],
            "invoice_weight": summary["invoice_weight"],
            "before_unloading": summary["before_unloading"],
            "carrier_weight": summary["carrier_weight"],
            "received_weight": summary["received_weight"],
            "number_of_bags": summary["number_of_bags"],
            "net_weight": summary["net_weight"],
            "calculated_drc_percent": summary["calculated_drc_percent"],
            "drc_weight": summary["drc_weight"],
            "base_rate": base_rate,
            "adjusted_rate": summary["adjusted_rate"],
            "gst_percent": summary["gst_percent"],
            "tds_percent": summary["tds_percent"],
            "taxable_amount": summary["taxable_amount"],
            "unloading_charge": summary["unloading_charge"],
            "gst_amount": summary["gst_amount"],
            "gross_amount": summary["gross_amount"],
            "tds_amount": summary["tds_amount"],
            "net_payable": summary["net_payable"],
        }
    )

    return row_data


def read_purchase_data():
    purchases = read_purchase_with_rows()
    contracts = contract_lookup()
    output = []

    for record in purchases:
        contract = contracts.get(safe_string(record.get("contract_id")).casefold())
        base_rate = safe_number(contract.get("base_price")) if contract else 0.0
        summary = build_purchase_summary(record, base_price=base_rate)
        output.append(as_purchase_json(record, summary, contract))

    return output


try:
    print(json.dumps(read_purchase_data(), ensure_ascii=False))
except Exception as error:
    fail(str(error))
