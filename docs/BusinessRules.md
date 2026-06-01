# RubexOps Business Rules

## Item

Item Name:
Natural Rubber Field Coagulum

Item Code:
NRFC

## Contract IDs

Format:

VendorID-R0

Examples:

743268-R0
743268-R1
743268-R2

## Quantity Tracking

Contract completion is based on:

NET WEIGHT

Not DRC Weight.

## Remaining Quantity

Remaining Qty =
Agreed Qty - Qty Delivered So Far

Never negative.

## Days Remaining

Days Remaining =
End Date - Today

Never negative.

Expired contracts display 0.

## Breach Detection

Triggered when:

End Date Passed

AND

Remaining Qty > 0

## Purchase Validation

Carrier Weight cannot exceed Before Unloading.

Received Weight must be positive.

Net Weight must be positive.

Net Weight cannot exceed remaining contract quantity.

Negative values are prohibited.

DRC must be between 0 and 100.

GST must be between 0 and 100.

TDS must be between 0 and 100.

## Renewal Rules

New Contract:
VendorID-R0

First Renewal:
VendorID-R1

Second Renewal:
VendorID-R2

Vendor ID remains constant.