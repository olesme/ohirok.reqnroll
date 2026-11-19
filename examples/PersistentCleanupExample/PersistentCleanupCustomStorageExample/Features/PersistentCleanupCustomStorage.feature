Feature: Persistent cleanup actions backed by a custom storage implementation

  Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin ships no storage backend at all - the
  consumer plugs in their own ICleanupStorage (an in-memory store here, but just as easily a blob
  store, Redis, or anything else). The storage is wired up once, at test-run start, via
  UseStorage(storage) - the ICleanupService itself lives in Reqnroll's global container, so every
  scenario in the run shares the one storage instance, exactly like a real crash-recovery setup
  where cleanup records must survive beyond any single scenario.

  Scenario: A cleanup action registered after storage is wired up lands in that storage
    Given a cleanup action "res-1" is registered
    When all pending cleanups are performed
    Then the custom storage should contain a completed cleanup record for "res-1"

  Scenario: PerformAllAsync(storage) ignores a newly-passed storage once one is already configured
    Given a cleanup action "res-2" is registered
    When all pending cleanups are performed against a freshly created in-memory storage directly
    Then the custom storage should contain a completed cleanup record for "res-2"
    And the freshly created storage passed to PerformAllAsync should remain untouched
