using NUnit.Framework;
using Ohirok.PersistentCleanup;
using Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin;
using PersistentCleanupCustomStorageExample.Bindings;
using PersistentCleanupCustomStorageExample.Support;

namespace PersistentCleanupCustomStorageExample.StepDefinitions
{
    [Binding]
    public class CleanupSteps(ICleanupService cleanupService)
    {
        private InMemoryCleanupStorage? _freshStorage;

        [Given("a cleanup action {string} is registered")]
        public async Task GivenCleanupActionIsRegistered(string key)
        {
            await cleanupService.AddAsync<CleanupActions>(x => x.DeleteTempUser(key));
        }

        [When("all pending cleanups are performed")]
        public async Task WhenAllPendingCleanupsArePerformed()
        {
            await cleanupService.PerformAllAsync();
        }

        [When("all pending cleanups are performed against a freshly created in-memory storage directly")]
        public async Task WhenPerformedAgainstFreshlyCreatedStorageDirectly()
        {
            _freshStorage = new InMemoryCleanupStorage();
            await cleanupService.PerformAllAsync(_freshStorage);
        }

        [Then("the custom storage should contain a completed cleanup record for {string}")]
        public static void ThenCustomStorageShouldContainCompletedRecordFor(string key)
        {
            Assert.That(CleanupHooks.SharedStorage.Records,
                Has.Some.Matches<CleanupRecord>(r => r.MethodKey == "DeleteTempUser"
                    && r.ArgumentsJson.Contains(key) && r.Status == CleanupStatus.Completed));
        }

        [Then("the freshly created storage passed to PerformAllAsync should remain untouched")]
        public void ThenFreshlyCreatedStorageShouldRemainUntouched()
        {
            Assert.That(_freshStorage!.Records, Is.Empty);
        }
    }
}
