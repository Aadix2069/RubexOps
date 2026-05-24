import json
import os
import sys
from copy import copy
from pathlib import Path

from openpyxl.formula.translate import Translator
from openpyxl import load_workbook


# ============================================================
# CONFIG
# ============================================================

CONFIG_PATH = (
    Path.home()
    / "Documents"
    / "RubexOps"
    / "database_config.json"
)

PURCHASE_SHEET_NAME = "purchase"
START_ROW = 5


# ============================================================
# COLUMN MAP
# ============================================================

COL_SERIAL_NO = "A"
COL_VENDOR_NAME = "B"
COL_VENDOR_ID = "C"
COL_ITEM_NAME = "D"
COL_ITEM_CODE = "E"
COL_INVOICE_NUMBER = "F"
COL_PURCHASE_ORDER = "G"
COL_DELIVERY = "H"
COL_INVOICE_WEIGHT = "I"
COL_BEFORE_UNLOADING = "J"
COL_CARRIER_WEIGHT = "K"
COL_NO_OF_BAGS = "M"
COL_CALCULATED_DRC = "O"

DEFAULT_ITEM_NAME = "Natural Rubber Field Coagulum"


# ============================================================
# DATABASE PATH
# ============================================================

def get_database_path():
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(
            f"Database config file not found: {CONFIG_PATH}"
        )

    with open(CONFIG_PATH, "r", encoding="utf-8") as file:
        config = json.load(file)

    database_path = (
        config.get("database_path")
        or config.get("excel_path")
        or config.get("path")
    )

    if not database_path:
        raise ValueError(
            "Database path was not found in database_config.json."
        )

    if not os.path.exists(database_path):
        raise FileNotFoundError(
            f"Excel database file not found: {database_path}"
        )

    return database_path


# ============================================================
# ROW HELPERS
# ============================================================

def get_next_purchase_row(sheet):
    row = START_ROW

    while True:
        vendor_id = sheet[f"{COL_VENDOR_ID}{row}"].value
        invoice_number = sheet[f"{COL_INVOICE_NUMBER}{row}"].value

        if vendor_id is None and invoice_number is None:
            return row

        row += 1


def copy_row_style_and_formulas(sheet, source_row, target_row):
    for column in range(1, sheet.max_column + 1):
        source_cell = sheet.cell(
            row=source_row,
            column=column
        )

        target_cell = sheet.cell(
            row=target_row,
            column=column
        )

        if source_cell.has_style:
            target_cell.font = copy(source_cell.font)
            target_cell.fill = copy(source_cell.fill)
            target_cell.border = copy(source_cell.border)
            target_cell.alignment = copy(source_cell.alignment)
            target_cell.number_format = source_cell.number_format
            target_cell.protection = copy(source_cell.protection)

        if isinstance(source_cell.value, str) and source_cell.value.startswith("="):
            target_cell.value = Translator(
                source_cell.value,
                origin=source_cell.coordinate
            ).translate_formula(target_cell.coordinate)

    if source_row in sheet.row_dimensions:
        sheet.row_dimensions[target_row].height = (
            sheet.row_dimensions[source_row].height
        )


# ============================================================
# WRITE PURCHASE DATA
# ============================================================

def create_purchase_data(args):
    if len(args) != 11:
        raise ValueError(
            "Expected 11 arguments: vendor name, vendor id, item code, "
            "invoice number, purchase order date, delivery date, invoice "
            "weight, before unloading, carrier weight, no. of bags, "
            "calculated DRC."
        )

    (
        vendor_name,
        vendor_id,
        item_code,
        invoice_number,
        purchase_order_date,
        delivery_date,
        invoice_weight,
        before_unloading,
        carrier_weight,
        no_of_bags,
        calculated_drc,
    ) = args

    database_path = get_database_path()

    workbook = load_workbook(
        database_path,
        data_only=False
    )

    if PURCHASE_SHEET_NAME not in workbook.sheetnames:
        raise ValueError(
            f"Sheet not found: {PURCHASE_SHEET_NAME}"
        )

    sheet = workbook[PURCHASE_SHEET_NAME]

    target_row = get_next_purchase_row(sheet)

    template_row = (
        target_row - 1
        if target_row > START_ROW
        else START_ROW
    )

    copy_row_style_and_formulas(
        sheet,
        template_row,
        target_row
    )

    sheet[f"{COL_SERIAL_NO}{target_row}"] = target_row - START_ROW + 1
    sheet[f"{COL_VENDOR_NAME}{target_row}"] = vendor_name
    sheet[f"{COL_VENDOR_ID}{target_row}"] = vendor_id
    sheet[f"{COL_ITEM_NAME}{target_row}"] = DEFAULT_ITEM_NAME
    sheet[f"{COL_ITEM_CODE}{target_row}"] = item_code
    sheet[f"{COL_INVOICE_NUMBER}{target_row}"] = invoice_number
    sheet[f"{COL_PURCHASE_ORDER}{target_row}"] = purchase_order_date
    sheet[f"{COL_DELIVERY}{target_row}"] = delivery_date
    sheet[f"{COL_INVOICE_WEIGHT}{target_row}"] = float(invoice_weight)
    sheet[f"{COL_BEFORE_UNLOADING}{target_row}"] = float(before_unloading)
    sheet[f"{COL_CARRIER_WEIGHT}{target_row}"] = float(carrier_weight)
    sheet[f"{COL_NO_OF_BAGS}{target_row}"] = int(no_of_bags)
    sheet[f"{COL_CALCULATED_DRC}{target_row}"] = float(calculated_drc)

    workbook.calculation.fullCalcOnLoad = True
    workbook.calculation.forceFullCalc = True

    workbook.save(database_path)

    return f"Purchase data saved successfully at row {target_row}."


# ============================================================
# MAIN
# ============================================================

try:
    print(
        create_purchase_data(
            sys.argv[1:]
        )
    )

except Exception as error:
    print(f"ERROR: {error}")
    sys.exit(1)
