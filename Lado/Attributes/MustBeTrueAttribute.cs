using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Lado.Attributes
{
    /// <summary>
    /// Atributo de validación que requiere que un valor booleano sea true.
    /// Útil para checkboxes de términos y condiciones.
    /// Incluye soporte para validación del lado del cliente.
    /// </summary>
    public class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
    {
        public MustBeTrueAttribute() : base("Este campo debe ser aceptado")
        {
        }

        public override bool IsValid(object? value)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }
            return false;
        }

        public void AddValidation(ClientModelValidationContext context)
        {
            // Agregar atributos para validación del lado del cliente
            context.Attributes.TryAdd("data-val", "true");
            context.Attributes.TryAdd("data-val-mustbetrue", ErrorMessage ?? "Este campo debe ser aceptado");
        }
    }
}
