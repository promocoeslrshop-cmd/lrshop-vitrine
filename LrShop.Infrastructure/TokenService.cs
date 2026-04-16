using System.Text;

namespace LrShop.Infrastructure;

public class TokenService
{
    public string GerarToken(string email)
    {
        var raw = $"{email}|{DateTime.UtcNow:O}|lrshop";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }
}