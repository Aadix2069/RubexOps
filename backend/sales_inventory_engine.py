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


def normalize_tcs_percent(value):
    """
    TCS is stored literally.

    Examples:
        0.1 -> 0.1
        1   -> 1
        5   -> 5
    """
    number = abs(safe_float(value, 0.0))

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
# RATE / TAX LOGIC
# =========================================================

def calculate_taxable_amount(base_price, weight):
    """
    Taxable Amount = Base Price * Weight.
    """
    base_price = clamp_non_negative(base_price)
    weight = clamp_non_negative(weight)

    taxable_amount = base_price * weight
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


def calculate_tcs_amount(taxable_amount, tcs_percent):
    """
    TCS Amount = TCS Ratio * Taxable Amount.
    """
    taxable_amount = clamp_non_negative(taxable_amount)
    tcs_ratio = min(max(normalize_tcs_percent(tcs_percent) / 100.0, 0.0), 1.0)

    tcs_amount = taxable_amount * tcs_ratio
    return round(max(tcs_amount, 0.0), 2)


def calculate_net_receivable(gross_amount, tcs_amount, loading_charge):
    """
    Net Receivable = Gross Amount + Loading Charge - TCS Amount.
    Frozen at 0 if it goes negative.
    """
    gross_amount = clamp_non_negative(gross_amount)
    tcs_amount = clamp_non_negative(tcs_amount)
    loading_charge = clamp_non_negative(loading_charge)

    net_receivable = gross_amount + loading_charge - tcs_amount
    return round(max(net_receivable, 0.0), 2)


# =========================================================
# MASTER SALES SUMMARY
# =========================================================

def build_sales_summary(sales_record, base_price=0):
    """
    Returns one fully calculated sales object.
    """
    customer_id = safe_string(sales_record.get("customer_id"))
    contract_id = safe_string(sales_record.get("contract_id"))
    invoice_number = safe_string(sales_record.get("invoice_number"))

    sales_order_date = format_date(sales_record.get("sales_order_date"))
    dispatch_date = format_date(sales_record.get("dispatch_date"))

    weight = clamp_non_negative(sales_record.get("weight"))

    gst_percent = normalize_percent_value(
        sales_record.get("gst_percent")
    )

    tcs_percent = normalize_tcs_percent(
        sales_record.get("tcs_percent")
    )

    loading_charge = clamp_non_negative(
        sales_record.get("loading_charge")
    )

    taxable_amount = calculate_taxable_amount(
        base_price,
        weight
    )

    gst_amount = calculate_gst_amount(
        taxable_amount,
        gst_percent
    )

    gross_amount = calculate_gross_amount(
        taxable_amount,
        gst_amount
    )

    tcs_amount = calculate_tcs_amount(
        taxable_amount,
        tcs_percent
    )

    net_receivable = calculate_net_receivable(
        gross_amount,
        tcs_amount,
        loading_charge
    )

    return {
        "customer_id": customer_id,
        "contract_id": contract_id,
        "invoice_number": invoice_number,
        "sales_order_date": sales_order_date,
        "dispatch_date": dispatch_date,
        "weight": round(weight, 2),
        "gst_percent": gst_percent,
        "tcs_percent": tcs_percent,
        "loading_charge": round(loading_charge, 2),
        "base_price": round(clamp_non_negative(base_price), 2),
        "taxable_amount": taxable_amount,
        "gst_amount": gst_amount,
        "gross_amount": gross_amount,
        "tcs_amount": tcs_amount,
        "net_receivable": net_receivable,
    }


def build_sales_summaries(sales_records, base_price=0):
    return [
        build_sales_summary(record, base_price=base_price)
        for record in (sales_records or [])
    ]