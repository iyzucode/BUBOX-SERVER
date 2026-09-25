namespace Bubox.Domain.Constants;

public static class MenuConstants
{
    public static class Types
    {
        public const string Utama = "UTAMA";
        public const string Sekunder = "SEKUNDER";
    }

    public static class Categories
    {
        public const string Bubur = "Bubur";
        public const string NasiTim = "Nasi Tim";
        public const string Sup = "Sup";
        public const string Snack = "Snack";
        public const string Pelengkap = "Pelengkap";
    }

    public static readonly Dictionary<int, string> DayNames = new()
    {
        { 1, "Senin" },
        { 2, "Selasa" },
        { 3, "Rabu" },
        { 4, "Kamis" },
        { 5, "Jumat" },
        { 6, "Sabtu" },
        { 7, "Minggu" }
    };

    public static string GetDayName(int dayOfWeek)
    {
        return DayNames.TryGetValue(dayOfWeek, out var name) ? name : "Senin";
    }
}
