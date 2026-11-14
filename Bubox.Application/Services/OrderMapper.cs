using System.Text.Json;
using Bubox.Application.DTOs.Order;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

internal static class OrderMapper
{
    public static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeAddress(AddressSnapshotDto address) =>
        JsonSerializer.Serialize(address, SnapshotJsonOptions);

    public static AddressSnapshotDto? DeserializeAddress(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AddressSnapshotDto>(json, SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeOutlet(OutletSnapshotDto outlet) =>
        JsonSerializer.Serialize(outlet, SnapshotJsonOptions);

    public static OutletSnapshotDto? DeserializeOutlet(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OutletSnapshotDto>(json, SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static DateOnly ToDateOnly(DateTime value) => DateOnly.FromDateTime(value);

    public static DateTime ToDbDate(DateOnly value) => value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    public static OrderSummaryDto ToSummary(OrderListItem o) => new(
        Id: o.Id,
        OrderNumber: o.OrderNumber,
        StatusCode: o.StatusCode,
        FulfillmentType: o.FulfillmentType,
        GrandTotal: o.GrandTotal,
        DaysCount: o.DaysCount,
        FirstDeliveryDate: o.FirstDeliveryDate.HasValue ? ToDateOnly(o.FirstDeliveryDate.Value) : null,
        LastDeliveryDate: o.LastDeliveryDate.HasValue ? ToDateOnly(o.LastDeliveryDate.Value) : null,
        PaymentDueAt: o.PaymentDueAt,
        CreatedAt: o.CreatedAt,
        CustomerName: o.CustomerName,
        CustomerEmail: o.CustomerEmail,
        DeliveryDates: (o.DeliveryDates ?? [])
            .Select(d => DateOnly.ParseExact(d, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            .ToList(),
        ItemLinesCount: o.ItemLinesCount,
        FirstMenuId: o.FirstMenuId,
        FirstMenuName: o.FirstMenuName
    );

    public static OrderItemDto ToDto(OrderItem i) => new(
        Id: i.Id,
        MenuId: i.MenuId,
        MenuName: i.MenuName,
        CategoryCode: i.CategoryCode,
        CategoryName: i.CategoryName,
        UnitPrice: i.UnitPrice,
        Qty: i.Qty,
        LineTotal: i.LineTotal,
        ImageUrl: i.MenuImageUpdatedAt is { } updatedAt
            ? $"{MenuImagePathPrefix}{i.MenuId}?v={updatedAt.ToUniversalTime().Ticks}"
            : null
    );

    /// <summary>Path endpoint gambar menu (MenuController.GetImage).</summary>
    public const string MenuImagePathPrefix = "/api/Menu/Image/";

    public static QrisPaymentDto ToDto(Payment p, bool includeQr) => new(
        QrString: includeQr ? p.QrString : null,
        QrUrl: includeQr ? p.QrUrl : null,
        Acquirer: p.Acquirer,
        TransactionStatus: p.TransactionStatus,
        GrossAmount: p.GrossAmount,
        ExpiresAt: p.ExpiresAt,
        PaidAt: p.PaidAt
    );
}
