from datetime import date, datetime


# =========================================================
# SAFE HELPERS
# =========================================================

def safe_string(value):
    if value is None:
        return ""
    return str(value).strip()


def safe_float(value, default=0.0):
    try:
        text = safe_string(value)
        if text == "":
            return float(default)
        return float(text.replace(",", ""))
    except Exception:
        return float(default)


def parse_date(value):
    if not value:
        return None

    if isinstance(value, datetime):
        return value.date()

    if isinstance(value, date):
        return value

    text = safe_string(value)
    if text == "":
        return None

    for fmt in ("%d-%m-%Y", "%d/%m/%Y", "%Y-%m-%d", "%m/%d/%Y"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            pass

    return None


def format_date(value):
    parsed = parse_date(value)
    if parsed is None:
        return ""
    return parsed.strftime("%d-%m-%Y")


def normalize_percent_value(value):
    """
    Converts 55 to 55.0 and 0.55 to 55.0.
    Clamps to 0..100.
    """
    number = abs(safe_float(value, 0.0))

    if 0 < number <= 1:
        number = number * 100.0

    return min(max(number, 0.0), 100.0)


def normalize_ratio(value):
    """
    Converts 55 to 0.55 and 0.55 to 0.55.
    Clamps to 0..1.
    """
    percent_value = normalize_percent_value(value)
    return min(max(percent_value / 100.0, 0.0), 1.0)


def clamp_non_negative(value):
    return max(safe_float(value, 0.0), 0.0)


# =========================================================
# CORE WEIGHT LOGIC
# =========================================================

def calculate_received_weight(before_unloading, carrier_weight):
    """
    Received Weight = Before Unloading - Carrier Weight.
    """
    before_unloading = clamp_non_negative(before_unloading)
    carrier_weight = clamp_non_negative(carrier_weight)

    received_weight = before_unloading - carrier_weight
    return round(max(received_weight, 0.0), 2)


def calculate_net_weight(received_weight, number_of_bags):
    """
    Net Weight = Received Weight - Number Of Bags.
    """
    received_weight = clamp_non_negative(received_weight)
    number_of_bags = clamp_non_negative(number_of_bags)

    net_weight = received_weight - number_of_bags
    return round(max(net_weight, 0.0), 2)


def calculate_drc_weight(net_weight, calculated_drc_percent):
    """
    DRC Weight = Net Weight * DRC Ratio.
    """
    net_weight = clamp_non_negative(net_weight)
    drc_ratio = normalize_ratio(calculated_drc_percent)

    drc_weight = net_weight * drc_ratio
    return round(max(drc_weight, 0.0), 2)


# =========================================================
# RATE / TAX LOGIC
# =========================================================

def calculate_adjusted_rate(base_price, calculated_drc_percent):
    """
    Adjusted Rate = (Base Price / 80) * DRC Percent.
    This function accepts either 55 or 0.55 safely.
    """
    base_price = clamp_non_negative(base_price)
    drc_percent_number = normalize_percent_value(calculated_drc_percent)

    adjusted_rate = (base_price / 80.0) * drc_percent_number
    return round(max(adjusted_rate, 0.0), 2)


def calculate_taxable_amount(adjusted_rate, net_weight):
    """
    Taxable Amount = Adjusted Rate * Net Weight.
    """
    adjusted_rate = clamp_non_negative(adjusted_rate)
    net_weight = clamp_non_negative(net_weight)

    taxable_amount = adjusted_rate * net_weight
    return round(max(taxable_amount, 0.0), 2)


def calculate_gst_amount(taxable_amount, gst_percent):
    """
    GST Amount = GST Ratio * Taxable Amount.
    """
    taxable_amount = clamp_non_negative(taxable_amount)
    gst_ratio = normalize_ratio(gst_percent)

    gst_amount = taxable_amount * gst_ratio
    return round(max(gst_amount, 0.0), 2)


def calculate_gross_amount(taxable_amount, gst_amount):
    """
    Gross Amount = Taxable Amount + GST Amount.
    """
    taxable_amount = clamp_non_negative(taxable_amount)
    gst_amount = clamp_non_negative(gst_amount)

    gross_amount = taxable_amount + gst_amount
    return round(max(gross_amount, 0.0), 2)


def calculate_tds_amount(taxable_amount, tds_percent):
    """
    TDS Amount = TDS Ratio * Taxable Amount.
    """
    taxable_amount = clamp_non_negative(taxable_amount)
    tds_ratio = normalize_ratio(tds_percent)

    tds_amount = taxable_amount * tds_ratio
    return round(max(tds_amount, 0.0), 2)


def calculate_net_payable(gross_amount, tds_amount, unloading_charge):
    """
    Net Payable = Gross Amount - TDS Amount - Unloading Charge.
    Frozen at 0 if it goes negative.
    """
    gross_amount = clamp_non_negative(gross_amount)
    tds_amount = clamp_non_negative(tds_amount)
    unloading_charge = clamp_non_negative(unloading_charge)

    net_payable = gross_amount - tds_amount - unloading_charge
    return round(max(net_payable, 0.0), 2)


# =========================================================
# MASTER PURCHASE SUMMARY
# =========================================================

def build_purchase_summary(purchase_record, base_price=0):
    """
    Returns one fully calculated purchase object.
    """
    vendor_id = safe_string(purchase_record.get("vendor_id"))
    contract_id = safe_string(purchase_record.get("contract_id"))
    invoice_number = safe_string(purchase_record.get("invoice_number"))

    purchase_order_date = format_date(purchase_record.get("purchase_order_date"))
    delivery_date = format_date(purchase_record.get("delivery_date"))

    invoice_weight = clamp_non_negative(purchase_record.get("invoice_weight"))
    before_unloading = clamp_non_negative(purchase_record.get("before_unloading"))
    carrier_weight = clamp_non_negative(purchase_record.get("carrier_weight"))
    number_of_bags = clamp_non_negative(purchase_record.get("number_of_bags"))

    calculated_drc_percent = normalize_percent_value(
        purchase_record.get("calculated_drc_percent")
    )

    gst_percent = normalize_percent_value(
        purchase_record.get("gst_percent")
    )

    tds_percent = normalize_percent_value(
        purchase_record.get("tds_percent")
    )

    unloading_charge = clamp_non_negative(
        purchase_record.get("unloading_charge")
    )

    received_weight = calculate_received_weight(
        before_unloading,
        carrier_weight
    )

    net_weight = calculate_net_weight(
        received_weight,
        number_of_bags
    )

    drc_weight = calculate_drc_weight(
        net_weight,
        calculated_drc_percent
    )

    adjusted_rate = calculate_adjusted_rate(
        base_price,
        calculated_drc_percent
    )

    taxable_amount = calculate_taxable_amount(
        adjusted_rate,
        net_weight
    )

    gst_amount = calculate_gst_amount(
        taxable_amount,
        gst_percent
    )

    gross_amount = calculate_gross_amount(
        taxable_amount,
        gst_amount
    )

    tds_amount = calculate_tds_amount(
        taxable_amount,
        tds_percent
    )

    net_payable = calculate_net_payable(
        gross_amount,
        tds_amount,
        unloading_charge
    )

    return {
        "vendor_id": vendor_id,
        "contract_id": contract_id,
        "invoice_number": invoice_number,
        "purchase_order_date": purchase_order_date,
        "delivery_date": delivery_date,
        "invoice_weight": round(invoice_weight, 2),
        "before_unloading": round(before_unloading, 2),
        "carrier_weight": round(carrier_weight, 2),
        "number_of_bags": round(number_of_bags, 2),
        "calculated_drc_percent": calculated_drc_percent,
        "gst_percent": gst_percent,
        "tds_percent": tds_percent,
        "unloading_charge": round(unloading_charge, 2),
        "received_weight": received_weight,
        "net_weight": net_weight,
        "drc_weight": drc_weight,
        "adjusted_rate": adjusted_rate,
        "taxable_amount": taxable_amount,
        "gst_amount": gst_amount,
        "gross_amount": gross_amount,
        "tds_amount": tds_amount,
        "net_payable": net_payable,
    }


def build_purchase_summaries(purchase_records, base_price=0):
    return [
        build_purchase_summary(record, base_price=base_price)
        for record in (purchase_records or [])
    ]
