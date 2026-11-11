#if ANDROID
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.Views;
using Android.Webkit;

namespace Duofy.Services;

/// <summary>
/// Erzeugt das PDF aus der Ansicht der App. Android kann eine WebView nicht ohne den Druckdialog des Systems als PDF
/// ablegen; deshalb schaltet die Seite kurz die Druck-Stilregeln (<c>@media print</c>) auf den Bildschirm, die ganze
/// Seite wird auf A4-Blätter gezeichnet, danach gelten die Regeln wieder nur fürs Drucken. Das Druckbild ist dasselbe
/// wie im Browser. Voraussetzung: <see cref="Prepare"/> lief vor der ersten WebView.
/// </summary>
public sealed class AndroidPrintViewRenderer : IPrintViewRenderer
{
    // A4 in PostScript-Punkten.
    private const int PageWidth = 595;
    private const int PageHeight = 842;
    private const int Margin = 28;

    private const string PrintOn = "(()=>{for(const s of document.styleSheets){for(const r of s.cssRules){if(r.type===CSSRule.MEDIA_RULE&&r.media.mediaText==='print'){r.media.mediaText='all';r.__duofy=1}}}return 1})()";
    private const string PrintOff = "(()=>{for(const s of document.styleSheets){for(const r of s.cssRules){if(r.__duofy){r.media.mediaText='print';delete r.__duofy}}}return 1})()";

    /// <summary>Muss vor dem Anlegen der ersten WebView laufen, sonst zeichnet sie nur den sichtbaren Ausschnitt.</summary>
    public static void Prepare() => Android.Webkit.WebView.EnableSlowWholeDocumentDraw();

    public async Task<string> RenderPdfAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var webView = FindWebView()
            ?? throw new InvalidOperationException("Die Ansicht der App wurde nicht gefunden; das PDF kann nicht erzeugt werden.");
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);

        await RunAsync(webView, PrintOn);
        try
        {
            // Die Ansicht braucht einen Takt, um mit den Druckregeln neu zu setzen.
            await Task.Delay(300, cancellationToken);
            await MainThread.InvokeOnMainThreadAsync(() => WritePdf(webView, path));
        }
        finally
        {
            await RunAsync(webView, PrintOff);
        }
        return path;
    }

    private static Android.Webkit.WebView? FindWebView()
    {
        var root = Platform.CurrentActivity?.Window?.DecorView as ViewGroup;
        return root is null ? null : Find(root);

        static Android.Webkit.WebView? Find(ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                switch (group.GetChildAt(i))
                {
                    case Android.Webkit.WebView web:
                        return web;
                    case ViewGroup inner when Find(inner) is { } found:
                        return found;
                }
            }
            return null;
        }
    }

    private static Task RunAsync(Android.Webkit.WebView webView, string script)
    {
        var done = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(() => webView.EvaluateJavascript(script, new Done(done)));
        return done.Task;
    }

    private sealed class Done(TaskCompletionSource source) : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => source.TrySetResult();
    }

    private static void WritePdf(Android.Webkit.WebView webView, string path)
    {
        var width = Math.Max(webView.Width, 1);
        var height = Math.Max((int)(webView.ContentHeight * webView.ScaleY * webView.Resources!.DisplayMetrics!.Density), webView.Height);
        var scale = (PageWidth - 2f * Margin) / width;
        var pageContent = (int)((PageHeight - 2f * Margin) / scale);

        using var document = new PdfDocument();
        for (int top = 0, number = 1; top < height; top += pageContent, number++)
        {
            var page = document.StartPage(new PdfDocument.PageInfo.Builder(PageWidth, PageHeight, number).Create())
                ?? throw new InvalidOperationException("Android hat keine PDF-Seite angelegt.");
            var canvas = page.Canvas
                ?? throw new InvalidOperationException("Die PDF-Seite hat keine Zeichenfläche.");
            canvas.Translate(Margin, Margin);
            canvas.Scale(scale, scale);
            canvas.ClipRect(0, 0, width, pageContent);
            canvas.Translate(0, -top);
            webView.Draw(canvas);
            document.FinishPage(page);
        }

        using var stream = File.Create(path);
        document.WriteTo(stream);
    }
}
#endif
