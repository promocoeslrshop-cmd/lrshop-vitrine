using System.Diagnostics;

namespace LrShop.Infrastructure;

public static class LinktreeBrowserService
{
    public static void AbrirPainel()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://linktr.ee/admin/links",
            UseShellExecute = true
        });
    }
}