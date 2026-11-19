@sequential
Feature: Scenario-level XFail failures (every scenario MUST be red)

  @xfail
  Scenario: FAIL_Scenario xfail, scenario passes
    Given step that succeeds

  @xfail:ArgumentException
  Scenario: FAIL_Scenario xfail with the wrong exception type
    Given step that throws InvalidOperationException
