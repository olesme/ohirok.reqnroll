using Shouldly;
using Ohirok.StepHashtags.ReqnrollPlugin;

namespace XFailNUnitTests.StepDefinitions
{
    public class CustomTestException(string message) : Exception(message)
    {
    }

    [Binding]
    public class XFailSteps(ScenarioContext context, IReqnrollOutputHelper output)
    {
        [Given("step that throws ArgumentException")]
        public static void StepThrowsArgumentException()
        {
            throw new ArgumentException("Test argument exception");
        }

        [Given("step that throws InvalidOperationException")]
        public static void StepThrowsInvalidOperationException()
        {
            throw new InvalidOperationException("Test invalid operation exception");
        }

        [Given("step that throws NullReferenceException")]
        public static void StepThrowsNullReferenceException()
        {
            throw new NullReferenceException("Test null reference exception");
        }

        [Given("step that throws ArgumentNullException")]
        public static void StepThrowsArgumentNullException()
        {
            throw new ArgumentNullException("testParam", "Test argument null exception");
        }

        [Given("step that throws custom exception")]
        public static void StepThrowsCustomException()
        {
            throw new CustomTestException("Test custom exception");
        }

        [Given("step that succeeds")]
        public static void StepThatSucceeds()
        {
            // This step intentionally does nothing and succeeds
        }

        [Then("step verifies exception was handled")]
        public void StepVerifiesExceptionHandled()
        {
            // If we reach this step, the exception was properly handled by XFail
            output.WriteLine("Exception was properly handled by xfail/afail");
        }

        [Then("step confirms success is ok")]
        public void StepConfirmsSuccessOk()
        {
            // This confirms that afail allows both success and failure
            output.WriteLine("AFail correctly allowed step to pass");
        }

        [Then("step confirms normal behavior")]
        public void StepConfirmsNormalBehavior()
        {
            // Step without xfail/afail behaves normally
            output.WriteLine("Step without xfail/afail hashtag behaves normally");
        }

        [Then("this step should never execute")]
        public static void StepShouldNeverExecute()
        {
            throw new ShouldAssertException("This step should never be reached - previous step should have thrown");
        }

        [Then("step confirms all handled correctly")]
        public void StepConfirmsAllHandled()
        {
            output.WriteLine("Multiple xfail/afail steps handled correctly");
        }

        [Given("step throws exception and saves hashtags to context")]
        public void StepThrowsAndSavesHashtags()
        {
            // Save hashtags BEFORE throwing exception
            var hashtags = context.GetStepHashtags();
            context["SavedHashtags"] = hashtags;

            throw new ArgumentException("Test exception");
        }

        [Then("saved hashtags contain:")]
        public void VerifySavedHashtags(Table table)
        {
            var saved = context.Get<string[]>("SavedHashtags");

            foreach (var row in table.Rows)
            {
                var expectedTag = row["hashtag"];
                saved.ShouldContain(expectedTag,
                    $"Hashtag '{expectedTag}' should be present. Found: {string.Join(", ", saved)}");
            }
        }
    }
}
