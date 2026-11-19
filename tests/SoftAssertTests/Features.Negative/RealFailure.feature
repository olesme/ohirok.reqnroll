Feature: Soft Assert Real Failure
    Deliberately red-by-design: these scenarios omit the @swallow_flush hook, so the
    aggregated soft failure is genuinely thrown at scenario end. This proves the
    plugin's core guarantee that, unlike XFail, the scenario still fails overall —
    The gate checks it fails for exactly this reason (see ../expected-failures.json).

Scenario: FAIL_soft_assert_step_continues_but_scenario_fails_overall
    #softassert
    When the step throws InvalidOperationException
    Then the next step still executes

Scenario: FAIL_Manual flush in a #softassert step still fails the scenario
    #softassert
    When the step throws InvalidOperationException
    #softassert
    And I flush the soft failures
    Then the next step still executes