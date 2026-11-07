using System.Net;
using Bubox.Application.Interfaces;
using Bubox.Domain.Enums;

namespace Bubox.Infrastructure.Services.Email;

/// <summary>Subjek, isi teks, dan isi HTML untuk email OTP (warna brand LUANNA).</summary>
public static class OtpEmailTemplate
{
    private const string BrandOrange = "#EE4D2D";
    private const string BrandNavy = "#113366";
    private const string SoftBackground = "#FFF7F5";
    private const string MutedText = "#5A6B86";

    public static string Subject(OtpEmailMessage message) => message.Type switch
    {
        OtpType.PasswordReset => $"{message.OtpCode} adalah kode reset kata sandi LUANNA kamu",
        _ => $"{message.OtpCode} adalah kode verifikasi LUANNA kamu",
    };

    private static (string Heading, string Intro) Copy(OtpType type) => type switch
    {
        OtpType.PasswordReset => (
            "Reset kata sandi",
            "Kami menerima permintaan untuk mengatur ulang kata sandi akun LUANNA kamu. Masukkan kode berikut di aplikasi:"),
        _ => (
            "Verifikasi email kamu",
            "Terima kasih sudah mendaftar di LUANNA. Masukkan kode berikut di aplikasi untuk mengaktifkan akun kamu:"),
    };

    public static string PlainText(OtpEmailMessage message)
    {
        var (heading, intro) = Copy(message.Type);
        var minutes = (int)message.Validity.TotalMinutes;
        return $"""
            Halo {message.RecipientName},

            {heading}
            {intro}

            {message.OtpCode}

            Kode berlaku {minutes} menit. Jangan bagikan kode ini kepada siapa pun, termasuk pihak yang mengaku dari LUANNA.
            Jika kamu tidak merasa meminta kode ini, abaikan email ini.

            Salam hangat,
            LUANNA
            """;
    }

    public static string Html(OtpEmailMessage message)
    {
        var (heading, intro) = Copy(message.Type);
        var name = WebUtility.HtmlEncode(message.RecipientName);
        var code = WebUtility.HtmlEncode(message.OtpCode);
        var minutes = (int)message.Validity.TotalMinutes;

        return $$"""
            <!doctype html>
            <html lang="id">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{WebUtility.HtmlEncode(heading)}}</title>
            </head>
            <body style="margin:0;padding:0;background:{{SoftBackground}};font-family:Arial,Helvetica,sans-serif;color:{{BrandNavy}};">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:{{SoftBackground}};padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:480px;background:#FFFFFF;border-radius:16px;overflow:hidden;border:1px solid #F2DCD5;">
                      <tr>
                        <td style="background:{{BrandNavy}};padding:20px 28px;">
                          <span style="color:#FFFFFF;font-size:20px;font-weight:bold;letter-spacing:2px;">LUANNA</span>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;">
                          <p style="margin:0 0 6px;font-size:14px;color:{{MutedText}};">Halo {{name}},</p>
                          <h1 style="margin:0 0 12px;font-size:22px;color:{{BrandNavy}};">{{WebUtility.HtmlEncode(heading)}}</h1>
                          <p style="margin:0 0 22px;font-size:14px;line-height:21px;color:{{BrandNavy}};">{{WebUtility.HtmlEncode(intro)}}</p>
                          <div style="background:#FDE8E3;border-radius:12px;padding:18px;text-align:center;">
                            <span style="font-size:34px;font-weight:bold;letter-spacing:10px;color:{{BrandOrange}};font-family:'Courier New',Courier,monospace;">{{code}}</span>
                          </div>
                          <p style="margin:18px 0 0;font-size:13px;line-height:20px;color:{{MutedText}};">
                            Kode berlaku <strong>{{minutes}} menit</strong>. Jangan bagikan kode ini kepada siapa pun, termasuk pihak yang mengaku dari LUANNA.
                          </p>
                          <p style="margin:12px 0 0;font-size:13px;line-height:20px;color:{{MutedText}};">
                            Jika kamu tidak merasa meminta kode ini, abaikan email ini. Akun kamu tetap aman.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:16px 28px;border-top:1px solid #F2DCD5;font-size:12px;color:{{MutedText}};">
                          Email ini dikirim otomatis, mohon tidak dibalas.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
