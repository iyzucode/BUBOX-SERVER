# Planning Flow Order BUBOX

As of: 2026-11-04 · Status: Draft, menunggu persetujuan implementasi

Order BUBOX memakai **dua state machine berbasis kode string**:
- **Order**: kontrak dan pembayaran (Midtrans).
- **OrderDay**: operasional untuk setiap tanggal kirim.

---

## 1. Keputusan yang sudah disepakati

| Topik | Keputusan |
|---|---|
| Penamaan stage | Kode string (`AWAITING_PAYMENT`, `PAID`, ...), bukan format versi `1.0.0` |
| Payment gateway | Midtrans Core API, hanya QRIS |
| Pembatalan setelah bayar | Tidak boleh oleh customer. Hanya admin untuk force majeure; refund manual di luar sistem |
| Cutoff pemesanan | H-1 pukul 17.00 WIB, disimpan di database dan bisa diubah admin dari dashboard |
| Bayar setelah cutoff | Diterima. Risikonya sudah dipertimbangkan admin saat menentukan jam cutoff |
| Reschedule | Tidak didukung |
| Batas waktu bayar | 1 jam (bisa dikonfigurasi) |
| Kupon | Ditunda (hold). Tidak ada di v1 |
| Delivery failed | Tidak ada. Pengiriman tidak bisa gagal kecuali force majeure (admin cancel), dan tidak bisa dikirim ulang |
| Ongkir | Rp 0 (disimpan sebagai setting agar bisa diubah nanti) |
| Transaksi Midtrans | 1 order = 1 transaksi. Jika gagal bayar, customer membuat order baru |

---

## 2. Status Order

| Kode | Kelompok | Terminal | Keterangan |
|---|---|---|---|
| `AWAITING_PAYMENT` | NORMAL | – | Order dibuat dan QR QRIS sudah diterbitkan |
| `PAID` | NORMAL | – | Midtrans mengirim `settlement` (QRIS terbayar) |
| `IN_PROGRESS` | NORMAL | – | Minimal satu hari sudah masuk `PREPARING` |
| `COMPLETED` | NORMAL | ✓ | Semua hari terminal dan minimal satu hari `DELIVERED` / `PICKED_UP` |
| `CANCELLED_BY_CUSTOMER` | CANCEL | ✓ | Dibatalkan customer selama masih `AWAITING_PAYMENT` |
| `CANCELLED_BY_ADMIN` | CANCEL | ✓ | Dibatalkan admin sebelum bayar, atau semua hari di-cancel karena force majeure |
| `EXPIRED` | CANCEL | ✓ | Melewati `payment_due_at` (dari webhook `expire` atau job expiry) |
| `PAYMENT_FAILED` | CANCEL | ✓ | Midtrans mengirim `deny`, `cancel` atau `failure` |

## 3. Status OrderDay

| Kode | Kelompok | Terminal | Pelaku | Dari |
|---|---|---|---|---|
| `SCHEDULED` | NORMAL | – | Sistem | (awal) |
| `CONFIRMED` | NORMAL | – | Sistem | `SCHEDULED` (ikut Order `PAID`) |
| `PREPARING` | NORMAL | – | Kitchen, Admin | `CONFIRMED` |
| `READY` | NORMAL | – | Kitchen, Admin | `PREPARING` |
| `OUT_FOR_DELIVERY` | NORMAL | – | Driver, Admin | `READY` (khusus DELIVERY) |
| `DELIVERED` | NORMAL | ✓ | Driver, Admin | `OUT_FOR_DELIVERY` |
| `PICKED_UP` | NORMAL | ✓ | Admin | `READY` (khusus PICKUP) |
| `CANCELLED` | CANCEL | ✓ | Sistem | `SCHEDULED` (ikut Order batal / expired / gagal bayar) |
| `CANCELLED_BY_ADMIN` | CANCEL | ✓ | Admin, Super Admin | `CONFIRMED`, `PREPARING`, `READY`, `OUT_FOR_DELIVERY` (force majeure, alasan wajib, refund manual) |

