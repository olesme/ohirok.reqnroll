Feature: [OnXFail] / [OnAFail] hooks (positive, must be green)

Scenario: OnXFail fires for a caught xfail step
    #xfail:ArgumentException
    Given step that throws ArgumentException
    Then the hook "xfail" saw "ArgumentException"

Scenario: OnAFail fires for a caught afail step
    #afail
    Given step that throws InvalidOperationException
    Then the hook "afail" saw "InvalidOperationException"

Scenario: OnAFail fires with no exception when the afail step passes
    #afail
    Given step that succeeds
    Then the hook "afail" saw "none"

Scenario: OnXFail does not fire for afail
    #afail
    Given step that throws InvalidOperationException
    Then the hook "xfail" did not run

Scenario: Filtered hook runs only when the filter matches
    #afail
    Given step that throws InvalidOperationException
    Then the hook "filtered-timeout" did not run
