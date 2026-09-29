using System.Collections.Generic;

namespace empifisJsonAPI2
{
    /// <summary>
    /// Descriptions from the manual's "Error Codes" table, used as the ErrorMessage when the fiscal
    /// device or the service returns an error code without a message of its own.
    /// </summary>
    public static partial class ErrorCodes
    {
        private static readonly Dictionary<int, string> Descriptions = new()
        {
            [16] = "ERR_ILLEGAL: An unknown or unsupported command.",
            [17] = "ERR_IDLE_STATE: The function cannot be executed in the IDLE state.",
            [18] = "ERR_NONFIS_STATE: The function cannot be executed in the NONFIS state.",
            [19] = "ERR_FIS_STATE: The function cannot be executed in the FIS state.",
            [21] = "ERR_PARAMETERS: Invalid parameters in the EmpiFis package: insufficient quantity of the parameters for the command or the types of the parameters contain invalid values.",
            [23] = "ERR_ITEM_QUANTITY: Invalid quantity value. The value cannot be negative.",
            [24] = "ERR_ITEM_PRICE: Invalid price value. The value cannot be negative.",
            [25] = "ERR_VAT: Invalid VAT number. Supported numbers are from 0 to 5. The VAT number 3 value cannot be changed and equals zero.",
            [27] = "ERR_DEFICIENT_PAYMENT: Insufficient payment sums for the receipt.",
            [28] = "ERR_OVERPAYMENT_CREDIT: Payment by credit sums exceeds the receipt total sum.",
            [29] = "ERR_ITEM_DISCOUNT: Invalid discount/surcharge for the item (e.g. percent discount exceeds 100%, fixed discount exceeds the item price).",
            [30] = "ERR_DISCOUNT_TYPE: Invalid discount/surcharge type.",
            [34] = "ERR_PRICE_AMOUNT_OVERFLOW: The item price times quantity cannot exceed 99999.99.",
            [37] = "ERR_BAD_DATE: Invalid date format.",
            [40] = "ERR_DISCOUNT_RECEIPT: Invalid discount/surcharge for the receipt (e.g. percent discount exceeds 100%, fixed discount exceeds the receipt total, no items on the receipt).",
            [41] = "ERR_NO_ITEMS: No items in the current receipt.",
            [42] = "ERR_CANT_RETURN: Impossible to return an item (item void).",
            [49] = "ERR_PAYMENT_NOT_EQUAL: The payment sum of the return receipt is not equal to the receipt total sum.",
            [50] = "ERR_DEFICIENT_CASH_DRAWER: Not enough cash in the drawer.",
            [68] = "ERR_CURRENCY_NUMBER: Invalid currency identifier.",
            [69] = "ERR_CURRENCY_RATE: Invalid currency rate.",
            [70] = "ERR_CURRENCY_NOT_SET: Currency is not set or forbidden.",
            [71] = "ERR_VAT_NUMBER: Invalid VAT number.",
            [73] = "ERR_TARE_QUANTITY: Invalid quantity of the tare.",
            [163] = "ERR_BARCODE_LENGTH: Invalid barcode length for the selected barcode system.",
            [164] = "ERR_BARCODE_SYSTEM: Unsupported barcode system.",
            [165] = "ERR_BARCODE_CHAR: Unsupported character in the barcode.",
            [166] = "ERR_BARCODE_HEIGHT: Barcode height is out of the supported range.",
            [171] = "ERR_INVALID_INTERVAL: Invalid range of dates or numbers.",
            [190] = "ECR_HANSAB_TIMEOUT: No connection with the card terminal (ECR).",
            [191] = "ECR_HANSAB_ERROR: Error sending the package to the card terminal (ECR).",
            [192] = "ECR_HANSAB_STATUSCODE: Error receiving the package from the card terminal (ECR) (HTTP status is not 200).",
            [193] = "ECR_HANSAB_RETURNCODE: The card terminal (ECR) returned an error (ReturnCode is not 0).",
            [194] = "ECR_HANSAB_EXCEPTION: Other card terminal (ECR) hardware or software error.",
            [400] = "ERR_DEPOSIT_IN_FISCAL_REFUND: Deposit in refund receipt.",
            [401] = "ERR_DEPOSIT_IN_FISCAL_PAYMENT: Deposit cannot be fully covered in credit or cash.",
            [402] = "ERR_MISSING_REFUND_RECEIPT_INFO: Refund receipt information is missing.",
            [404] = "ERR_INVALID_PRERECEIPT: The specified receipt is not a prepayment invoice or has been transferred.",
            [500] = "ERR_FAILURE: Unknown, general error.",
            [501] = "ERR_LOCK: The command cannot be executed until the current command is completed.",
            [502] = "ERR_OFFLINE_Z: The limit of Z reports in the SM.",
            [503] = "ERR_OFFLINE_RECEIPTS: The limit of receipts in the SM.",
            [504] = "ERR_OFFLINE_FISCAL_RECEIPTS: The limit of fiscal receipts in the SM.",
            [505] = "ERR_OFFLINE_NONFISCAL_RECEIPTS: The limit of non-fiscal receipts in the SM.",
            [506] = "ERR_SIGN_STATE: Command cannot be executed in the state of signing.",
            [507] = "ERR_PAYMENT_STATE: Command cannot be executed in the state of receipt payment.",
            [511] = "ERR_HARDWARE_SM: No connected security module.",
            [512] = "ERR_HARDWARE_PRINTER: No connected printer.",
            [513] = "ERR_INVALID_SM_STATE: Invalid SM status.",
            [514] = "ERR_LAST_SM_TIME: The time of the receipt to sign is equal to or less than the time of the last signed SM receipt.",
            [520] = "ERR_SEND_TO_SM: Error sending to the SM module.",
            [521] = "ERR_SEND_TO_PRINTER: Error sending to the printer.",
            [522] = "ERR_SEND_TO_DISPLAY: Error sending to the customer display.",
            [530] = "ERR_SOFTWARE_JOURNAL: The directory of the electronic journal is not accessible.",
            [532] = "ERR_SOFTWARE_MODULES: EmpiFis cannot load required libraries.",
            [555] = "The fiscal device did not respond in time; EmpiFis was reloaded. The receipt may still have been printed - check the device before resending.",
            [556] = "The fiscal device did not respond in time and EmpiFis could not be reloaded. The receipt may still have been printed - check the device before resending.",
            [557] = "Receipt processing was interrupted by a service restart. The receipt may or may not have been printed - check the fiscal device before resending.",
            [998] = "This command is not supported by the installed EmpiFisX.dll. Update EmpiFisX on this computer.",
            [999] = "Invalid parameters in empifisJsonAPI.",
        };

        public static string Describe(int errorCode)
        {
            if (Descriptions.TryGetValue(errorCode, out var description)) return description;
            // Worldline card terminal (ECR) codes, 10001-15002 (ErrorCodes.Ecr.cs).
            if (EcrDescriptions.TryGetValue(errorCode, out var ecrDescription)) return $"Card terminal (ECR) error {ecrDescription}";
            if (errorCode >= 10000 && errorCode < 16000) return $"Card terminal (ECR) error {errorCode}. See the ECR error codes in the manual.";
            return $"Error {errorCode}.";
        }
    }
}
