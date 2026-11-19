# XFailOptions.ExceptionFactory is process-wide by design (set once before the run); the @custom-exceptions
# scenario swaps it for its own duration, so no other scenario of this feature may run at the same time.
@sequential
Feature: XFail library, more expected failures (every scenario here MUST be red; no sentinel step on purpose: the plugin itself must fail the step)

Scenario: FAIL_XFail bare, step passes
    #xfail
    Given step that succeeds

Scenario: FAIL_XFail with type, step passes
    #xfail:ArgumentException
    Given step that succeeds

Scenario: FAIL_XFail with wrong exception type
    #xfail:ArgumentException
    Given step that throws InvalidOperationException

Scenario: FAIL_XFail with excluded type
    #xfail:*Exception,!ArgumentException
    Given step that throws ArgumentException

Scenario: FAIL_AFail with several types but a different one thrown
    #afail:TimeoutException|IOException
    Given step that throws ArgumentException

@custom-exceptions
Scenario: FAIL_Custom exception types are used for unexpected pass
    #xfail
    Given step that succeeds
