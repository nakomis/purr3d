// Where a project's icon lives. Purr gives a path such as "/icons/HOME.png", served from the same site as the API
// but from its root, not under /api.

using System;

namespace Purr3D.Core
{
    public static class IconUrl
    {
        /// <summary>
        /// The full URL of <paramref name="icon"/>: a full http(s) URL as it is, a path against the site root of
        /// <paramref name="apiBaseUrl"/>. Null when there's no icon or no usable base URL.
        /// </summary>
        public static string Resolve(string apiBaseUrl, string icon)
        {
            if (string.IsNullOrWhiteSpace(icon)) return null;
            icon = icon.Trim();

            if (Uri.TryCreate(icon, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
                return absolute.ToString();

            if (string.IsNullOrWhiteSpace(apiBaseUrl) ||
                !Uri.TryCreate(apiBaseUrl.Trim(), UriKind.Absolute, out var api) ||
                (api.Scheme != Uri.UriSchemeHttps && api.Scheme != Uri.UriSchemeHttp))
                return null;

            var root = api.GetLeftPart(UriPartial.Authority);
            return root + (icon.StartsWith("/", StringComparison.Ordinal) ? icon : "/" + icon);
        }
    }
}
