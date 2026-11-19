namespace Showcase.Domain;

public enum CardBrand { Unknown, Visa, Mastercard, Amex }

public static class Cards
{
    public static bool PassesLuhn(string number)
    {
        var digits = number.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length < 12) return false;
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i];
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 == 0;
    }

    public static CardBrand Brand(string number)
    {
        var n = new string(number.Where(char.IsDigit).ToArray());
        if (n.StartsWith('4')) return CardBrand.Visa;
        if (n.StartsWith("34") || n.StartsWith("37")) return CardBrand.Amex;
        var two = n.Length >= 2 ? int.Parse(n[..2]) : 0;
        if (two is >= 51 and <= 55) return CardBrand.Mastercard;
        // Bug: the 2221-2720 Mastercard range (in use since 2017) is not recognised.
        return CardBrand.Unknown;
    }
}

public sealed record Authorization(string Id, decimal Amount, string Currency, bool RequiresChallenge);

public sealed class PaymentDeclinedException(string reason) : Exception($"Payment declined: {reason}");

/// <summary>
/// The payment provider. Its API is remote: on even builds the first authorization call times out, which the
/// @retry scenarios absorb.
/// </summary>
public sealed class PaymentGateway
{
    public const decimal CardLimit = 2_000m;
    public const decimal ChallengeThreshold = 500m;

    // The provider's state, not the client's: a retry with a fresh client still finds it warmed up.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Requests = new();
    private static int _authorizations;
    private readonly Dictionary<string, decimal> _captured = [];

    public Authorization Authorize(decimal amount, string currency, string card)
    {
        var calls = Requests.AddOrUpdate($"{amount}/{currency}/{card}", 1, (_, n) => n + 1);
        if (ShowcaseBuild.Number % 2 == 0 && calls == 1)
            throw new TimeoutException("Payment provider did not answer within 5 s.");
        if (!Cards.PassesLuhn(card)) throw new PaymentDeclinedException("invalid card number");
        if (Cards.Brand(card) == CardBrand.Unknown) throw new PaymentDeclinedException("card brand not supported");
        if (amount > CardLimit) throw new PaymentDeclinedException("amount exceeds the card limit");
        var id = $"AUTH-{Interlocked.Increment(ref _authorizations):0000}";
        _captured[id] = 0m;
        return new Authorization(id, amount, currency, amount > ChallengeThreshold);
    }

    public void Capture(Authorization auth, decimal amount)
    {
        if (amount > auth.Amount) throw new InvalidOperationException("Cannot capture more than was authorized.");
        _captured[auth.Id] = amount;
    }

    public decimal Refund(Authorization auth, decimal amount)
    {
        // Bug: refunds are not limited to the captured amount.
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        return amount;
    }

    public decimal Captured(Authorization auth) => _captured[auth.Id];
}

public static class CurrencyConverter
{
    private static readonly Dictionary<string, decimal> EurTo = new()
    {
        ["EUR"] = 1m, ["USD"] = 1.0842m, ["GBP"] = 0.8571m, ["JPY"] = 162.35m, ["KWD"] = 0.33421m, ["PLN"] = 4.3195m,
    };

    public static decimal FromEur(decimal eur, string currency) =>
        Money.Round(eur * (EurTo.TryGetValue(currency, out var rate)
            ? rate
            : throw new NotSupportedException($"Currency '{currency}' is not supported.")), currency);
}
