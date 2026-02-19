using System;
using System.ComponentModel.DataAnnotations;

namespace DenoLite.Application.Validation
{
    public class FutureDateAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is null) return true;
            if (value is DateTime dt)
                return dt.ToUniversalTime() > DateTime.UtcNow;

            return false;
        }

        public override string FormatErrorMessage(string name)
            => $"{name} must be a future date.";
    }
}