---

## 4. Alur status

```
ORDER
AWAITING_PAYMENT ──bayar──▶ PAID ──hari pertama PREPARING──▶ IN_PROGRESS ──semua hari terminal──▶ COMPLETED
   │                          │                                   │
   ├─ customer batal ─▶ CANCELLED_BY_CUSTOMER                     │
   ├─ admin batal ────▶ CANCELLED_BY_ADMIN ◀── semua hari force majeure (tanpa ada yang terkirim)
   ├─ lewat 1 jam ────▶ EXPIRED
   └─ deny / cancel / failure ──▶ PAYMENT_FAILED

ORDER DAY
SCHEDULED ─▶ CONFIRMED ─▶ PREPARING ─▶ READY ─┬─(DELIVERY)─▶ OUT_FOR_DELIVERY ─▶ DELIVERED
   │                                          └─(PICKUP)───▶ PICKED_UP
   └─ order batal/expired/gagal ─▶ CANCELLED
CONFIRMED | PREPARING | READY | OUT_FOR_DELIVERY ─ force majeure ─▶ CANCELLED_BY_ADMIN
```

---

## 5. Aturan sinkronisasi dan penjagaan transisi

**Sinkronisasi Order ↔ OrderDay**
1. Order `AWAITING_PAYMENT → PAID`: semua hari `SCHEDULED → CONFIRMED`.
2. Hari pertama masuk `PREPARING`: Order `PAID → IN_PROGRESS`.
3. Order masuk `CANCELLED_BY_CUSTOMER` / `CANCELLED_BY_ADMIN` / `EXPIRED` / `PAYMENT_FAILED` sebelum bayar: semua hari `SCHEDULED → CANCELLED`.
4. Semua hari sudah terminal:
   - ada minimal satu `DELIVERED` / `PICKED_UP` → Order `COMPLETED`;
   - semuanya `CANCELLED_BY_ADMIN` → Order `CANCELLED_BY_ADMIN`.
5. Admin membatalkan order yang sudah dibayar = semua hari yang belum terminal menjadi `CANCELLED_BY_ADMIN`, lalu aturan 4 berlaku.

**Penjagaan**
- Transisi yang diizinkan didefinisikan di `rf_order_status_transitions (scope, from_code, to_code, allowed_roles)`. Semua perubahan wajib lewat `OrderStatusEngine`.
- Optimistic update: `UPDATE ... SET status_code = @To WHERE id = @Id AND status_code = @From`. Jika 0 baris berubah, transisi dianggap konflik.
- Transisi Order dan OrderDay yang saling terkait dijalankan dalam satu transaksi database.
- Setiap transisi dicatat di `order_status_histories`.
- Cancel force majeure: `reason` wajib diisi, dan `requires_manual_refund = true` di-set pada hari tersebut.

---

## 6. Cutoff dan pengaturan (`app_settings`)

| Key | Tipe | Default | Keterangan |
|---|---|---|---|
| `ORDER_CUTOFF_DAYS_BEFORE` | int | `1` | H-N |
| `ORDER_CUTOFF_TIME` | time `HH:mm` | `17:00` | Jam cutoff |
| `ORDER_TIMEZONE` | string | `Asia/Jakarta` | Zona waktu acuan |
| `PAYMENT_TIMEOUT_MINUTES` | int | `60` | Batas waktu bayar |
| `DELIVERY_FEE` | decimal | `0` | Ongkir per order |

- Validasi saat `CreateOrder`: setiap `delivery_date` harus memenuhi `now(WIB) < (delivery_date − N hari) + ORDER_CUTOFF_TIME`. Jika ada satu tanggal yang melanggar, order ditolak dan pesannya menyebut tanggal tersebut.
- `payment_due_at = created_at + PAYMENT_TIMEOUT_MINUTES`. Tidak dibatasi oleh cutoff, sesuai keputusan.
- Hanya Admin / Super Admin yang bisa mengubah setting. `updated_by` dan `updated_at` dicatat. Nilai divalidasi (format jam, rentang angka).

