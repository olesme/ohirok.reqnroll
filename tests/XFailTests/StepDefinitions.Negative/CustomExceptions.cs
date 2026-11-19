using Ohirok.XFail.ReqnrollPlugin;

namespace XFailNUnitTests.Negative.StepDefinitions
{
    public class NegCustomUnexpectedPassException(string message) : Exception(message) { }
    public class NegCustomWrongException(string message, Exception inner) : Exception(message, inner) { }

    [Binding]
    public static class CustomExceptionHooks
    {
        [BeforeScenario("custom-exceptions")]
        public static void Use() => XFailOptions.UseExceptionTypes<NegCustomUnexpectedPassException, NegCustomWrongException>();

        [AfterScenario("custom-exceptions", Order = int.MaxValue)]
        public static void Restore() => XFailOptions.ExceptionFactory = new DefaultXFailExceptionFactory();
    }
}
