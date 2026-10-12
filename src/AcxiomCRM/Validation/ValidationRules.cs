namespace AcxiomCRM.Validation;

/// <summary>Shared validation patterns and messages (section 5.4 of the specification).</summary>
public static class ValidationRules
{
    /// <summary>Project phone rule: 10-digit Indian mobile number starting with 6-9.</summary>
    public const string PhonePattern = @"^[6-9][0-9]{9}$";

    /// <summary>Stricter than [EmailAddress]: requires a dot in the domain part and no spaces.</summary>
    public const string EmailPattern = @"^[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";

    public const string PhoneMessage = "Enter a valid phone number.";
    public const string EmailMessage = "Enter a valid email address.";
    public const string AmountMessage = "Opportunity Amount must be greater than 0.";
    public const string ProbabilityMessage = "Probability must be between 0 and 100.";
    public const string CloseDateMessage = "Expected Close Date cannot be in the past.";
    public const string FollowUpDateMessage = "Follow-up date cannot be earlier than today.";

    public const string MaxMoney = "999999999999";
    public const decimal MaxMoneyValue = 999_999_999_999m;

    public const int NameLength = 100;
    public const int CompanyLength = 150;
    public const int EmailLength = 256;
    public const int NotesLength = 1000;
}
