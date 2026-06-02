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


def clamp_non_negative(value):
    return max(safe_float(value, 0.0), 0.0)


# =========================================================
# CORE PRODUCTION LOGIC
# =========================================================

def calculate_production_loss(input_qty, output_qty):
    """
    Production Loss = Input Quantity - Output
    """
    input_qty = clamp_non_negative(input_qty)
    output_qty = clamp_non_negative(output_qty)

    # In a valid scenario, output <= input. 
    # The validation gates handle blocking illegal values.
    production_loss = input_qty - output_qty
    return round(production_loss, 2)


def calculate_actual_drc(input_qty, output_qty):
    """
    Actual DRC = (Output / Input) * 100
    """
    input_qty = clamp_non_negative(input_qty)
    output_qty = clamp_non_negative(output_qty)

    if input_qty <= 0:
        return 0.0

    actual_drc = (output_qty / input_qty) * 100.0
    return round(actual_drc, 2)


def calculate_drc_variance(actual_drc, initial_drc):
    """
    DRC Variance = Actual DRC - Initial DRC
    Note: Preserves the sign. Positive means the yield outperformed 
    the lab test; Negative means the yield underperformed.
    """
    actual_drc = safe_float(actual_drc, 0.0)
    initial_drc = safe_float(initial_drc, 0.0)

    variance = actual_drc - initial_drc
    return round(variance, 2)


# =========================================================
# MASTER PRODUCTION SUMMARY
# =========================================================

def build_production_summary(production_record, purchase_summary):
    """
    Merges the raw production record (entered by the user) with the 
    historical data fetched from its parent purchase invoice.
    Returns a fully calculated production snapshot.
    """
    
    # 1. Base Raw Inputs (From Production Excel)
    sl_no = safe_float(production_record.get("sl_no", 0))
    batch_id = safe_string(production_record.get("batch_id"))
    production_date = format_date(production_record.get("production_date"))
    invoice_number = safe_string(production_record.get("invoice_number"))
    output_qty = clamp_non_negative(production_record.get("output"))

    # 2. Inherited Base Metrics (From Purchase Excel)
    if not purchase_summary:
        input_qty = 0.0
        initial_drc = 0.0
    else:
        # Input quantity is securely bound to the parent purchase's Net Weight
        input_qty = clamp_non_negative(purchase_summary.get("net_weight"))
        # Initial DRC is securely bound to the parent purchase's lab test
        initial_drc = safe_float(purchase_summary.get("calculated_drc_percent"), 0.0)

    # 3. Dynamic Chemistry & Mass Balances
    production_loss = calculate_production_loss(input_qty, output_qty)
    actual_drc = calculate_actual_drc(input_qty, output_qty)
    drc_variance = calculate_drc_variance(actual_drc, initial_drc)

    return {
        "sl_no": int(sl_no),
        "batch_id": batch_id,
        "production_date": production_date,
        "invoice_number": invoice_number,
        "input_quantity": input_qty,
        "output": output_qty,
        "production_loss": production_loss,
        "initial_drc": initial_drc,
        "actual_drc": actual_drc,
        "drc_variance": drc_variance,
    }

def build_production_summaries(production_records, purchase_summaries_dict):
    """
    Batch processor that links a list of raw production records to their 
    corresponding purchase summaries using 'invoice_number' as the primary key.
    """
    summaries = []
    
    for record in (production_records or []):
        invoice_no = safe_string(record.get("invoice_number")).casefold()
        
        # Find the matching purchase dictionary via the lookup dict
        matching_purchase = purchase_summaries_dict.get(invoice_no)
        
        summaries.append(
            build_production_summary(record, matching_purchase)
        )
        
    return summaries