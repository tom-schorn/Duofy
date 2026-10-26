using QRCoder;

namespace Duofy.Shared.Services;

/// <summary>
/// Der QR-Code eines Einladungslinks als SVG (QRCoder 1.8.0, MIT). Schwarz auf Weiß, auch im dunklen
/// Modus, damit jede Kamera ihn lesen kann. Gleicher Link ergibt immer dasselbe Bild.
/// </summary>
public static class InvitationQr
{
    public static string Svg(string link)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(link, QRCodeGenerator.ECCLevel.M);
        // Ohne Zeilenumbrüche: QRCoder bricht je nach Betriebssystem anders um, das Bild bleibt dasselbe.
        return new SvgQRCode(data)
            .GetGraphic(8, "#000000", "#ffffff", drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute)
            .ReplaceLineEndings("");
    }
}
