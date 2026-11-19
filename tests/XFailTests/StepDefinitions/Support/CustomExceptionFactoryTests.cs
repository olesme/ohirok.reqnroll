using Ohirok.XFail.ReqnrollPlugin;

namespace XFailNUnitTests.Support
{
    // Custom exception types for testing
    public class CustomUnexpectedPassException(string message) : Exception(message)
    {
    }

    public class CustomWrongException(string message, Exception inner) : Exception(message, inner)
    {
    }

    [Binding]
    public static class CustomExceptionFactoryTests
    {
        [BeforeTestRun]
        public static void SetupCustomExceptionFactory()
        {
            // Example of how to configure custom exception types
            // This would typically be done in application startup
            XFailOptions.UseExceptionTypes<CustomUnexpectedPassException, CustomWrongException>();
        }

        [AfterTestRun]
        public static void RestoreDefaultExceptionFactory()
        {
            // Restore default factory after tests
            XFailOptions.ExceptionFactory = new DefaultXFailExceptionFactory();
        }
    }
}
