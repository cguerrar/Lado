using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Lado.Services;

namespace Lado.Helpers
{
    public static class LocalizationHelper
    {
        /// <summary>
        /// Obtiene una traducción por su key
        /// </summary>
        public static string T(this IHtmlHelper htmlHelper, string key)
        {
            var localizationService = htmlHelper.ViewContext.HttpContext.RequestServices
                .GetService<ILocalizationService>();

            return localizationService?.Get(key) ?? key;
        }

        /// <summary>
        /// Obtiene una traducción con parámetros
        /// </summary>
        public static string T(this IHtmlHelper htmlHelper, string key, params object[] args)
        {
            var localizationService = htmlHelper.ViewContext.HttpContext.RequestServices
                .GetService<ILocalizationService>();

            return localizationService?.Get(key, args) ?? key;
        }

        /// <summary>
        /// Obtiene una traducción como IHtmlContent (para usar en atributos HTML)
        /// </summary>
        public static IHtmlContent TRaw(this IHtmlHelper htmlHelper, string key)
        {
            return new HtmlString(htmlHelper.T(key));
        }
    }
}
