using System.Text.RegularExpressions;

namespace Bubox.Application.Services;

/// <summary>
/// Mencocokkan nama wilayah dari Google Maps ke nama di master wilayah (Kemendagri).
/// Contoh perbedaan yang ditangani: "Kecamatan Wates" ↔ "Wates", "Kab. Sleman" ↔ "Kabupaten Sleman",
/// "Kel. Tegalrejo" ↔ "Tegalrejo", "D.I. Yogyakarta" ↔ "Daerah Istimewa Yogyakarta".
/// </summary>
public static partial class WilayahNameMatcher
{
    [GeneratedRegex(@"^(provinsi|prov\.?|kecamatan|kec\.?|kelurahan|kel\.?|desa|ds\.?|kabupaten|kab\.?|kota)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex AdministrativePrefix();

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlphanumeric();

    /// <summary>"Kabupaten Kulon Progo" → "kulonprogo"; "D.I. Yogyakarta" → "daerahistimewayogyakarta".</summary>
    public static string Normalize(string name)
    {
        var value = name.Trim().ToLowerInvariant();
        value = Regex.Replace(value, @"^(d\.\s?i\.?|di)\s+", "daerah istimewa ");
        value = Regex.Replace(value, @"^dki\s+", "daerah khusus ibukota ");
        value = AdministrativePrefix().Replace(value, string.Empty);
        return NonAlphanumeric().Replace(value, string.Empty);
    }

    /// <summary>Jenis kab/kota dari nama ("kota" / "kabupaten"), null bila tidak disebut.</summary>
    private static string? CityKind(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        if (lower.StartsWith("kota ")) return "kota";
        if (lower.StartsWith("kabupaten ") || lower.StartsWith("kab.") || lower.StartsWith("kab ")) return "kabupaten";
        return null;
    }

    /// <summary>
    /// Pilih kandidat yang paling cocok:
    /// 1) nama persis sama (tanpa beda huruf besar/kecil),
    /// 2) nama ternormalisasi sama (untuk kab/kota, jenisnya juga harus sama bila disebut),
    /// 3) salah satu nama ternormalisasi memuat yang lain, hanya bila hasilnya tunggal.
    /// </summary>
    public static T? FindBest<T>(string? sourceName, IEnumerable<T> candidates, Func<T, string> nameOf) where T : class
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return null;
        }

        var list = candidates.ToList();
        var exact = list.FirstOrDefault(c => string.Equals(nameOf(c).Trim(), sourceName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var target = Normalize(sourceName);
        if (target.Length == 0)
        {
            return null;
        }

        var kind = CityKind(sourceName);
        var sameName = list.Where(c => Normalize(nameOf(c)) == target).ToList();
        if (sameName.Count == 1)
        {
            return sameName[0];
        }
        if (sameName.Count > 1)
        {
            // Mis. "Magelang" ada sebagai Kabupaten dan Kota: pakai jenis yang disebut Google, default kabupaten.
            return sameName.FirstOrDefault(c => CityKind(nameOf(c)) == (kind ?? "kabupaten")) ?? sameName[0];
        }

        // Kecocokan sebagian hanya untuk nama yang cukup panjang agar tidak salah tebak.
        if (target.Length < 4)
        {
            return null;
        }

        var partial = list
            .Where(c =>
            {
                var candidate = Normalize(nameOf(c));
                return candidate.Length >= 4 && (candidate.Contains(target) || target.Contains(candidate));
            })
            .ToList();

        return partial.Count == 1 ? partial[0] : null;
    }
}
