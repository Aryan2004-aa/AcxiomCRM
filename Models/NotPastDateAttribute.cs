using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AcxiomCRM.Models;

// Date must not be earlier than today. Optionally exempt when another property holds one of ExemptValues
// (e.g. a Won/Lost opportunity). Works on the server AND on the client (see wwwroot/js/site.js "notpast").
[AttributeUsage(AttributeTargets.Property)]
public class NotPastDateAttribute : ValidationAttribute, IClientModelValidator
{
    public string DependsOn { get; set; }
    public string ExemptValues { get; set; }

    public NotPastDateAttribute(string message) { ErrorMessage = message; }

    protected override ValidationResult IsValid(object value, ValidationContext ctx)
    {
        if (value is DateTime d)
        {
            if (!string.IsNullOrEmpty(DependsOn))
            {
                var other = ctx.ObjectType.GetProperty(DependsOn)?.GetValue(ctx.ObjectInstance)?.ToString();
                if (other != null && (ExemptValues ?? "").Split(',').Contains(other)) return ValidationResult.Success;
            }
            if (d.Date < DateTime.Today) return new ValidationResult(ErrorMessage);
        }
        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext c)
    {
        c.Attributes.TryAdd("data-val", "true");
        c.Attributes.TryAdd("data-val-notpast", ErrorMessage);
        if (!string.IsNullOrEmpty(DependsOn))
        {
            c.Attributes.TryAdd("data-val-notpast-dependson", DependsOn);
            c.Attributes.TryAdd("data-val-notpast-exempt", ExemptValues ?? "");
        }
    }
}
