# Backend Development Guide & AI Context
**Path**: `/Server`
**Tech Stack**: .NET 10 (ASP.NET Core Web API), C# 14
**Database**: PostgreSQL
**Data Access**: Dapper (Micro-ORM)
**Architecture**: Clean Architecture

## 1. Core Principles & Vibe Coding Rules
Sebagai AI Assistant, Anda HARUS mematuhi aturan berikut saat meng-generate atau memodifikasi kode di proyek ini:
- **STRICTLY Dapper**: Dilarang menggunakan Entity Framework Core (EF Core). Semua interaksi database harus menggunakan **Dapper** dengan raw SQL queries (selalu gunakan parameterized queries untuk mencegah SQL Injection).
- **Clean Architecture Enforcement**:
  - `Domain` TIDAK BOLEH memiliki dependensi ke layer lain atau framework eksternal.
  - `Application` hanya bergantung pada `Domain`.
  - `Infrastructure` bergantung pada `Application` dan `Domain`. Di sinilah Dapper dan koneksi database diimplementasikan.
  - `Api` (Presentation) bergantung pada `Application` dan `Infrastructure` (hanya untuk Dependency Injection).
- **Modern .NET 10**: Gunakan fitur-fitur modern C# seperti Primary Constructors, Records (untuk DTOs), global usings, dan async/await di setiap operasi I/O.
- **UUID for Identifiers**: Seluruh entitas dan tabel database wajib menggunakan tipe data **UUID** (`Guid` pada C#, `UUID PRIMARY KEY DEFAULT gen_random_uuid()` pada PostgreSQL).
- **Respon Kode**: Berikan kode utuh dan pastikan Dependency Injection selalu di-register di `Program.cs` atau kelas ekstensi (Extension Methods).

## 2. Struktur Direktori Clean Architecture
Proyek ini terdiri dari 4 *class libraries/projects* utama di dalam `/Server`:

/Server
├── /MyApp.Domain          # (Core) Entities, Enums, Exceptions, Repository Interfaces
├── /MyApp.Application     # Use Cases, Services, DTOs, Validation
├── /MyApp.Infrastructure  # Dapper Implementation, Database Connection Factory
├── /MyApp.Api             # Controllers / Minimal APIs, Middleware, Program.cs
├── /SQL                   # Skrip SQL untuk skema database (DDL/DML yang akan/sudah dieksekusi)
└── development_guide.md

### Detail Tiap Layer:
1. **MyApp.Domain**:
   - Berisi representasi *table* database murni (POCO classes).
   - Berisi `Interfaces` untuk kontrak Repository (contoh: `IUserRepository`).
2. **MyApp.Application**:
   - Berisi logika bisnis (Services).
   - Berisi *Data Transfer Objects* (DTO) menggunakan `record`.
   - Menggunakan interface dari Domain untuk mengambil/menyimpan data.
3. **MyApp.Infrastructure**:
   - Implementasi dari Repository interfaces (contoh: `UserRepository : IUserRepository`).
   - Berisi `SqlConnectionFactory` atau `DbConnectionFactory` untuk me-return instance `IDbConnection` yang dibutuhkan oleh Dapper.
4. **MyApp.Api**:
   - *Entry point* aplikasi.
   - Global Exception Handling Middleware.
   - Swagger / OpenAPI setup.

## 3. Standard Patterns & Database Connection

### Database Connection Factory Pattern
Karena menggunakan Dapper, infrastruktur harus menyediakan factory untuk koneksi database. AI harus menggunakan pattern ini:

```csharp
// Di dalam Infrastructure layer
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class DbConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
    {
        // Sesuaikan dengan driver: NpgsqlConnection untuk PostgreSQL atau SqlConnection untuk SQL Server
        return new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
    }
}
```

## 4. SQL Scripts Management (/Server/SQL)
Semua skrip query DDL/DML untuk database PostgreSQL disimpan di dalam folder `/Server/SQL`:
- Dilarang membuat otomatisasi migrasi kode runtime (seperti `DbInitializer`) kecuali secara eksplisit diminta.
- Semua query pembuatan tabel, indeks, modifikasi kolom, atau migrasi skema harus didokumentasikan dalam bentuk file `.sql` di folder `/Server/SQL` (contoh: `001_create_users_table.sql`).
- Folder ini menjadi sumber referensi query yang akan dan sudah dieksekusi di database.

## 5. API Route Naming Conventions
Gunakan penamaan endpoint API yang **deskriptif langsung** (*explicit action-oriented naming*) dan dikelompokkan berdasarkan modul domain/fitur (misal: kelompok `Biodata`):
- Nama endpoint harus secara eksplisit menyatakan aksi yang dilakukan (misal: `GetAddresses`, `GetDetailAddress`, `SaveAddress`, `UpdateAddress`, `SetPrimaryAddress`, `DeleteAddress`).
- **Parameter Query (`[FromQuery]`):** Hindari menuliskan parameter langsung di path nama rute (jangan gunakan template path seperti `{id}` di dalam rute URL). Gunakan query string (`[FromQuery]`) untuk passing identifier (seperti `id`) ataupun parameter filter.
- Contoh standar endpoint pada kelompok Biodata:
  - `GET /api/biodata/GetAddresses` (mengambil daftar alamat pengguna)
  - `GET /api/biodata/GetDetailAddress?id=...` (mengambil detail alamat tertentu via `[FromQuery] Guid id`)
  - `POST /api/biodata/SaveAddress` (menyimpan / menambah alamat baru via `[FromBody]`)
  - `PUT /api/biodata/UpdateAddress?id=...` (memperbarui data alamat via `[FromQuery] Guid id` dan `[FromBody]`)
  - `PUT /api/biodata/SetPrimaryAddress?id=...` (menjadikan alamat sebagai utama via `[FromQuery] Guid id`)
  - `DELETE /api/biodata/DeleteAddress?id=...` (menghapus alamat via `[FromQuery] Guid id`)
- Contoh standar endpoint pada kelompok General (Master Data & Referensi Dropdown):
  - `GET /api/general/GetProvinces` (mengambil daftar seluruh provinsi)
  - `GET /api/general/GetCities?provinceCode=...` (mengambil daftar kota/kabupaten via `[FromQuery] string provinceCode`)
  - `GET /api/general/GetDistricts?cityCode=...` (mengambil daftar kecamatan via `[FromQuery] string cityCode`)
  - `GET /api/general/GetVillages?districtCode=...` (mengambil daftar kelurahan dan kode pos via `[FromQuery] string districtCode`)
- Hindari rute yang ambigu atau sekadar nama koleksi polos tanpa aksi eksplisit.