---

## 7. Integrasi Midtrans (QRIS)

Pembayaran **hanya lewat QRIS** memakai Midtrans Core API. QR ditampilkan langsung di aplikasi, tanpa halaman Snap.

1. **CreateOrder**
   - Server menghitung ulang harga dari database.
   - Menyimpan `orders`, `order_days`, dan `order_items` dalam satu transaksi.
   - Memanggil `POST /v2/charge` dengan `payment_type: qris` (`order_id = order_number`, `gross_amount` dalam rupiah bulat, `custom_expiry = PAYMENT_TIMEOUT_MINUTES`).
   - Menyimpan `qr_string`, URL gambar QR (`actions` generate-qr-code), dan `acquirer` ke `payments`, lalu mengembalikannya ke client bersama `payment_due_at`.
   - **Client** menampilkan QR beserta hitung mundur, lalu polling `GetDetailOrder` tiap 5 detik sampai status berubah. Di web, QR dipindai dari ponsel; di aplikasi mobile tersedia tombol simpan gambar QR.
2. **Webhook** `POST /api/Payment/MidtransNotification` (`AllowAnonymous`)
   - Payload mentah disimpan ke `payment_notifications`.
   - Verifikasi `signature_key = SHA512(order_id + status_code + gross_amount + ServerKey)`.
   - Konfirmasi ulang lewat Status API (`GET /v2/{order_id}/status`).
   - Idempoten: notifikasi berulang tidak boleh menimbulkan efek ganda.
3. **Pemetaan status**

   | Midtrans | Order |
   |---|---|
   | `settlement` | `PAID` |
   | `pending` | tidak berubah |
   | `deny`, `cancel`, `failure` | `PAYMENT_FAILED` |
   | `expire` | `EXPIRED` |
4. **Pengaman**: `settlement` yang datang untuk order yang sudah terminal tidak mengubah status. Pembayaran ditandai `needs_review = true` dan admin melakukan refund manual.
5. **Job expiry**: `HostedService` berjalan tiap 1 menit dan mengubah `AWAITING_PAYMENT` yang lewat `payment_due_at` menjadi `EXPIRED`. Ini cadangan kalau webhook tidak sampai.
6. **Konfigurasi** (`configuration.yml` / environment variable): `Midtrans:ServerKey`, `Midtrans:IsProduction`. QRIS harus sudah aktif di akun Midtrans (Sandbox dan Production).
7. **Perlu dicek ke Midtrans**: batas nominal maksimum per transaksi QRIS untuk akun BUBOX, karena order beberapa hari bisa bernilai besar.

---

## 8. Struktur tabel

| Tabel | Kolom utama |
|---|---|
| `app_settings` | key PK, value, value_type, description, updated_by, updated_at |
| `rf_order_statuses` | code PK, scope (`ORDER`/`DAY`), status_group (`NORMAL`/`CANCEL`), sort_order, name, is_terminal |
| `rf_order_status_transitions` | id, scope, from_code, to_code, allowed_roles TEXT[] |
| `orders` | id, order_number (`BBX-YYYYMMDD-XXXX`), user_id, status_code, fulfillment_type (`DELIVERY`/`PICKUP`), address_snapshot JSONB, subtotal, discount_amount (0, untuk kupon nanti), delivery_fee, grand_total, payment_due_at, paid_at, completed_at, cancelled_at, cancel_reason, created_at, updated_at |
| `order_days` | id, order_id, delivery_date, day_of_week, status_code, day_subtotal, requires_manual_refund, cancel_reason, cancelled_by, cancelled_at, updated_at · UNIQUE(order_id, delivery_date) |
| `order_items` | id, order_day_id, menu_id, menu_name, category_code, category_name, unit_price, qty, line_total |
| `payments` | id, order_id, provider, provider_order_id, qr_string, qr_url, acquirer, transaction_status, payment_type (selalu `qris`), gross_amount, paid_at, needs_review, created_at, updated_at |
| `payment_notifications` | id, payment_id, raw_payload JSONB, signature_valid, received_at |
| `order_status_histories` | id, scope, entity_id, from_code, to_code, actor_user_id, actor_role, reason, created_at |

