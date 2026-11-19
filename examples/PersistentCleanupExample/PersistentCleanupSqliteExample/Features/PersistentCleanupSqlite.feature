Feature: Persistent cleanup actions backed by Sqlite storage

  Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin needs no storage code at all - installing the
  package and configuring "persistentCleanup" in reqnroll.json is enough. Cleanup actions are
  written to the SQLite file AT REGISTRATION TIME, not at execution time, so they survive a crashed
  test process - the next run (or an explicit PerformAllAsync call) can still pick them up and run
  them, unlike a plain [AfterScenario] hook, which never runs if the process dies first.

  Scenario: Cleanup actions execute in configured order, not registration order
    Given a cleanup action "second" is registered with order 20
    And a cleanup action "first" is registered with order 10
    When all pending cleanups are performed
    Then the cleanup actions should have executed in order "first,second"
    And the cleanup statistics should report 0 pending cleanups

  Scenario: A cleanup action that keeps failing is retried up to its budget, then marked failed
    Given a cleanup action "res-1" is registered that always fails, with max retries 2
    When all pending cleanups are performed
    Then the cleanup action "res-1" should have been attempted 2 time(s)
    And the cleanup statistics should report 1 failed cleanup
