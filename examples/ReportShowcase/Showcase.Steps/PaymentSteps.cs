using Reqnroll;
using Shouldly;
using Showcase.Domain;

namespace Showcase.Steps;

public sealed class PaymentContext
{
    public PaymentGateway Gateway { get; } = new();
    public Authorization? Authorization { get; set; }
    public Exception? Error { get; set; }
    public decimal Refunded { get; set; }
}

[Binding]
public sealed class PaymentSteps(PaymentContext payment)
{
    [Then("card {string} is {word}")]
    public void CardValidity(string number, string validity) =>
        Cards.PassesLuhn(number).ShouldBe(validity == "valid", $"Luhn check of {number}");

    [Then("card {string} is recognised as {word}")]
    public void CardBrandIs(string number, string brand) => Cards.Brand(number).ToString().ShouldBe(brand);

    [When("{decimal} {word} is authorized on card {string}")]
    public void Authorize(decimal amount, string currency, string card) =>
        payment.Authorization = payment.Gateway.Authorize(amount, currency, card);

    [When("{decimal} {word} is attempted on card {string}")]
    public void Attempt(decimal amount, string currency, string card)
    {
        try { payment.Authorization = payment.Gateway.Authorize(amount, currency, card); }
        catch (PaymentDeclinedException ex) { payment.Error = ex; }
    }

    [Then("the payment is authorized")]
    public void Authorized() => payment.Authorization.ShouldNotBeNull();

    [Then("the payment is declined because {string}")]
    public void Declined(string reason) => payment.Error.ShouldNotBeNull().Message.ShouldContain(reason);

    [Then("the customer is asked to confirm the payment in the banking app")]
    public void Challenge() => payment.Authorization.ShouldNotBeNull().RequiresChallenge.ShouldBeTrue();

    [When("the customer confirms the payment in the banking app")]
    public void ConfirmChallenge() => throw new PendingStepException("3-D Secure confirmation is not automated yet.");

    [When("{decimal} EUR is captured")]
    public void Capture(decimal amount) => payment.Gateway.Capture(payment.Authorization!, amount);

    [Then("{decimal} EUR has been captured")]
    public void Captured(decimal expected) => payment.Gateway.Captured(payment.Authorization!).ShouldBe(expected);

    [When("{decimal} EUR is refunded")]
    public void Refund(decimal amount) => payment.Refunded = payment.Gateway.Refund(payment.Authorization!, amount);

    [Then("a refund of {decimal} EUR is refused")]
    public void RefundRefused(decimal amount) =>
        Should.Throw<InvalidOperationException>(() => payment.Gateway.Refund(payment.Authorization!, amount));

    [Then("{decimal} EUR is {decimal} {word}")]
    public void Converted(decimal eur, decimal expected, string currency) => CurrencyConverter.FromEur(eur, currency).ShouldBe(expected);
}