Validasi item saat `CreateOrder`:
- Menu harus aktif, tidak terhapus, dan di-assign ke `day_of_week` dari tanggal tersebut.
- qty antara 1 dan 20.
- Order DELIVERY wajib memakai alamat milik user tersebut; alamatnya disimpan sebagai snapshot.

---

## 9. Endpoint API

| Grup | Endpoint | Role |
|---|---|---|
| Order | `POST /api/Order/CreateOrder` | Customer |
| Order | `GET /api/Order/GetMyOrders` | Customer |
| Order | `GET /api/Order/GetDetailOrder?id=` | Customer (miliknya), Admin |
| Order | `PUT /api/Order/CancelOrder?id=` | Customer (hanya `AWAITING_PAYMENT`) |
| Payment | `POST /api/Payment/MidtransNotification` | Anonymous + signature |
| Admin | `GET /api/OrderAdmin/GetOrders` (filter status, tanggal) | Admin, Super Admin, Owner |
| Admin | `GET /api/OrderAdmin/GetDailyProduction?date=` | Admin, Kitchen |
| Admin | `GET /api/OrderAdmin/GetDailyDeliveries?date=` | Admin, Driver |
| Admin | `PUT /api/OrderAdmin/UpdateDayStatus?id=` | Kitchen, Driver, Admin (sesuai tabel transisi) |
| Admin | `PUT /api/OrderAdmin/CancelOrderByAdmin?id=` | Admin, Super Admin |
| Admin | `PUT /api/OrderAdmin/CancelDayByAdmin?id=` | Admin, Super Admin |
| Settings | `GET /api/Settings/GetOrderSettings` | Admin, Super Admin |
| Settings | `PUT /api/Settings/UpdateOrderSettings` | Admin, Super Admin |
| Settings | `GET /api/Settings/GetOrderCutoffInfo` | Publik (dipakai client untuk menonaktifkan tanggal di kalender) |

---

## 10. Tahap implementasi

- [x] Migrasi `SQL/SPRINT-3/011_create_app_settings.sql` (beserta seed)
- [x] Migrasi `SQL/SPRINT-3/012_create_order_tables.sql` (seed status dan transisi)
- [x] Domain: entity, konstanta status, interface repository
- [x] Infrastructure: repository Dapper (dengan transaksi), `MidtransPaymentGateway` (HttpClient)
- [x] Application: `OrderService`, `OrderStatusEngine`, `SettingsService`, `PaymentService`
- [x] Api: controller, mapping exception di middleware, `OrderExpiryHostedService`, konfigurasi Midtrans
- [ ] Uji dengan Midtrans Sandbox asli (sudah lolos 53 skenario E2E dengan mock Midtrans)
- [ ] Client: checkout memanggil `CreateOrder` lalu menampilkan QR dan polling status, riwayat order, dashboard admin order, dan halaman setting cutoff

---

## 11. Di-hold / di luar sistem

- **Kupon**: ditunda. Kolom `discount_amount` tetap ada dengan nilai 0.
- **Refund**: dilakukan manual di luar sistem (dashboard Midtrans / transfer). Sistem hanya menandai `requires_manual_refund` dan `needs_review`.
- **Reschedule** dan **pengiriman ulang**: tidak didukung.
- **Ongkir dinamis**: tidak ada di v1. Nilainya tetap dari setting `DELIVERY_FEE = 0`.
