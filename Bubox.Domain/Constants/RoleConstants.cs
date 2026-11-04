namespace Bubox.Domain.Constants;

public static class RoleConstants
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string Driver = "Driver";
    public const string Kitchen = "Kitchen";
    public const string Owner = "Owner";
    public const string SuperAdmin = "Super Admin";

    public static class Codes
    {
        public const string Customer = "CUSTOMER";
        public const string Admin = "ADMIN";
        public const string Driver = "DRIVER";
        public const string Kitchen = "KITCHEN";
        public const string Owner = "OWNER";
        public const string SuperAdmin = "SUPER_ADMIN";
    }

    private static readonly Dictionary<string, string> NameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        { Customer, Codes.Customer },
        { Admin, Codes.Admin },
        { Driver, Codes.Driver },
        { Kitchen, Codes.Kitchen },
        { Owner, Codes.Owner },
        { SuperAdmin, Codes.SuperAdmin }
    };

    /// <summary>Mengubah nama role (yang ada di klaim JWT) menjadi kode role (rf_roles.code).</summary>
    public static string? ToCode(string roleName) =>
        NameToCode.TryGetValue(roleName.Trim(), out var code) ? code : null;
}
